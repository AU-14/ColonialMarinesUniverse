using System.Globalization;
using System.Numerics;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-118, Clan Totems: the clan's territory. Any Bad Blood raises one with the Raise Totem action, away from the
/// Lodge, other Totems and the landing zones. A standing Totem pays the clan through the rule
/// (<see cref="BadBloodCampRuleSystem.AddTotemIncome"/>), more the longer it has stood, and gives the Bad Bloods
/// around it an aura. Marines are told where it is, see it on their map after a while, and reveal the Lodge by
/// destroying it. FLOW-503: a marine desecrates it with the Desecrate verb and a do-after (no income, not ripe, no
/// aura) until a Bad Blood reconsecrates it the same way. Nothing here depends on who stands nearby.
/// </summary>
public sealed partial class BadBloodTotemSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private IntelSystem _intel = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SatiationSystem _satiation = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTacticalMapSystem _tacticalMap = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private TimeSpan _nextUpdate;
    private readonly List<EntityUid> _scratch = new();
    private readonly HashSet<EntityUid> _inAura = new();
    private readonly List<(MapId Map, Vector2 Position, float Radius)> _auras = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodTotemComponent, MapInitEvent>(OnTotemMapInit);
        SubscribeLocalEvent<BadBloodTotemComponent, DestructionEventArgs>(OnTotemDestruction);
        SubscribeLocalEvent<BadBloodTotemComponent, EntityTerminatingEvent>(OnTotemTerminating);
        SubscribeLocalEvent<BadBloodTotemComponent, ExaminedEvent>(OnTotemExamined);
        SubscribeLocalEvent<BadBloodTotemComponent, GetVerbsEvent<Verb>>(OnTotemVerbs);
        SubscribeLocalEvent<BadBloodTotemComponent, BadBloodTotemConsecrationDoAfterEvent>(OnConsecrationDoAfter);

        SubscribeLocalEvent<BadBloodTotemBuilderComponent, BadBloodRaiseTotemActionEvent>(OnRaiseTotem);
        SubscribeLocalEvent<BadBloodTotemBuilderComponent, BadBloodRaiseTotemDoAfterEvent>(OnRaiseTotemDoAfter);

        SubscribeLocalEvent<BadBloodTotemAuraComponent, DamageChangedEvent>(OnAuraDamaged);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;

        SyncBuilders();
        UpdateTotems(now, UpdateEvery);
        UpdateAuras(now, UpdateEvery);
    }

    #region The Totem

    private void OnTotemMapInit(Entity<BadBloodTotemComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.PlacedAt = _timing.CurTime;
        ent.Comp.Registered = _rule.TryRegisterTotem(ent.Owner);
    }

    private void OnTotemDestruction(Entity<BadBloodTotemComponent> ent, ref DestructionEventArgs args)
    {
        if (ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        var counted = ent.Comp.Registered;
        ent.Comp.Registered = false;
        _rule.UnregisterTotem(ent.Owner);

        // a Totem the clan never counted is scenery: no loot, no reveal, no announcement
        if (!counted || !_rule.IsRuleActive())
            return;

        if (ent.Comp.Loot is { } loot)
            Spawn(loot, Transform(ent.Owner).Coordinates);

        _camp.RevealLodge(ent.Comp.LodgeRevealTime, BadBloodRevealSource.TotemFell);

        if (ent.Comp.DestroyedIntel > FixedPoint2.Zero)
            _intel.AddPoints(_intel.EnsureTechTree(ent.Comp.IntelTeam), ent.Comp.DestroyedIntel, ent.Comp.IntelTeam);

        var area = _area.GetAreaName(ent.Owner);
        _marineAnnounce.AnnounceToMarines(Loc.GetString("cmu-bad-blood-totem-destroyed-marines", ("area", area)));
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-totem-destroyed-clan", ("area", area)), PopupType.LargeCaution);
    }

    // a Totem deleted without being destroyed just leaves the clan's list: nobody is paid and nothing is revealed
    private void OnTotemTerminating(Entity<BadBloodTotemComponent> ent, ref EntityTerminatingEvent args)
    {
        if (ent.Comp.Destroyed)
            return;

        ent.Comp.Destroyed = true;
        ent.Comp.Registered = false;
        _rule.UnregisterTotem(ent.Owner);
    }

    /// <summary>
    /// SP per minute a Totem of this age pays while it is consecrated.
    /// </summary>
    public float GetIncomePerMinute(BadBloodTotemComponent totem, TimeSpan now)
    {
        var age = now - totem.PlacedAt;
        if (age >= totem.RipeAfter)
            return totem.RipeIncome;

        return age >= totem.GrowingAfter ? totem.GrowingIncome : totem.YoungIncome;
    }

    /// <summary>
    /// A ripe Totem has stood for <see cref="BadBloodTotemComponent.RipeAfter"/>. Deleted, uncounted and
    /// desecrated Totems are never ripe.
    /// </summary>
    public bool IsRipe(EntityUid totem)
    {
        return TryComp(totem, out BadBloodTotemComponent? comp) &&
               comp.Registered &&
               !comp.Desecrated &&
               !TerminatingOrDeleted(totem) &&
               _timing.CurTime - comp.PlacedAt >= comp.RipeAfter;
    }

    /// <summary>
    /// How many of the clan's Totems are ripe right now. The clan ship is called early off this.
    /// </summary>
    public int CountRipeTotems()
    {
        var ripe = 0;
        foreach (var totem in _rule.GetTotems())
        {
            if (IsRipe(totem))
                ripe++;
        }

        return ripe;
    }

    /// <summary>
    /// A living Bad Blood stands within the Totem's contest radius. Only the intel read still asks this: a marine
    /// cannot read a Totem with a hunter beside it.
    /// </summary>
    public bool IsBadBloodNear(Entity<BadBloodTotemComponent> totem)
    {
        var here = _transform.GetMapCoordinates(totem.Owner);
        var range = totem.Comp.ContestRadius * totem.Comp.ContestRadius;

        var badBloods = EntityQueryEnumerator<YautjaBadBloodComponent, TransformComponent>();
        while (badBloods.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != here.MapId || _mobState.IsDead(uid))
                continue;

            if ((_transform.GetWorldPosition(xform) - here.Position).LengthSquared() <= range)
                return true;
        }

        return false;
    }

    private void UpdateTotems(TimeSpan now, TimeSpan elapsed)
    {
        var totems = EntityQueryEnumerator<BadBloodTotemComponent>();
        while (totems.MoveNext(out var uid, out var totem))
        {
            if (!totem.Registered || totem.Destroyed)
                continue;

            var ent = new Entity<BadBloodTotemComponent>(uid, totem);
            if (!totem.Desecrated)
                Accrue(ent, now, elapsed);

            if (!totem.MapRevealed && now - totem.PlacedAt >= totem.MapRevealDelay)
            {
                totem.MapRevealed = true;
                EntityManager.AddComponents(uid, totem.RevealComponents);
                _tacticalMap.RefreshTracked(uid);
            }
        }
    }

    /// <summary>
    /// Desecrates or reconsecrates the Totem at once, raising the broadcast event and telling the clan and the
    /// marines. Does nothing when it is already in that state.
    /// </summary>
    public void SetDesecrated(Entity<BadBloodTotemComponent> totem, bool desecrated)
    {
        var comp = totem.Comp;
        if (comp.Desecrated == desecrated || !comp.Registered || comp.Destroyed)
            return;

        comp.Desecrated = desecrated;

        var area = _area.GetAreaName(totem.Owner);
        if (desecrated)
        {
            var ev = new BadBloodTotemDesecratedEvent(totem.Owner);
            RaiseLocalEvent(ref ev);

            _marineAnnounce.AnnounceToMarines(Loc.GetString("cmu-bad-blood-totem-desecrated-marines", ("area", area)));
            _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-totem-desecrated-clan", ("area", area)), PopupType.LargeCaution);
        }
        else
        {
            var ev = new BadBloodTotemReconsecratedEvent(totem.Owner);
            RaiseLocalEvent(ref ev);

            _marineAnnounce.AnnounceToMarines(Loc.GetString("cmu-bad-blood-totem-reconsecrated-marines", ("area", area)));
            _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-totem-reconsecrated-clan", ("area", area)));
        }
    }

    /// <summary>
    /// Books <paramref name="elapsed"/> of standing time and pays the clan whatever has built up past the payout
    /// step. The once-a-second update calls this for every consecrated Totem. A desecrated Totem books nothing.
    /// </summary>
    public void Accrue(Entity<BadBloodTotemComponent> totem, TimeSpan now, TimeSpan elapsed)
    {
        if (!totem.Comp.Registered || totem.Comp.Desecrated || elapsed <= TimeSpan.Zero)
            return;

        totem.Comp.PendingIncome += GetIncomePerMinute(totem.Comp, now) * elapsed.TotalMinutes;

        var step = totem.Comp.PayoutStep.Double();
        if (step <= 0d || totem.Comp.PendingIncome < step)
            return;

        // whole steps only, the rest waits for the next tick
        var steps = Math.Floor(totem.Comp.PendingIncome / step + 1e-9);
        var payout = totem.Comp.PayoutStep * (long) steps;
        if (!_rule.AddTotemIncome(payout))
            return;

        totem.Comp.PendingIncome = Math.Max(0d, totem.Comp.PendingIncome - payout.Double());

        var wholeBefore = totem.Comp.PaidIncome.Long();
        totem.Comp.PaidIncome += payout;

        // the steps are too small to see, so the Totem speaks up each time it has fed the clan a whole point
        var wholeNow = totem.Comp.PaidIncome.Long();
        if (wholeNow > wholeBefore)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-bad-blood-totem-paid", ("paid", wholeNow)),
                totem.Owner,
                Filter.Empty().AddWhereAttachedEntity(_badBlood.IsBadBlood),
                false,
                PopupType.Medium);

            var ev = new BadBloodTotemPaidEvent(totem.Owner, (int) (wholeNow - wholeBefore));
            RaiseLocalEvent(ref ev);
        }
    }

    /// <summary>
    /// What a Bad Blood reads on a Totem: how long it has stood, what it pays a minute, what it has paid, and
    /// whether it has been desecrated. Null for a Totem the clan does not count.
    /// </summary>
    public string? GetTotemExamineText(Entity<BadBloodTotemComponent> totem)
    {
        if (!totem.Comp.Registered || totem.Comp.Destroyed)
            return null;

        var now = _timing.CurTime;
        var age = now - totem.Comp.PlacedAt;
        var stage = age >= totem.Comp.RipeAfter
            ? "cmu-bad-blood-totem-stage-ripe"
            : age >= totem.Comp.GrowingAfter
                ? "cmu-bad-blood-totem-stage-growing"
                : "cmu-bad-blood-totem-stage-young";

        var text = Loc.GetString("cmu-bad-blood-totem-examine",
            ("stage", Loc.GetString(stage)),
            ("rate", GetIncomePerMinute(totem.Comp, now).ToString("0.00", CultureInfo.InvariantCulture)),
            ("paid", BadBloodCampSystem.FormatSp(totem.Comp.PaidIncome)));

        if (totem.Comp.Desecrated)
            text += "\n" + Loc.GetString("cmu-bad-blood-totem-examine-desecrated");

        return text;
    }

    private void OnTotemExamined(Entity<BadBloodTotemComponent> ent, ref ExaminedEvent args)
    {
        if (_badBlood.IsBadBlood(args.Examiner))
        {
            if (GetTotemExamineText(ent) is { } text)
                args.PushMarkup(text);

            return;
        }

        // marines are told what they can do with it, so nobody has to guess at a mechanic
        if (!IsDesecrator(args.Examiner) || !ent.Comp.Registered || ent.Comp.Destroyed)
            return;

        args.PushMarkup(Loc.GetString(ent.Comp.Desecrated
            ? "cmu-bad-blood-totem-examine-marine-desecrated"
            : "cmu-bad-blood-totem-examine-marine"));
    }

    #endregion

    #region Desecration

    private void OnTotemVerbs(Entity<BadBloodTotemComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Registered || ent.Comp.Destroyed)
            return;

        var user = args.User;
        bool desecrate;
        if (IsDesecrator(user) && !ent.Comp.Desecrated)
            desecrate = true;
        else if (_badBlood.IsBadBlood(user) && ent.Comp.Desecrated)
            desecrate = false;
        else
            return;

        var totem = ent.Owner;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(desecrate ? "cmu-bad-blood-totem-desecrate-verb" : "cmu-bad-blood-totem-reconsecrate-verb"),
            Message = Loc.GetString(desecrate
                ? "cmu-bad-blood-totem-desecrate-verb-message"
                : "cmu-bad-blood-totem-reconsecrate-verb-message"),
            Act = () => TryStartConsecration(user, totem, desecrate),
        });
    }

    // any marine who is not one of the clan may desecrate
    private bool IsDesecrator(EntityUid user)
    {
        return HasComp<MarineComponent>(user) && !_badBlood.IsBadBlood(user);
    }

    /// <summary>
    /// Why <paramref name="user"/> cannot desecrate (<paramref name="desecrate"/> true) or reconsecrate
    /// <paramref name="totem"/> now, as a loc key, or null when they can.
    /// </summary>
    public string? GetConsecrationDenial(EntityUid user, Entity<BadBloodTotemComponent> totem, bool desecrate)
    {
        if (!totem.Comp.Registered || totem.Comp.Destroyed || TerminatingOrDeleted(totem.Owner))
            return "cmu-bad-blood-totem-consecration-gone";

        if (desecrate)
        {
            if (!IsDesecrator(user))
                return "cmu-bad-blood-totem-desecrate-marines-only";

            if (totem.Comp.Desecrated)
                return "cmu-bad-blood-totem-already-desecrated";
        }
        else
        {
            if (!_badBlood.IsBadBlood(user))
                return "cmu-bad-blood-totem-reconsecrate-clan-only";

            if (!totem.Comp.Desecrated)
                return "cmu-bad-blood-totem-not-desecrated";
        }

        return null;
    }

    /// <summary>
    /// Starts the desecrate or reconsecrate do-after. It breaks if the user moves or is hurt, so the other side
    /// stops it by fighting, not by standing nearby. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartConsecration(EntityUid user, EntityUid totem, bool desecrate)
    {
        if (!TryComp(totem, out BadBloodTotemComponent? comp))
            return false;

        if (GetConsecrationDenial(user, (totem, comp), desecrate) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), user, user, PopupType.SmallCaution);
            return false;
        }

        var time = desecrate ? comp.DesecrateTime : comp.ReconsecrateTime;
        var doAfter = new DoAfterArgs(EntityManager, user, time, new BadBloodTotemConsecrationDoAfterEvent(desecrate), totem, totem)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(
            Loc.GetString(desecrate ? "cmu-bad-blood-totem-desecrate-start" : "cmu-bad-blood-totem-reconsecrate-start",
                ("seconds", (int) time.TotalSeconds)),
            user,
            user);
        return true;
    }

    private void OnConsecrationDoAfter(Entity<BadBloodTotemComponent> ent, ref BadBloodTotemConsecrationDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        TryCompleteConsecration(args.User, ent, args.Desecrate);
    }

    /// <summary>
    /// Finishes a desecration or reconsecration at once, checking the conditions again first.
    /// </summary>
    public bool TryCompleteConsecration(EntityUid user, Entity<BadBloodTotemComponent> totem, bool desecrate)
    {
        // the other side can have finished first while this one ran
        if (GetConsecrationDenial(user, totem, desecrate) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), user, user, PopupType.SmallCaution);
            return false;
        }

        SetDesecrated(totem, desecrate);
        return true;
    }

    #endregion

    #region Raising a Totem

    // every Bad Blood can raise a Totem while the clan's rule runs, so every one of them carries the action
    private void SyncBuilders()
    {
        _scratch.Clear();

        if (!_rule.IsRuleActive())
        {
            var holders = EntityQueryEnumerator<BadBloodTotemBuilderComponent>();
            while (holders.MoveNext(out var uid, out _))
            {
                _scratch.Add(uid);
            }

            foreach (var uid in _scratch)
            {
                if (TryComp(uid, out BadBloodTotemBuilderComponent? builder))
                    _actions.RemoveAction(uid, builder.RaiseTotemActionEntity);

                RemComp<BadBloodTotemBuilderComponent>(uid);
            }

            return;
        }

        var badBloods = EntityQueryEnumerator<YautjaBadBloodComponent>();
        while (badBloods.MoveNext(out var uid, out _))
        {
            if (!HasComp<BadBloodTotemBuilderComponent>(uid))
                _scratch.Add(uid);
        }

        foreach (var uid in _scratch)
        {
            var builder = EnsureComp<BadBloodTotemBuilderComponent>(uid);
            _actions.AddAction(uid, ref builder.RaiseTotemActionEntity, builder.RaiseTotemAction);
            Dirty(uid, builder);
        }
    }

    private void OnRaiseTotem(Entity<BadBloodTotemBuilderComponent> ent, ref BadBloodRaiseTotemActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryStartRaisingTotem(ent.Owner);
    }

    /// <summary>
    /// Why <paramref name="user"/> cannot raise a Totem where they stand, as a loc key, or null when they can.
    /// The price is not looked at here.
    /// </summary>
    public string? GetRaiseDenial(EntityUid user, BadBloodTotemBuilderComponent builder)
    {
        if (!_badBlood.IsBadBlood(user) || !_rule.IsRuleActive())
            return "cmu-bad-blood-totem-not-clan";

        if (_rule.GetTotems().Count >= _rule.GetTotemLimit())
            return "cmu-bad-blood-totem-limit";

        var coords = Transform(user).Coordinates;
        _area.CanOrbitalBombard(coords, out var roofed);
        if (roofed)
            return "cmu-bad-blood-totem-roofed";

        var here = _transform.GetMapCoordinates(user);

        if (_rule.GetLodge() is { } lodge && IsWithin(lodge, here, builder.Spacing))
            return "cmu-bad-blood-totem-near-lodge";

        foreach (var totem in _rule.GetTotems())
        {
            if (IsWithin(totem, here, builder.Spacing))
                return "cmu-bad-blood-totem-near-totem";
        }

        var zones = EntityQueryEnumerator<DropshipDestinationComponent>();
        while (zones.MoveNext(out var zone, out _))
        {
            if (IsWithin(zone, here, builder.LandingZoneDistance))
                return "cmu-bad-blood-totem-near-landing-zone";
        }

        return null;
    }

    private bool IsWithin(EntityUid uid, MapCoordinates here, float distance)
    {
        if (distance <= 0f || TerminatingOrDeleted(uid))
            return false;

        var there = _transform.GetMapCoordinates(uid);
        return there.MapId == here.MapId && (there.Position - here.Position).Length() < distance;
    }

    /// <summary>
    /// Whether the clan can pay for a Totem right now: it has a free one, or the SP.
    /// </summary>
    public bool CanAffordTotem()
    {
        return _rule.TryGetRule(out var rule) && (rule.Comp.FreeTotems > 0 || rule.Comp.Sp >= rule.Comp.TotemCost);
    }

    /// <summary>
    /// Starts the build do-after where <paramref name="user"/> stands. They are told why when it is refused.
    /// </summary>
    public bool TryStartRaisingTotem(EntityUid user)
    {
        if (!TryComp(user, out BadBloodTotemBuilderComponent? builder))
            return false;

        if (GetRaiseDenial(user, builder) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!CanAffordTotem())
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-totem-cannot-afford"), user, user, PopupType.SmallCaution);
            return false;
        }

        var doAfter = new DoAfterArgs(EntityManager, user, builder.BuildTime, new BadBloodRaiseTotemDoAfterEvent(), user)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-totem-raising"), user, user);
        return true;
    }

    private void OnRaiseTotemDoAfter(Entity<BadBloodTotemBuilderComponent> ent, ref BadBloodRaiseTotemDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        TryRaiseTotem(ent.Owner, out _);
    }

    /// <summary>
    /// Raises a Totem where <paramref name="user"/> stands, at once: the placement rules and the clan's limit are
    /// checked again, and the clan pays with a free Totem if it has one, with SP otherwise. Nothing is built or
    /// paid when it fails, and the user is told why.
    /// </summary>
    public bool TryRaiseTotem(EntityUid user, out EntityUid totem)
    {
        totem = default;
        if (!TryComp(user, out BadBloodTotemBuilderComponent? builder) || !_rule.TryGetRule(out var rule))
            return false;

        if (GetRaiseDenial(user, builder) is { } denial)
        {
            _popup.PopupEntity(Loc.GetString(denial), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!CanAffordTotem())
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-totem-cannot-afford"), user, user, PopupType.SmallCaution);
            return false;
        }

        // MapInit puts it on the clan's list, or does not if the clan turned out to be at its limit
        var spawned = Spawn(builder.TotemPrototype, Transform(user).Coordinates);
        if (!TryComp(spawned, out BadBloodTotemComponent? comp) || !comp.Registered)
        {
            Del(spawned);
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-totem-limit"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_rule.TryTakeFreeTotem() && !_rule.TrySpendSp(rule.Comp.TotemCost))
        {
            Del(spawned);
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-totem-cannot-afford"), user, user, PopupType.SmallCaution);
            return false;
        }

        totem = spawned;

        var area = _area.GetAreaName(spawned);
        _marineAnnounce.AnnounceToMarines(Loc.GetString("cmu-bad-blood-totem-raised-marines", ("area", area)));
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-totem-raised-clan", ("area", area)));
        return true;
    }

    #endregion

    #region The aura

    private void OnAuraDamaged(Entity<BadBloodTotemAuraComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageIncreased)
            ent.Comp.LastDamagedAt = _timing.CurTime;
    }

    /// <summary>
    /// Whether <paramref name="uid"/> stands in the aura of one of the clan's Totems.
    /// </summary>
    public bool IsInAura(EntityUid uid)
    {
        return HasComp<BadBloodTotemAuraComponent>(uid);
    }

    private void UpdateAuras(TimeSpan now, TimeSpan elapsed)
    {
        _auras.Clear();
        var totems = EntityQueryEnumerator<BadBloodTotemComponent, TransformComponent>();
        while (totems.MoveNext(out _, out var totem, out var xform))
        {
            if (!totem.Registered || totem.Destroyed || totem.Desecrated || totem.AuraRadius <= 0f)
                continue;

            _auras.Add((xform.MapID, _transform.GetWorldPosition(xform), totem.AuraRadius));
        }

        _inAura.Clear();
        if (_auras.Count > 0)
        {
            var badBloods = EntityQueryEnumerator<YautjaBadBloodComponent, TransformComponent>();
            while (badBloods.MoveNext(out var uid, out _, out var xform))
            {
                if (_mobState.IsDead(uid))
                    continue;

                var position = _transform.GetWorldPosition(xform);
                foreach (var (map, center, radius) in _auras)
                {
                    if (map != xform.MapID || (position - center).LengthSquared() > radius * radius)
                        continue;

                    _inAura.Add(uid);
                    break;
                }
            }
        }

        _scratch.Clear();
        var holders = EntityQueryEnumerator<BadBloodTotemAuraComponent>();
        while (holders.MoveNext(out var uid, out var aura))
        {
            if (!_inAura.Remove(uid))
            {
                _scratch.Add(uid);
                continue;
            }

            if (aura.Heal > 0f &&
                now - aura.LastDamagedAt >= aura.HealDelay &&
                _damageable.GetTotalDamage(uid) > FixedPoint2.Zero)
            {
                _damageable.HealEvenly(uid, FixedPoint2.New(-aura.Heal * (float) elapsed.TotalSeconds));
            }
        }

        foreach (var uid in _scratch)
        {
            RemComp<BadBloodTotemAuraComponent>(uid);
            RefreshDrain(uid);
        }

        // what is left walked in since the last update
        foreach (var uid in _inAura)
        {
            var aura = EnsureComp<BadBloodTotemAuraComponent>(uid);
            aura.LastDamagedAt = now;
            RefreshDrain(uid);
        }
    }

    // hunger and thirst only work out their drain again when a threshold is crossed, so nudge them
    private void RefreshDrain(EntityUid uid)
    {
        if (!TryComp(uid, out SatiationComponent? satiation))
            return;

        var ent = new Entity<SatiationComponent>(uid, satiation);
        _satiation.ModifyValue(ent, SatiationSystem.Hunger, 0f);
        _satiation.ModifyValue(ent, SatiationSystem.Thirst, 0f);
    }

    #endregion
}
