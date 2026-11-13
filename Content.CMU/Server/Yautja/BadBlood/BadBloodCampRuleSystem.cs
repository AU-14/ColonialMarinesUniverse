using System.Linq;
using Content.Server.CMU14.Round.Objectives;
using Content.Server.CMU14.Threats.Rules;
using Content.Server.GameTicking.Rules;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.CMU14.Threats;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// Owns the Bad Blood clan's state for the round (<see cref="BadBloodCampRuleComponent"/> on the
/// <c>CMUBadBloodCampRule</c> entity) and is the only writer of Superiority Points, the ledgers and the Leader.
/// <para>
/// This is the core file. Later work adds new partial files and does not edit this one: the end rule, the clan
/// ship and the Blood Reserve each have a set of partial hooks at the bottom. A hook with no body costs nothing.
/// Other systems use the public methods here and the events in BadBloodCampEvents.cs.
/// </para>
/// <para>
/// Dependency fields already taken by the core: _actions, _badBlood, _inventory, _mind, _mobState, _player,
/// _popup, _threatRuleHelper, plus the base class's ChatManager, Timing, RobustRandom and GameTicker.
/// </para>
/// </summary>
public sealed partial class BadBloodCampRuleSystem : GameRuleSystem<BadBloodCampRuleComponent>
{
    private static readonly ProtoId<JobPrototype> ThreatLeaderJob = "AU14JobThreatLeader";
    private const string BracerSlot = "gloves";
    private static readonly TimeSpan TickEvery = TimeSpan.FromSeconds(1);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ThreatRuleHelper _threatRuleHelper = default!;

    private TimeSpan _nextTick;
    private readonly List<EntityUid> _leaderScratch = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<ObjectiveWatchedEntityStartupEvent>(OnThreatRoleApplied);

        InitializeEnd();
        InitializeShip();
        InitializeReserve();
    }

    protected override void Started(EntityUid uid, BadBloodCampRuleComponent component, GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var rule = new Entity<BadBloodCampRuleComponent>(uid, component);
        component.StartedAt = Timing.CurTime;
        component.LandingParty = CountLivingBadBloods();
        _nextTick = TimeSpan.Zero;

        if (FindLandingLeader() is { } leader)
            SetLeader(leader);

        StartedEnd(rule);
        StartedShip(rule);
        StartedReserve(rule);

        var ev = new BadBloodRuleStartedEvent(rule);
        RaiseLocalEvent(ref ev);
    }

    protected override void Ended(EntityUid uid, BadBloodCampRuleComponent component, GameRuleComponent gameRule,
        GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        var rule = new Entity<BadBloodCampRuleComponent>(uid, component);
        EndedEnd(rule);
        EndedShip(rule);
        EndedReserve(rule);

        // nobody leads a clan that has no rule, IsBadBloodLeader goes back to the objective job
        ApplyLeader(rule, null, false);
    }

    protected override void ActiveTick(EntityUid uid, BadBloodCampRuleComponent component, GameRuleComponent gameRule,
        float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        var now = Timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickEvery;

        var rule = new Entity<BadBloodCampRuleComponent>(uid, component);
        UpdateLeaderStatus(rule, now);

        TickReserve(rule, now);
        TickShip(rule, now);
        TickEnd(rule, now);
    }

    #region The rule

    /// <summary>
    /// The running Bad Blood rule. False on rounds without one, and every other method here is then a no-op that
    /// returns false, zero or null.
    /// </summary>
    public bool TryGetRule(out Entity<BadBloodCampRuleComponent> rule)
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out var uid, out _, out var comp, out _))
        {
            rule = (uid, comp);
            return true;
        }

        rule = default;
        return false;
    }

    public bool IsRuleActive()
    {
        return TryGetRule(out _);
    }

    /// <summary>
    /// True for the first <see cref="BadBloodCampRuleComponent.FoundingGrace"/> of the rule while no Lodge has ever
    /// been pitched.
    /// </summary>
    public bool InFoundingGrace()
    {
        return TryGetRule(out var rule) &&
               !rule.Comp.FirstCampPitched &&
               Timing.CurTime < rule.Comp.StartedAt + rule.Comp.FoundingGrace;
    }

    #endregion

    #region Superiority Points

    public FixedPoint4 GetSp()
    {
        return TryGetRule(out var rule) ? rule.Comp.Sp : FixedPoint4.Zero;
    }

    public FixedPoint4 GetLifetimeSp()
    {
        return TryGetRule(out var rule) ? rule.Comp.LifetimeSp : FixedPoint4.Zero;
    }

    /// <summary>
    /// Credits the clan. <paramref name="tierCredit"/> is the share that also counts towards armory tiers:
    /// 1 for trophies and the spit. Totem income goes through <see cref="AddTotemIncome"/> instead.
    /// </summary>
    public bool AddSp(FixedPoint4 amount, float tierCredit = 1f)
    {
        if (amount <= FixedPoint4.Zero || !TryGetRule(out var rule))
            return false;

        var old = rule.Comp.Sp;
        rule.Comp.Sp += amount;
        rule.Comp.LifetimeSp += amount * Math.Clamp(tierCredit, 0f, 1f);
        RaiseSpChanged(rule, old);
        return true;
    }

    /// <summary>
    /// Takes <paramref name="amount"/> from the clan, or nothing if it cannot pay in full.
    /// </summary>
    public bool TrySpendSp(FixedPoint4 amount)
    {
        if (amount < FixedPoint4.Zero || !TryGetRule(out var rule) || rule.Comp.Sp < amount)
            return false;

        if (amount == FixedPoint4.Zero)
            return true;

        var old = rule.Comp.Sp;
        rule.Comp.Sp -= amount;
        RaiseSpChanged(rule, old);
        return true;
    }

    /// <summary>
    /// Gives spent SP back, e.g. for an unclaimed squad seat. Does not count towards tiers a second time.
    /// </summary>
    public bool RefundSp(FixedPoint4 amount)
    {
        return AddSp(amount, 0f);
    }

    /// <summary>
    /// The armory tier the clan has reached: 1 from the start, 2 and 3 by lifetime SP, 4 once the ship has arrived.
    /// 0 without a rule.
    /// </summary>
    public int GetTier()
    {
        if (!TryGetRule(out var rule))
            return 0;

        if (rule.Comp.TierFourUnlocked)
            return 4;

        if (rule.Comp.LifetimeSp >= rule.Comp.TierThreeLifetimeSp)
            return 3;

        return rule.Comp.LifetimeSp >= rule.Comp.TierTwoLifetimeSp ? 2 : 1;
    }

    public bool IsTierUnlocked(int tier)
    {
        return tier >= 1 && GetTier() >= tier;
    }

    private void RaiseSpChanged(Entity<BadBloodCampRuleComponent> rule, FixedPoint4 old)
    {
        var ev = new BadBloodSpChangedEvent(rule, old, rule.Comp.Sp);
        RaiseLocalEvent(ref ev);
    }

    #endregion

    #region Ledgers

    /// <summary>
    /// How many trophies cut from <paramref name="body"/> the Lodge has taken.
    /// </summary>
    public int GetOfferings(EntityUid body)
    {
        return TryGetRule(out var rule) ? rule.Comp.OfferedPerBody.GetValueOrDefault(body) : 0;
    }

    public bool CanOffer(EntityUid body)
    {
        return TryGetRule(out var rule) &&
               rule.Comp.OfferedPerBody.GetValueOrDefault(body) < rule.Comp.MaxOfferingsPerBody;
    }

    /// <summary>
    /// Books one trophy offering from <paramref name="body"/> and pays the clan for it. False, with nothing paid,
    /// once the body has given its <see cref="BadBloodCampRuleComponent.MaxOfferingsPerBody"/>. The caller has
    /// already checked the trophy itself (polished, worthy origin).
    /// </summary>
    public bool TryCreditOffering(EntityUid body)
    {
        if (!TryGetRule(out var rule) || !CanOffer(body))
            return false;

        rule.Comp.OfferedPerBody[body] = rule.Comp.OfferedPerBody.GetValueOrDefault(body) + 1;
        AddSp(rule.Comp.OfferingSp);
        return true;
    }

    /// <summary>
    /// Spit SP <paramref name="body"/> can still give.
    /// </summary>
    public FixedPoint4 GetCookRemaining(EntityUid body)
    {
        if (!TryGetRule(out var rule))
            return FixedPoint4.Zero;

        var left = rule.Comp.CookSpCapPerBody - rule.Comp.CookSpPerBody.GetValueOrDefault(body);
        return left > FixedPoint4.Zero ? left : FixedPoint4.Zero;
    }

    /// <summary>
    /// Pays the clan for cooking <paramref name="body"/> or a piece of it, up to the body's cap. Returns what was
    /// actually paid, zero once the body is used up. Pass <see cref="BadBloodCampRuleComponent.CorpseCookSp"/> for a
    /// whole corpse and <see cref="BadBloodCampRuleComponent.MeatCookSp"/> for a loose cut.
    /// </summary>
    public FixedPoint4 TryCreditCook(EntityUid body, FixedPoint4 amount)
    {
        if (amount <= FixedPoint4.Zero || !TryGetRule(out var rule))
            return FixedPoint4.Zero;

        var paid = FixedPoint4.Min(amount, GetCookRemaining(body));
        if (paid <= FixedPoint4.Zero)
            return FixedPoint4.Zero;

        rule.Comp.CookSpPerBody[body] = rule.Comp.CookSpPerBody.GetValueOrDefault(body) + paid;
        AddSp(paid);
        return paid;
    }

    #endregion

    #region Clan Totems

    /// <summary>
    /// The Totem income entry point. Pays the clan in full and counts
    /// <see cref="BadBloodCampRuleComponent.TotemTierCredit"/> of it towards armory tiers.
    /// </summary>
    public bool AddTotemIncome(FixedPoint4 amount)
    {
        return TryGetRule(out var rule) && AddSp(amount, rule.Comp.TotemTierCredit);
    }

    /// <summary>
    /// 3 Totems, or 4 on a high population round.
    /// </summary>
    public int GetTotemLimit()
    {
        if (!TryGetRule(out var rule))
            return 0;

        return GetPlayerCount() > rule.Comp.TotemHighPopPlayers
            ? rule.Comp.TotemLimitHighPop
            : rule.Comp.TotemLimit;
    }

    /// <summary>
    /// The clan's standing Totems. Deleted ones are dropped from the list first.
    /// </summary>
    public IReadOnlyList<EntityUid> GetTotems()
    {
        if (!TryGetRule(out var rule))
            return Array.Empty<EntityUid>();

        rule.Comp.Totems.RemoveAll(totem => TerminatingOrDeleted(totem));
        return rule.Comp.Totems;
    }

    /// <summary>
    /// Adds a Totem to the clan's list. False when the clan is at its limit, the Totem must then not be built.
    /// </summary>
    public bool TryRegisterTotem(EntityUid totem)
    {
        if (!TryGetRule(out var rule))
            return false;

        var totems = GetTotems();
        if (totems.Contains(totem))
            return true;

        if (totems.Count >= GetTotemLimit())
            return false;

        rule.Comp.Totems.Add(totem);
        return true;
    }

    public void UnregisterTotem(EntityUid totem)
    {
        if (TryGetRule(out var rule))
            rule.Comp.Totems.Remove(totem);
    }

    /// <summary>
    /// Uses up one free Totem, if the clan has one. The first Lodge grants one.
    /// </summary>
    public bool TryTakeFreeTotem()
    {
        if (!TryGetRule(out var rule) || rule.Comp.FreeTotems <= 0)
            return false;

        rule.Comp.FreeTotems--;
        return true;
    }

    #endregion

    #region The Leader

    /// <summary>
    /// The clan's Leader, dead or alive. Null before one is picked and after the body is deleted.
    /// </summary>
    public EntityUid? GetLeader()
    {
        if (!TryGetRule(out var rule) || rule.Comp.Leader is not { } leader || TerminatingOrDeleted(leader))
            return null;

        return leader;
    }

    /// <summary>
    /// The Leader is dead, deleted, evacuated, or disconnected for longer than
    /// <see cref="BadBloodCampRuleComponent.LeaderSsdTimeout"/>. Also true while the clan has never had one.
    /// </summary>
    public bool IsLeaderGone()
    {
        return !TryGetRule(out var rule) || rule.Comp.Leader == null || rule.Comp.LeaderGoneAt != null;
    }

    /// <summary>
    /// A Leader who is not gone. The ship's summoning pauses without one.
    /// </summary>
    public bool HasLivingLeader()
    {
        return !IsLeaderGone();
    }

    /// <summary>
    /// Hands leadership to <paramref name="leader"/>: moves <see cref="BadBloodLeaderComponent"/> and the Pitch
    /// Camp action, and marks the bracer they wear as a leader bracer. Pass null to leave the clan leaderless.
    /// It does not touch the threat objective job or the old Leader's bracer, the ascension vote does those.
    /// False if there is no rule or <paramref name="leader"/> is not a Bad Blood.
    /// </summary>
    public bool SetLeader(EntityUid? leader, bool promoteBracer = true)
    {
        if (!TryGetRule(out var rule))
            return false;

        if (leader is { } uid && (TerminatingOrDeleted(uid) || !_badBlood.IsBadBlood(uid)))
            return false;

        ApplyLeader(rule, leader, promoteBracer);
        return true;
    }

    private void ApplyLeader(Entity<BadBloodCampRuleComponent> rule, EntityUid? leader, bool promoteBracer)
    {
        var old = rule.Comp.Leader;

        _leaderScratch.Clear();
        var holders = EntityQueryEnumerator<BadBloodLeaderComponent>();
        while (holders.MoveNext(out var holder, out _))
        {
            if (holder != leader)
                _leaderScratch.Add(holder);
        }

        foreach (var holder in _leaderScratch)
        {
            if (TryComp(holder, out BadBloodLeaderComponent? held))
                _actions.RemoveAction(holder, held.PitchCampActionEntity);

            RemComp<BadBloodLeaderComponent>(holder);
        }

        if (leader is { } uid)
        {
            var comp = EnsureComp<BadBloodLeaderComponent>(uid);
            _actions.AddAction(uid, ref comp.PitchCampActionEntity, comp.PitchCampAction);
            Dirty(uid, comp);

            if (promoteBracer &&
                _inventory.TryGetSlotEntity(uid, BracerSlot, out var bracer) &&
                HasComp<YautjaBracerComponent>(bracer))
            {
                EnsureComp<BadBloodLeaderBracerComponent>(bracer.Value);
            }

            rule.Comp.LeaderEverAssigned = true;
        }

        rule.Comp.Leader = leader;
        rule.Comp.LeaderGoneAt = null;
        rule.Comp.LeaderDisconnectedAt = null;

        if (old == leader)
            return;

        LeaderChangedEnd(rule, old, leader);
        LeaderChangedShip(rule, old, leader);
        LeaderChangedReserve(rule, old, leader);

        var ev = new BadBloodLeaderChangedEvent(rule, old, leader);
        RaiseLocalEvent(ref ev);
    }

    private EntityUid? FindLandingLeader()
    {
        var query = EntityQueryEnumerator<YautjaBadBloodComponent, ThreatComponent>();
        while (query.MoveNext(out var uid, out _, out var threat))
        {
            if (threat.ObjectiveJob == ThreatLeaderJob && !_mobState.IsDead(uid))
                return uid;
        }

        return null;
    }

    // a Leader body that gets its threat role after the rule started, e.g. a late forced spawn
    private void OnThreatRoleApplied(ObjectiveWatchedEntityStartupEvent ev)
    {
        if (!TryGetRule(out var rule) || rule.Comp.LeaderEverAssigned)
            return;

        if (_badBlood.IsBadBlood(ev.Uid) &&
            TryComp(ev.Uid, out ThreatComponent? threat) &&
            threat.ObjectiveJob == ThreatLeaderJob &&
            !_mobState.IsDead(ev.Uid))
        {
            SetLeader(ev.Uid);
        }
    }

    private void UpdateLeaderStatus(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;
        if (!comp.LeaderEverAssigned)
            return;

        bool gone;
        if (comp.Leader is not { } leader ||
            TerminatingOrDeleted(leader) ||
            _mobState.IsDead(leader) ||
            _threatRuleHelper.IsEvacuated(leader))
        {
            gone = true;
        }
        else if (!HasComp<ActorComponent>(leader))
        {
            comp.LeaderDisconnectedAt ??= now;
            gone = now - comp.LeaderDisconnectedAt.Value >= comp.LeaderSsdTimeout;
        }
        else
        {
            comp.LeaderDisconnectedAt = null;
            gone = false;
        }

        if (!gone)
        {
            comp.LeaderGoneAt = null;
            return;
        }

        if (comp.LeaderGoneAt != null)
            return;

        comp.LeaderGoneAt = now;

        LeaderGoneEnd(rule);
        LeaderGoneShip(rule);
        LeaderGoneReserve(rule);

        var ev = new BadBloodLeaderGoneEvent(rule, comp.Leader);
        RaiseLocalEvent(ref ev);
    }

    #endregion

    #region The Lodge

    /// <summary>
    /// The standing Lodge, if there is one.
    /// </summary>
    public EntityUid? GetLodge()
    {
        if (!TryGetRule(out var rule) || rule.Comp.Camp is not { } lodge || TerminatingOrDeleted(lodge))
            return null;

        return lodge;
    }

    public bool HasLodge()
    {
        return GetLodge() != null;
    }

    /// <summary>
    /// For <see cref="BadBloodCampSystem"/> only: books a new Lodge. False if another Lodge already stands.
    /// </summary>
    public bool TryRegisterLodge(EntityUid lodge, out bool first)
    {
        first = false;
        if (!TryGetRule(out var rule))
            return false;

        if (GetLodge() is { } standing)
            return standing == lodge;

        first = !rule.Comp.FirstCampPitched;
        rule.Comp.Camp = lodge;
        rule.Comp.CampPlaced = true;
        rule.Comp.KitOut = false;
        rule.Comp.FirstCampPitched = true;
        if (first)
            rule.Comp.FreeTotems++;

        LodgePitchedEnd(rule, lodge, first);
        LodgePitchedShip(rule, lodge, first);
        LodgePitchedReserve(rule, lodge, first);
        return true;
    }

    /// <summary>
    /// For <see cref="BadBloodCampSystem"/> only: the Lodge is gone, start the rebuild cooldown.
    /// </summary>
    public bool UnregisterLodge(EntityUid lodge)
    {
        if (!TryGetRule(out var rule) || rule.Comp.Camp != lodge)
            return false;

        rule.Comp.Camp = null;
        rule.Comp.CampPlaced = false;
        rule.Comp.NextCampAt = Timing.CurTime + rule.Comp.CampRebuildCooldown;

        LodgeDestroyedEnd(rule, lodge);
        LodgeDestroyedShip(rule, lodge);
        LodgeDestroyedReserve(rule, lodge);
        return true;
    }

    #endregion

    #region The clan ship

    public BadBloodShipState GetShipState()
    {
        return TryGetRule(out var rule) ? rule.Comp.ShipState : BadBloodShipState.None;
    }

    public bool IsShipInbound()
    {
        return GetShipState() == BadBloodShipState.Inbound;
    }

    public bool IsShipArrived()
    {
        return GetShipState() == BadBloodShipState.Arrived;
    }

    #endregion

    #region Headcount

    public int GetPlayerCount()
    {
        return _player.PlayerCount;
    }

    /// <summary>
    /// The most Bad Bloods and hounds together the round allows: a share of the player count.
    /// </summary>
    public int GetThreatBudget()
    {
        if (!TryGetRule(out var rule))
            return 0;

        return (int) MathF.Round(GetPlayerCount() * rule.Comp.ThreatBudgetFraction, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The most living Bad Bloods the clan may have: the landing party + the founding squad + 2, or the seats the
    /// vote held, whichever is higher. Once the ship has arrived it is the threat budget. Callers still check
    /// <see cref="GetThreatBudget"/> for Bad Bloods and hounds together.
    /// </summary>
    public int GetLivingBadBloodCap()
    {
        if (!TryGetRule(out var rule))
            return 0;

        if (rule.Comp.ShipState == BadBloodShipState.Arrived)
            return GetThreatBudget();

        return Math.Max(rule.Comp.LandingParty + rule.Comp.FoundingSeats + rule.Comp.LivingCapBonus,
            rule.Comp.HeldVoteSeats);
    }

    /// <summary>
    /// Bad Bloods that are not dead. With <paramref name="connectedOnly"/>, only those a player is driving.
    /// </summary>
    public int CountLivingBadBloods(bool connectedOnly = false)
    {
        var count = 0;
        var query = EntityQueryEnumerator<YautjaBadBloodComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out _, out var mobState))
        {
            if (_mobState.IsDead(uid, mobState))
                continue;

            if (connectedOnly && !HasComp<ActorComponent>(uid))
                continue;

            count++;
        }

        return count;
    }

    /// <summary>
    /// The player lost a Bad Blood body this round.
    /// </summary>
    public bool IsDeadBadBlood(NetUserId user)
    {
        return TryGetRule(out var rule) && rule.Comp.DeadBadBloods.ContainsKey(user);
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead ||
            !_badBlood.IsBadBlood(args.Target) ||
            !TryGetRule(out var rule))
        {
            return;
        }

        var now = Timing.CurTime;
        if (_mind.TryGetMind(args.Target, out _, out var mind) && mind.UserId is { } user)
            rule.Comp.DeadBadBloods[user] = now;

        if (rule.Comp.Leader == args.Target)
            UpdateLeaderStatus(rule, now);

        BadBloodDiedEnd(rule, args.Target);
        BadBloodDiedShip(rule, args.Target);
        BadBloodDiedReserve(rule, args.Target);

        var ev = new BadBloodDiedEvent(rule, args.Target);
        RaiseLocalEvent(ref ev);
    }

    #endregion

    #region Messages

    /// <summary>
    /// Tells every Bad Blood player something, in chat and as a popup over their own body.
    /// </summary>
    public void AnnounceToClan(string message, PopupType popup = PopupType.Medium)
    {
        var filter = Filter.Empty().AddWhereAttachedEntity(_badBlood.IsBadBlood);
        ChatManager.ChatMessageToManyFiltered(
            filter,
            ChatChannel.Notifications,
            message,
            message,
            EntityUid.Invalid,
            false,
            true,
            null);

        var query = EntityQueryEnumerator<YautjaBadBloodComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            _popup.PopupEntity(message, uid, uid, popup);
        }
    }

    #endregion

    #region Hooks for later partial files

    // Implement the ones you need in your own partial file. Do not subscribe to MobStateChangedEvent or
    // ObjectiveWatchedEntityStartupEvent again in this class, use the hooks.
    // Tick* runs once a second while the rule is active, in the order Reserve, Ship, End.

    // the end rule (marines win, Clan Wipe)
    partial void InitializeEnd();
    partial void StartedEnd(Entity<BadBloodCampRuleComponent> rule);
    partial void EndedEnd(Entity<BadBloodCampRuleComponent> rule);
    partial void TickEnd(Entity<BadBloodCampRuleComponent> rule, TimeSpan now);
    partial void LeaderChangedEnd(Entity<BadBloodCampRuleComponent> rule, EntityUid? old, EntityUid? leader);
    partial void LeaderGoneEnd(Entity<BadBloodCampRuleComponent> rule);
    partial void LodgePitchedEnd(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge, bool first);
    partial void LodgeDestroyedEnd(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge);
    partial void BadBloodDiedEnd(Entity<BadBloodCampRuleComponent> rule, EntityUid body);

    // the Ship Call-In (call, arrival, summoning, departure)
    partial void InitializeShip();
    partial void StartedShip(Entity<BadBloodCampRuleComponent> rule);
    partial void EndedShip(Entity<BadBloodCampRuleComponent> rule);
    partial void TickShip(Entity<BadBloodCampRuleComponent> rule, TimeSpan now);
    partial void LeaderChangedShip(Entity<BadBloodCampRuleComponent> rule, EntityUid? old, EntityUid? leader);
    partial void LeaderGoneShip(Entity<BadBloodCampRuleComponent> rule);
    partial void LodgePitchedShip(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge, bool first);
    partial void LodgeDestroyedShip(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge);
    partial void BadBloodDiedShip(Entity<BadBloodCampRuleComponent> rule, EntityUid body);

    // the Blood Reserve
    partial void InitializeReserve();
    partial void StartedReserve(Entity<BadBloodCampRuleComponent> rule);
    partial void EndedReserve(Entity<BadBloodCampRuleComponent> rule);
    partial void TickReserve(Entity<BadBloodCampRuleComponent> rule, TimeSpan now);
    partial void LeaderChangedReserve(Entity<BadBloodCampRuleComponent> rule, EntityUid? old, EntityUid? leader);
    partial void LeaderGoneReserve(Entity<BadBloodCampRuleComponent> rule);
    partial void LodgePitchedReserve(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge, bool first);
    partial void LodgeDestroyedReserve(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge);
    partial void BadBloodDiedReserve(Entity<BadBloodCampRuleComponent> rule, EntityUid body);

    #endregion
}
