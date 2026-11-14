using Content.Server.Chat.Managers;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-115: Bad Blood hounds. A hound only ever comes from a sleeping hound, through the shared wake path in
/// <see cref="YautjaSleepingHellhoundSystem"/>, and only a ghost drives it: its HTN is idle. This system gates who
/// wakes a sleeper, orders who may take the hound, puts an unclaimed hound back to sleep and lays out the landing
/// hounds. Buying one is in BadBloodCampSystem.Kennel.cs.
/// </summary>
public sealed partial class BadBloodHoundSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private IChatManager _chat = default!;
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
    /// The most hounds the clan may have: one for every two living Bad Bloods, rounded up.
    /// </summary>
    public int GetHoundCap()
    {
        return (_rule.CountLivingBadBloods() + 1) / 2;
    }

    /// <summary>
    /// How many more hounds the clan may buy: under the hound cap, and under the threat budget that Bad Bloods,
    /// open squad seats and hounds share.
    /// </summary>
    public int GetHoundRoom()
    {
        if (!_rule.IsRuleActive())
            return 0;

        var hounds = CountHounds();
        var byCap = GetHoundCap() - hounds;
        var byBudget = _rule.GetThreatBudget() - _rule.CountLivingBadBloods() - _camp.GetPendingSquadSeats() - hounds;
        return Math.Max(0, Math.Min(byCap, byBudget));
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
    /// The landing hounds: one sleeper for every two Bad Bloods of the landing party, beside the Leader.
    /// Returns how many were laid out.
    /// </summary>
    public int SpawnLandingHounds(Entity<BadBloodCampRuleComponent> rule)
    {
        var count = rule.Comp.LandingParty / 2;
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
    /// Whether <paramref name="user"/> may wake the sleeper: a Bad Blood, and while a buyer's reservation runs,
    /// that buyer. <paramref name="reason"/> is a locale id.
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
    /// Whether <paramref name="session"/> may ask for the hound right now. The reserve goes first for
    /// <see cref="BadBloodHoundComponent.ReservePriority"/>, then this round's dead Bad Bloods for
    /// <see cref="BadBloodCampRuleComponent.DeadPriorityWindow"/>, then anyone. A window is skipped when the clan
    /// has nobody of that kind. This round's honourable hunters never get one. <paramref name="reason"/> is a
    /// locale id.
    /// </summary>
    public bool CanTakeHound(Entity<BadBloodHoundComponent> hound, ICommonSession session, out string reason)
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

        if (rule.Comp.ReserveQueue.Contains(user))
            return true;

        var now = _timing.CurTime;
        var open = hound.Comp.WokenAt;
        if (rule.Comp.ReserveQueue.Count > 0)
        {
            open += hound.Comp.ReservePriority;
            if (now < open)
            {
                reason = "cmu-bad-blood-hound-seat-reserve";
                return false;
            }
        }

        if (rule.Comp.DeadBadBloods.ContainsKey(user))
            return true;

        if (rule.Comp.DeadBadBloods.Count > 0 && now < open + rule.Comp.DeadPriorityWindow)
        {
            reason = "cmu-bad-blood-hound-seat-priority";
            return false;
        }

        return true;
    }

    private void OnHoundRequestAttempt(Entity<BadBloodHoundComponent> ent, ref GhostRoleRequestAttemptEvent args)
    {
        if (args.Cancelled || CanTakeHound(ent, args.Player, out var reason))
            return;

        args.Cancelled = true;
        if (args.ExplicitRequest)
            _chat.DispatchServerMessage(args.Player, Loc.GetString(reason));
    }

    // nobody took it: it lies down again at the kennel, or where it stands if the clan has none. no SP is lost,
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
