using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared._RMC14.Gibbing;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-115: Bad Blood hounds, and only a ghost drives one: its HTN is idle. The landing hounds are sleepers, woken
/// through the shared wake path in <see cref="YautjaSleepingHellhoundSystem"/>; CMU14 ARCH-603: every other hound is
/// taken free from the kennel's spawner (BadBloodCampSystem.Kennel.cs). This system gates who wakes a sleeper, orders
/// who may take a hound (FLOW-605: not again within a minute of their last hound's death), puts an unclaimed woken
/// hound back to sleep and lays out the landing hounds.
/// </summary>
public sealed partial class BadBloodHoundSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private YautjaPredatorRoundSystem _predatorRound = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;
    private readonly List<EntityUid> _sleepScratch = new();

    public override void Initialize()
    {
        // the gate runs first and swallows the click, so the shared wake dialog never opens for the wrong hands
        SubscribeLocalEvent<BadBloodSleepingHoundComponent, InteractHandEvent>(OnSleeperInteractHand,
            before: new[] { typeof(YautjaSleepingHellhoundSystem) });
        SubscribeLocalEvent<BadBloodSleepingHoundComponent, ActivateInWorldEvent>(OnSleeperActivate,
            before: new[] { typeof(YautjaSleepingHellhoundSystem) });

        SubscribeLocalEvent<BadBloodHoundComponent, MapInitEvent>(OnHoundMapInit);
        SubscribeLocalEvent<BadBloodHoundComponent, MindAddedMessage>(OnHoundMindAdded);
        SubscribeLocalEvent<BadBloodHoundComponent, GhostRoleRequestAttemptEvent>(OnHoundRequestAttempt);
        // CMU14 FLOW-605: the dead hound's mind is read before the gib sends it to a ghost
        SubscribeLocalEvent<BadBloodHoundComponent, MobStateChangedEvent>(OnHoundMobStateChanged,
            before: new[] { typeof(RMCGibSystem) });

        // CMU14 ARCH-603: the kennel's free hounds
        SubscribeLocalEvent<BadBloodKennelSpawnerComponent, GhostRoleRequestAttemptEvent>(OnKennelRequestAttempt);

        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;

        _sleepScratch.Clear();
        var query = EntityQueryEnumerator<BadBloodHoundComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var hound, out var mobState))
        {
            if (hound.Claimed ||
                now < hound.WokenAt + hound.UnclaimedSleepAfter ||
                _mobState.IsDead(uid, mobState) ||
                HasComp<ActorComponent>(uid) ||
                // somebody is about to win it
                HasComp<GhostRoleRaffleComponent>(uid))
            {
                continue;
            }

            _sleepScratch.Add(uid);
        }

        foreach (var uid in _sleepScratch)
        {
            PutBackToSleep(uid);
        }

        _sleepScratch.Clear();
    }

    #region Counting

    /// <summary>
    /// The clan's hounds: the living ones, and with <paramref name="includeSleeping"/> the sleepers waiting to be
    /// woken as well.
    /// </summary>
    public int CountHounds(bool includeSleeping = true)
    {
        var count = 0;
        var hounds = EntityQueryEnumerator<BadBloodHoundComponent, MobStateComponent>();
        while (hounds.MoveNext(out var uid, out _, out var mobState))
        {
            if (!_mobState.IsDead(uid, mobState))
                count++;
        }

        if (!includeSleeping)
            return count;

        var sleepers = EntityQueryEnumerator<BadBloodSleepingHoundComponent>();
        while (sleepers.MoveNext(out var uid, out _))
        {
            if (!TerminatingOrDeleted(uid))
                count++;
        }

        return count;
    }

    /// <summary>
    /// The most hounds the clan may have: one for every <see cref="BadBloodCampRuleComponent.HoundCapDivisor"/>
    /// living Bad Bloods, rounded up, plus <see cref="BadBloodCampRuleComponent.HoundCapBonus"/>. 0 without a rule.
    /// </summary>
    public int GetHoundCap()
    {
        if (!_rule.TryGetRule(out var rule))
            return 0;

        // a divisor of zero or less counts as 1 (one hound per Bad Blood) rather than lifting the cap
        var divisor = Math.Max(1, rule.Comp.HoundCapDivisor);
        return (_rule.CountLivingBadBloods() + divisor - 1) / divisor + Math.Max(0, rule.Comp.HoundCapBonus);
    }

    #endregion

    #region Sleepers

    /// <summary>
    /// Puts a sleeping Bad Blood hound at <paramref name="coords"/>, free of charge and outside the caps. With
    /// <paramref name="reservedFor"/> only that Bad Blood can wake it for <paramref name="reservation"/>.
    /// </summary>
    public EntityUid SpawnSleepingHound(
        EntityCoordinates coords,
        EntityUid? reservedFor = null,
        TimeSpan? reservation = null,
        string prototype = "CMUBadBloodSleepingHound")
    {
        var sleeper = Spawn(prototype, coords);
        var comp = EnsureComp<BadBloodSleepingHoundComponent>(sleeper);
        if (reservedFor != null && reservation is { } time)
        {
            comp.ReservedFor = reservedFor;
            comp.ReservedUntil = _timing.CurTime + time;
        }

        return sleeper;
    }

    /// <summary>
    /// The landing hounds: one sleeper for every <see cref="BadBloodCampRuleComponent.LandingHoundDivisor"/> Bad
    /// Bloods of the landing party, rounded up, beside the Leader. Returns how many were laid out.
    /// </summary>
    public int SpawnLandingHounds(Entity<BadBloodCampRuleComponent> rule)
    {
        var divisor = rule.Comp.LandingHoundDivisor;
        var count = divisor > 0 ? (rule.Comp.LandingParty + divisor - 1) / divisor : 0;
        if (count <= 0)
            return 0;

        var anchor = _rule.GetLeader();
        if (anchor == null)
        {
            var clan = EntityQueryEnumerator<YautjaBadBloodComponent, MobStateComponent>();
            while (clan.MoveNext(out var uid, out _, out var mobState))
            {
                if (_mobState.IsDead(uid, mobState))
                    continue;

                anchor = uid;
                break;
            }
        }

        if (anchor is not { } beside)
            return 0;

        var coords = Transform(beside).Coordinates;
        for (var i = 0; i < count; i++)
        {
            SpawnSleepingHound(coords);
        }

        return count;
    }

    /// <summary>
    /// Whether <paramref name="user"/> may wake the sleeper: a Bad Blood, and while a reservation from
    /// <see cref="SpawnSleepingHound"/> runs, the one it is reserved for. <paramref name="reason"/> is a locale id.
    /// </summary>
    public bool CanWake(Entity<BadBloodSleepingHoundComponent> sleeper, EntityUid user, out string reason)
    {
        reason = string.Empty;
        if (!_badBlood.IsBadBlood(user))
        {
            reason = "cmu-bad-blood-hound-sleeper-not-clan";
            return false;
        }

        if (sleeper.Comp.ReservedFor is { } buyer &&
            buyer != user &&
            _timing.CurTime < sleeper.Comp.ReservedUntil &&
            !TerminatingOrDeleted(buyer) &&
            !_mobState.IsDead(buyer))
        {
            reason = "cmu-bad-blood-hound-sleeper-reserved";
            return false;
        }

        return true;
    }

    private void OnSleeperInteractHand(Entity<BadBloodSleepingHoundComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !RefuseWake(ent, args.User))
            return;

        args.Handled = true;
    }

    private void OnSleeperActivate(Entity<BadBloodSleepingHoundComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !RefuseWake(ent, args.User))
            return;

        args.Handled = true;
    }

    private bool RefuseWake(Entity<BadBloodSleepingHoundComponent> ent, EntityUid user)
    {
        if (CanWake(ent, user, out var reason))
            return false;

        _popup.PopupEntity(Loc.GetString(reason), ent, user, PopupType.SmallCaution);
        return true;
    }

    #endregion

    #region Woken hounds

    private void OnHoundMapInit(Entity<BadBloodHoundComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.WokenAt = _timing.CurTime;
    }

    private void OnHoundMindAdded(Entity<BadBloodHoundComponent> ent, ref MindAddedMessage args)
    {
        ent.Comp.Claimed = true;
    }

    /// <summary>
    /// Whether <paramref name="session"/> may ask for this woken hound right now. The reserve goes first for
    /// <see cref="BadBloodHoundComponent.ReservePriority"/> after it woke, then anyone; the window is skipped when the
    /// clan has no reserve. A dead Bad Blood gets no priority. This round's honourable hunters never get one, and
    /// nobody does within <see cref="BadBloodCampRuleComponent.HoundRespawnDelay"/> of their last hound's death.
    /// <paramref name="reason"/> is a locale id.
    /// </summary>
    public bool CanTakeHound(Entity<BadBloodHoundComponent> hound, ICommonSession session, out string reason)
    {
        return CanTakeHound(session, hound.Comp.WokenAt + hound.Comp.ReservePriority, out reason);
    }

    /// <summary>
    /// CMU14 ARCH-603: whether <paramref name="session"/> may take a hound from the kennel right now. The same order
    /// as a woken hound, with the reserve's window counted from the kennel's last stocked takeover.
    /// </summary>
    public bool CanTakeHound(Entity<BadBloodKennelSpawnerComponent> kennel, ICommonSession session, out string reason)
    {
        // a kennel left standing after the clan's rule ended offers nothing
        if (!_rule.IsRuleActive())
        {
            reason = "cmu-bad-blood-kennel-no-clan";
            return false;
        }

        return CanTakeHound(session, kennel.Comp.StockedAt + kennel.Comp.ReservePriority, out reason);
    }

    private bool CanTakeHound(ICommonSession session, TimeSpan reserveUntil, out string reason)
    {
        reason = string.Empty;
        var user = session.UserId;
        if (_predatorRound.WasHunterThisRound(user))
        {
            reason = "cmu-bad-blood-hound-seat-hunter";
            return false;
        }

        // a hound spawned outside a Bad Blood round is an ordinary ghost role
        if (!_rule.TryGetRule(out var rule))
            return true;

        // CMU14 FLOW-605: a hound player waits out the respawn delay, reserve or not
        if (GetHoundRespawnWait(user) > TimeSpan.Zero)
        {
            reason = "cmu-bad-blood-hound-respawn-wait";
            return false;
        }

        if (rule.Comp.ReserveQueue.Contains(user))
            return true;

        // CMU14: FLOW-601, no window for the clan's dead: after the reserve, anyone
        if (rule.Comp.ReserveQueue.Count > 0 && _timing.CurTime < reserveUntil)
        {
            reason = "cmu-bad-blood-hound-seat-reserve";
            return false;
        }

        return true;
    }

    /// <summary>
    /// CMU14 FLOW-605: how long <paramref name="user"/> still waits before taking another hound. Zero once
    /// <see cref="BadBloodCampRuleComponent.HoundRespawnDelay"/> has passed since their last hound died, or without a
    /// rule.
    /// </summary>
    public TimeSpan GetHoundRespawnWait(NetUserId user)
    {
        if (!_rule.TryGetRule(out var rule) || !rule.Comp.HoundDiedAt.TryGetValue(user, out var diedAt))
            return TimeSpan.Zero;

        var left = diedAt + rule.Comp.HoundRespawnDelay - _timing.CurTime;
        return left > TimeSpan.Zero ? left : TimeSpan.Zero;
    }

    private void OnHoundRequestAttempt(Entity<BadBloodHoundComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled || CanTakeHound(ent, args.Player, out var reason))
            return;

        Refuse(ref args, reason);
    }

    private void OnKennelRequestAttempt(Entity<BadBloodKennelSpawnerComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled || CanTakeHound(ent, args.Player, out var reason))
            return;

        Refuse(ref args, reason);
    }

    private void Refuse(ref GhostRoleRequestAttemptEvent args, string reason)
    {
        args.Cancelled = true;
        if (!args.ExplicitRequest)
            return;

        var seconds = (int) Math.Ceiling(GetHoundRespawnWait(args.Player.UserId).TotalSeconds);
        _chat.DispatchServerMessage(args.Player, Loc.GetString(reason, ("seconds", seconds)));
    }

    // CMU14 FLOW-605: the player who drove a hound that just died starts their respawn delay
    private void OnHoundMobStateChanged(Entity<BadBloodHoundComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !_rule.TryGetRule(out var rule) ||
            !_mind.TryGetMind(ent.Owner, out _, out var mind) ||
            mind.UserId is not { } user)
        {
            return;
        }

        rule.Comp.HoundDiedAt[user] = _timing.CurTime;
    }

    // nobody took it: it lies down again at the kennel, or where it stands if the clan has none. nothing is lost,
    // the sleeper can be woken again
    private void PutBackToSleep(EntityUid hound)
    {
        if (TerminatingOrDeleted(hound) || !TryComp(hound, out BadBloodHoundComponent? comp))
            return;

        var at = _camp.TryGetPart(BadBloodCampPartKind.Kennel, out var kennel) ? kennel : hound;
        var xform = Transform(at);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map))
            return;

        SpawnSleepingHound(xform.Coordinates, prototype: comp.SleeperPrototype);
        QueueDel(hound);
    }

    #endregion

    private void OnRuleStarted(ref BadBloodRuleStartedEvent args)
    {
        SpawnLandingHounds(args.Rule);
    }
}
