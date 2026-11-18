using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Dialog;
using Content.Shared._RMC14.TacticalMap;
using Content.Shared.Actions;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// CMU14 FLOW-703..706, Hunt Orders: the Clan Leader, off-screen, orders the clan to seize Hunt Grounds, and a Clan
/// Totem rises on every ground the clan holds. This replaces the voluntary Raise Totem action (ECS-706).
/// <para>
/// The first order comes <see cref="BadBloodCampRuleComponent.OrderFirstDelay"/> after the first Lodge; a new one
/// whenever open orders + Totems are under the clan's limit and <see cref="BadBloodCampRuleComponent.OrderCooldown"/>
/// has passed. An order lapses after <see cref="BadBloodCampRuleComponent.OrderTimeout"/>. Nothing is issued while no
/// Lodge stands; open grounds keep their timers.
/// </para>
/// <para>
/// FLOW-705: living Bad Bloods alone on a ground fill its meter by their summed contest weight
/// (<see cref="BadBloodTotemSystem.CountNear"/>), marines alone drain it, both together freeze it. Full, the Totem
/// rises (<see cref="BadBloodTotemSystem.SpawnOrderedTotem"/>), the clan gets SP as Totem income, and every Bad Blood
/// seen on the ground in the last <see cref="BadBloodCampRuleComponent.SeizeRewardWindow"/> gets Honour.
/// </para>
/// <para>
/// FLOW-706: the living Hunt Leader picks one of two sites (else the first after
/// <see cref="BadBloodCampRuleComponent.OrderPickTime"/>), and may petition the Clan Leader for a different ground.
/// </para>
/// </summary>
public sealed partial class BadBloodHuntOrderSystem : EntitySystem
{
    private static readonly TimeSpan UpdateEvery = TimeSpan.FromSeconds(1);

    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private DialogSystem _dialog = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SharedTacticalMapSystem _tacticalMap = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private BadBloodTotemSystem _totems = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    private TimeSpan _nextUpdate;
    private readonly List<EntityUid> _grounds = new();
    private readonly List<EntityUid> _marines = new();
    private readonly List<EntityUid> _present = new();
    private readonly List<EntityUid> _scratch = new();
    private readonly List<MapCoordinates> _sites = new();

    // instrumentation for the tests
    public int OrdersIssuedCount { get; private set; }
    public int OrdersSeizedCount { get; private set; }
    public int OrdersLapsedCount { get; private set; }
    public int PicksOffered { get; private set; }
    public int PetitionsGranted { get; private set; }
    public int MarinePopupsSent { get; private set; }

    // CMU14 FLOW-711: sounds played (only counted when the clip is set)
    public int SeizeStartSoundsPlayed { get; private set; }
    public int SeizePulseSoundsPlayed { get; private set; }
    public int ContestedSoundsPlayed { get; private set; }
    public int RisenSoundsPlayed { get; private set; }

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodHuntGroundComponent, ExaminedEvent>(OnGroundExamined);
        SubscribeLocalEvent<BadBloodHuntPetitionComponent, BadBloodPetitionClanLeaderActionEvent>(OnPetitionAction);

        SubscribeLocalEvent<BadBloodLeaderChangedEvent>(OnLeaderChanged);
        SubscribeLocalEvent<BadBloodHuntOrderPickedEvent>(OnPicked);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextUpdate)
            return;

        _nextUpdate = now + UpdateEvery;

        if (!_rule.TryGetRule(out var rule))
            return;

        UpdateGrounds(rule, now, UpdateEvery);
        UpdatePick(rule, now);
        UpdateOrders(rule, now);
    }

    #region Orders

    /// <summary>
    /// Open orders, the pending pick and standing Totems: what counts against the clan's Totem limit.
    /// </summary>
    public int CountActive()
    {
        return _rule.TryGetRule(out var rule) ? CountActive(rule) : 0;
    }

    private int CountActive(Entity<BadBloodCampRuleComponent> rule)
    {
        rule.Comp.HuntOrders.RemoveAll(ground => TerminatingOrDeleted(ground));
        return rule.Comp.HuntOrders.Count + (rule.Comp.PendingPick != null ? 1 : 0) + _rule.GetTotems().Count;
    }

    /// <summary>
    /// The open Hunt Grounds.
    /// </summary>
    public IReadOnlyList<EntityUid> GetOpenOrders()
    {
        if (!_rule.TryGetRule(out var rule))
            return Array.Empty<EntityUid>();

        rule.Comp.HuntOrders.RemoveAll(ground => TerminatingOrDeleted(ground));
        return rule.Comp.HuntOrders;
    }

    private void UpdateOrders(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        var comp = rule.Comp;

        // a slot freed (a Totem fell, an order lapsed): the next order waits the cooldown from now
        var active = CountActive(rule);
        if (active < comp.LastActiveCount && comp.NextOrderAt is { } next && next < now + comp.OrderCooldown)
            comp.NextOrderAt = now + comp.OrderCooldown;

        comp.LastActiveCount = active;

        if (!comp.HuntOrdersEnabled || comp.PendingPick != null)
            return;

        // no Lodge, no orders. Open grounds keep their timers
        if (_rule.GetLodge() is not { } lodge)
            return;

        if (comp.NextOrderAt == null)
        {
            comp.NextOrderAt = now + comp.OrderFirstDelay;
            return;
        }

        if (now < comp.NextOrderAt || active >= _rule.GetTotemLimit())
            return;

        StartOrder(rule, lodge, now);
        comp.LastActiveCount = CountActive(rule);
    }

    private void StartOrder(Entity<BadBloodCampRuleComponent> rule, EntityUid lodge, TimeSpan now)
    {
        var comp = rule.Comp;
        var near = comp.OrdersIssued == 0;
        var center = _transform.GetMapCoordinates(lodge);
        if (!TryPickSites(rule, center, near, _sites))
        {
            comp.NextOrderAt = now + comp.OrderRetryDelay;
            Log.Info($"Bad Blood Hunt Order: no site found around the Lodge at {center}, trying again in {comp.OrderRetryDelay.TotalSeconds} s");
            return;
        }

        comp.NextOrderAt = now + comp.OrderCooldown;

        if (_sites.Count >= 2 &&
            _rule.HasLivingLeader() &&
            _rule.GetLeader() is { } leader &&
            !_mobState.IsDead(leader))
        {
            OfferPick(rule, leader, center, near, now);
            return;
        }

        IssueOrderAt(_sites[0], near);
    }

    /// <summary>
    /// Issues a Hunt Order at <paramref name="where"/> at once: spawns the Hunt Ground, tells the clan and raises
    /// <see cref="BadBloodHuntOrderIssuedEvent"/>. The site is not checked here. Null without a rule or when open
    /// orders and Totems are already at the clan's limit.
    /// </summary>
    public EntityUid? IssueOrderAt(MapCoordinates where, bool near = false)
    {
        if (!_rule.TryGetRule(out var rule) ||
            where.MapId == MapId.Nullspace ||
            !_map.MapExists(where.MapId))
        {
            return null;
        }

        var comp = rule.Comp;
        if (CountActive(rule) >= _rule.GetTotemLimit())
            return null;

        var ground = Spawn(comp.HuntGroundPrototype, where);
        if (!TryComp(ground, out BadBloodHuntGroundComponent? hunt))
        {
            Del(ground);
            return null;
        }

        var now = _timing.CurTime;
        comp.OrdersIssued++;
        hunt.Order = comp.OrdersIssued;
        hunt.Near = near;
        hunt.IssuedAt = now;
        hunt.ExpiresAt = now + comp.OrderTimeout;
        hunt.SeizeTime = comp.SeizeTime;
        comp.HuntOrders.Add(ground);

        var area = _area.GetAreaName(ground);
        comp.RecentOrderAreas[area] = now;
        OrdersIssuedCount++;
        RefreshLight((ground, hunt));

        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-hunt-order-issued-clan",
                ("area", area),
                ("minutes", (int) Math.Ceiling(comp.OrderTimeout.TotalMinutes))),
            PopupType.Medium);

        var ev = new BadBloodHuntOrderIssuedEvent(ground);
        RaiseLocalEvent(ref ev);
        return ground;
    }

    #endregion

    #region The Hunt Ground

    private void UpdateGrounds(Entity<BadBloodCampRuleComponent> rule, TimeSpan now, TimeSpan elapsed)
    {
        // FLOW-604: no pin before the drop, then MapRevealDelay from the later of the order and the landing
        TimeSpan? landedAt = _rule.HaveMarinesLanded() ? _rule.MarinesLandedAt() ?? TimeSpan.Zero : null;

        _grounds.Clear();
        _grounds.AddRange(rule.Comp.HuntOrders);
        foreach (var ground in _grounds)
        {
            if (TerminatingOrDeleted(ground) || !TryComp(ground, out BadBloodHuntGroundComponent? hunt))
            {
                rule.Comp.HuntOrders.Remove(ground);
                continue;
            }

            UpdateGround(rule, (ground, hunt), now, elapsed, landedAt);
        }
    }

    private void UpdateGround(
        Entity<BadBloodCampRuleComponent> rule,
        Entity<BadBloodHuntGroundComponent> ground,
        TimeSpan now,
        TimeSpan elapsed,
        TimeSpan? landedAt)
    {
        var hunt = ground.Comp;
        var here = _transform.GetMapCoordinates(ground.Owner);
        _totems.CountNear(here, hunt.Radius, out var badBloods, out var weight, _marines);

        if (badBloods > 0)
        {
            GetBadBloodsNear(here, hunt.Radius, _present);
            foreach (var present in _present)
            {
                hunt.LastPresent[present] = now;
            }
        }

        var marines = _marines.Count;
        var contested = badBloods > 0 && marines > 0;
        var filling = badBloods > 0 && marines == 0;

        if (contested && !hunt.Contested)
            StartContest(ground, now);

        hunt.Contested = contested;

        var seconds = hunt.SeizeTime.TotalSeconds;
        var step = seconds <= 0d ? 1f : (float) (elapsed.TotalSeconds / seconds);
        if (filling)
        {
            if (!hunt.Filling)
            {
                if (Play(hunt.SeizeStartSound, ground.Owner))
                    SeizeStartSoundsPlayed++;

                hunt.NextPulseAt = now + hunt.SeizePulseInterval;
            }
            else if (now >= hunt.NextPulseAt)
            {
                if (Play(hunt.SeizePulseSound, ground.Owner))
                    SeizePulseSoundsPlayed++;

                hunt.NextPulseAt = now + hunt.SeizePulseInterval;
            }

            hunt.Progress = Math.Min(1f, hunt.Progress + Math.Max(weight, 0f) * step);
        }
        else if (badBloods == 0 && marines > 0)
        {
            hunt.Progress = Math.Max(0f, hunt.Progress - Math.Max(hunt.DrainMultiplier, 0f) * step);
        }

        hunt.Filling = filling;

        // marines who step onto it are told what it is, once per ground
        foreach (var marine in _marines)
        {
            if (!hunt.MarinesWarned.Add(marine))
                continue;

            MarinePopupsSent++;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-hunt-ground-marine-enter"), ground.Owner, marine, PopupType.Medium);
        }

        if (!hunt.MapRevealed &&
            landedAt is { } landed &&
            now - (hunt.IssuedAt > landed ? hunt.IssuedAt : landed) >= hunt.MapRevealDelay)
        {
            hunt.MapRevealed = true;
            EntityManager.AddComponents(ground.Owner, hunt.RevealComponents);
            _tacticalMap.RefreshTracked(ground.Owner);
        }

        if (hunt.Progress >= 1f)
        {
            TrySeize(rule, ground, here, now);
            return;
        }

        // a Bad Blood still on it holds the order open
        if (now >= hunt.ExpiresAt && badBloods == 0)
        {
            Lapse(rule, ground);
            return;
        }

        RefreshLight(ground);
    }

    private void StartContest(Entity<BadBloodHuntGroundComponent> ground, TimeSpan now)
    {
        var hunt = ground.Comp;
        if (Play(hunt.ContestedSound, ground.Owner))
            ContestedSoundsPlayed++;

        if (hunt.LastContestAnnouncedAt is { } last && now - last < hunt.ContestAnnounceCooldown)
            return;

        hunt.LastContestAnnouncedAt = now;
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-hunt-order-contested-clan", ("area", _area.GetAreaName(ground.Owner))),
            PopupType.MediumCaution);
    }

    private void TrySeize(
        Entity<BadBloodCampRuleComponent> rule,
        Entity<BadBloodHuntGroundComponent> ground,
        MapCoordinates here,
        TimeSpan now)
    {
        var comp = rule.Comp;
        var hunt = ground.Comp;
        if (hunt.Seized)
            return;

        // at the limit (a Totem placed by other means): stay full and try again next tick
        if (_totems.SpawnOrderedTotem(here) is not { } totem)
        {
            hunt.Progress = 1f;
            RefreshLight(ground);
            return;
        }

        hunt.Seized = true;
        comp.HuntOrders.Remove(ground.Owner);
        OrdersSeizedCount++;

        _rule.AddTotemIncome(comp.SeizeRewardSp);

        var honoured = 0;
        foreach (var (uid, last) in hunt.LastPresent)
        {
            if (now - last > comp.SeizeRewardWindow || TerminatingOrDeleted(uid) || !_badBlood.IsBadBlood(uid) ||
                _mobState.IsDead(uid))
                continue;

            if (!_rule.AddHonour(uid, comp.SeizeRewardHonour))
                continue;

            honoured++;
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-hunt-order-seized-honour",
                    ("honour", BadBloodCampSystem.FormatSp(comp.SeizeRewardHonour))),
                uid,
                uid,
                PopupType.Medium);
        }

        if (Play(hunt.RisenSound, totem))
            RisenSoundsPlayed++;

        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-hunt-order-seized-clan",
            ("area", _area.GetAreaName(totem)),
            ("sp", BadBloodCampSystem.FormatSp(comp.SeizeRewardSp)),
            ("hunters", honoured)));

        var ev = new BadBloodHuntOrderSeizedEvent(ground.Owner, totem);
        RaiseLocalEvent(ref ev);

        Del(ground.Owner);
    }

    private void Lapse(Entity<BadBloodCampRuleComponent> rule, Entity<BadBloodHuntGroundComponent> ground)
    {
        rule.Comp.HuntOrders.Remove(ground.Owner);
        OrdersLapsedCount++;

        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-hunt-order-lapsed-clan", ("area", _area.GetAreaName(ground.Owner))),
            PopupType.MediumCaution);

        var ev = new BadBloodHuntOrderLapsedEvent(ground.Owner);
        RaiseLocalEvent(ref ev);

        Del(ground.Owner);
    }

    /// <summary>
    /// Fills <paramref name="found"/> with the living Bad Bloods within <paramref name="radius"/> of
    /// <paramref name="here"/>: the same people <see cref="BadBloodTotemSystem.CountNear"/> counts.
    /// </summary>
    public void GetBadBloodsNear(MapCoordinates here, float radius, List<EntityUid> found)
    {
        found.Clear();
        if (here.MapId == MapId.Nullspace)
            return;

        var range = radius * radius;
        var query = EntityQueryEnumerator<YautjaBadBloodComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapID != here.MapId || _mobState.IsDead(uid))
                continue;

            if ((_transform.GetWorldPosition(xform) - here.Position).LengthSquared() <= range)
                found.Add(uid);
        }
    }

    // steady and dim while idle, low while contested; while filling it blinks, faster as the meter fills
    private void RefreshLight(Entity<BadBloodHuntGroundComponent> ground)
    {
        if (!_pointLight.TryGetLight(ground.Owner, out var light))
            return;

        var hunt = ground.Comp;
        float energy;
        if (hunt.Contested)
        {
            energy = hunt.ContestedLightEnergy;
        }
        else if (hunt.Filling)
        {
            hunt.PulseTick++;
            var period = hunt.Progress < 1f / 3f ? 3 : hunt.Progress < 2f / 3f ? 2 : 1;
            energy = hunt.PulseTick / period % 2 == 0 ? hunt.PulseLightEnergy : hunt.IdleLightEnergy;
        }
        else
        {
            energy = hunt.IdleLightEnergy;
        }

        _pointLight.SetEnabled(ground.Owner, true, light);
        _pointLight.SetEnergy(ground.Owner, energy, light);
    }

    private bool Play(SoundSpecifier? sound, EntityUid at)
    {
        if (sound == null)
            return false;

        _audio.PlayPvs(sound, at);
        return true;
    }

    private void OnGroundExamined(Entity<BadBloodHuntGroundComponent> ent, ref ExaminedEvent args)
    {
        var percent = (int) Math.Round(Math.Clamp(ent.Comp.Progress, 0f, 1f) * 100f);
        if (_badBlood.IsBadBlood(args.Examiner))
        {
            var left = ent.Comp.ExpiresAt - _timing.CurTime;
            args.PushMarkup(Loc.GetString("cmu-bad-blood-hunt-ground-examine-clan",
                ("percent", percent),
                ("minutes", Math.Max(0, (int) Math.Ceiling(left.TotalMinutes)))));

            if (ent.Comp.Contested)
                args.PushMarkup(Loc.GetString("cmu-bad-blood-hunt-ground-examine-contested"));

            return;
        }

        args.PushMarkup(Loc.GetString("cmu-bad-blood-hunt-ground-examine-marine", ("percent", percent)));
    }

    #endregion

    #region The Hunt Leader

    private void OfferPick(
        Entity<BadBloodCampRuleComponent> rule,
        EntityUid leader,
        MapCoordinates center,
        bool near,
        TimeSpan now)
    {
        var comp = rule.Comp;
        var pick = new BadBloodHuntOrderPick
        {
            Id = ++comp.LastPickId,
            Leader = leader,
            Near = near,
            DecideAt = now + comp.OrderPickTime,
        };
        pick.Sites.AddRange(_sites);
        comp.PendingPick = pick;
        PicksOffered++;

        var options = new List<DialogOption>();
        for (var i = 0; i < pick.Sites.Count; i++)
        {
            var site = pick.Sites[i];
            options.Add(new DialogOption(
                Loc.GetString("cmu-bad-blood-hunt-order-pick-option",
                    ("area", GetAreaName(site)),
                    ("distance", (int) (site.Position - center.Position).Length())),
                new BadBloodHuntOrderPickedEvent(GetNetEntity(leader), pick.Id, i)));
        }

        var seconds = (int) comp.OrderPickTime.TotalSeconds;
        _dialog.OpenOptions(
            leader,
            Loc.GetString("cmu-bad-blood-hunt-order-pick-title"),
            options,
            Loc.GetString("cmu-bad-blood-hunt-order-pick-message", ("seconds", seconds)),
            timeout: comp.OrderPickTime);

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-hunt-order-pick-popup", ("seconds", seconds)),
            leader,
            leader,
            PopupType.Medium);
    }

    // the Hunt Leader did not choose in time, or is gone: the first site is taken
    private void UpdatePick(Entity<BadBloodCampRuleComponent> rule, TimeSpan now)
    {
        if (rule.Comp.PendingPick is not { } pick)
            return;

        if (now < pick.DecideAt && !TerminatingOrDeleted(pick.Leader) && !_mobState.IsDead(pick.Leader))
            return;

        ResolvePick(rule, 0, now);
    }

    /// <summary>
    /// The Hunt Leader <paramref name="user"/> takes site <paramref name="index"/> of offer <paramref name="pick"/>.
    /// False when that offer is no longer open to them.
    /// </summary>
    public bool ChooseSite(EntityUid user, int pick, int index)
    {
        if (!_rule.TryGetRule(out var rule) ||
            rule.Comp.PendingPick is not { } pending ||
            pending.Id != pick ||
            pending.Leader != user ||
            rule.Comp.Leader != user || // a deposed Hunt Leader cannot answer a pick still open
            index < 0 ||
            index >= pending.Sites.Count)
        {
            return false;
        }

        var where = GetAreaName(pending.Sites[index]);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: {ToPrettyString(user):player} chose Hunt Ground {index + 1} of {pending.Sites.Count}, at {where}");
        ResolvePick(rule, index, _timing.CurTime);
        return true;
    }

    private void OnPicked(BadBloodHuntOrderPickedEvent ev)
    {
        if (TryGetEntity(ev.User, out var user) && !TerminatingOrDeleted(user))
            ChooseSite(user.Value, ev.Pick, ev.Index);
    }

    // the chosen site, or the other if the chosen one went bad while the Hunt Leader thought about it
    private void ResolvePick(Entity<BadBloodCampRuleComponent> rule, int index, TimeSpan now)
    {
        var comp = rule.Comp;
        if (comp.PendingPick is not { } pick)
            return;

        comp.PendingPick = null;
        for (var i = 0; i < pick.Sites.Count; i++)
        {
            var site = pick.Sites[i == 0 ? index : i == index ? 0 : i];
            if (_totems.GetSiteDenial(site, comp.OrderSpacing, comp.OrderLandingZoneDistance) != null ||
                IsNearClaim(rule, site, comp.OrderSpacing))
            {
                continue;
            }

            if (IssueOrderAt(site, pick.Near) != null)
                return;
        }

        // the pick's slot is given back without starting the freed-slot cooldown: this is a retry
        comp.NextOrderAt = now + comp.OrderRetryDelay;
        comp.LastActiveCount = CountActive(rule);
        Log.Info("Bad Blood Hunt Order: both offered sites went bad before the order was issued, trying again");
    }

    private void OnLeaderChanged(ref BadBloodLeaderChangedEvent ev)
    {
        // only the Hunt Leader petitions the Clan Leader
        _scratch.Clear();
        var holders = EntityQueryEnumerator<BadBloodHuntPetitionComponent>();
        while (holders.MoveNext(out var holder, out _))
        {
            if (holder != ev.New)
                _scratch.Add(holder);
        }

        foreach (var holder in _scratch)
        {
            if (TryComp(holder, out BadBloodHuntPetitionComponent? held))
                _actions.RemoveAction(holder, held.ActionEntity);

            RemComp<BadBloodHuntPetitionComponent>(holder);
        }

        if (ev.New is not { } leader || TerminatingOrDeleted(leader))
            return;

        var petition = EnsureComp<BadBloodHuntPetitionComponent>(leader);
        _actions.AddAction(leader, ref petition.ActionEntity, petition.Action);
        Dirty(leader, petition);
    }

    private void OnPetitionAction(Entity<BadBloodHuntPetitionComponent> ent, ref BadBloodPetitionClanLeaderActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryPetition(ent.Owner);
    }

    /// <summary>
    /// The Hunt Leader <paramref name="user"/> petitions the Clan Leader: the first open order the clan has not
    /// started on is replaced by a fresh ground, for <see cref="BadBloodCampRuleComponent.TotemCost"/> SP, once per
    /// <see cref="BadBloodCampRuleComponent.RerollCooldown"/>. Nothing is paid when it is refused, and the user is
    /// told why.
    /// </summary>
    public bool TryPetition(EntityUid user)
    {
        if (!_rule.TryGetRule(out var rule))
            return false;

        var comp = rule.Comp;
        var now = _timing.CurTime;
        EntityUid? target = null;
        string? denial = null;
        if (comp.Leader != user || !_rule.HasLivingLeader())
            denial = Loc.GetString("cmu-bad-blood-hunt-order-petition-not-leader");
        else if (now < comp.NextRerollAt)
            denial = Loc.GetString("cmu-bad-blood-hunt-order-petition-cooldown",
                ("seconds", (int) Math.Ceiling((comp.NextRerollAt - now).TotalSeconds)));
        else if ((target = FindPetitionTarget(rule)) == null)
            denial = Loc.GetString("cmu-bad-blood-hunt-order-petition-no-order");
        else if (comp.Sp < comp.TotemCost)
            denial = Loc.GetString("cmu-bad-blood-hunt-order-petition-cannot-afford",
                ("cost", BadBloodCampSystem.FormatSp(comp.TotemCost)));
        else if (_rule.GetLodge() == null)
            denial = Loc.GetString("cmu-bad-blood-hunt-order-petition-no-lodge");

        if (denial != null || target is not { } old || _rule.GetLodge() is not { } lodge)
        {
            if (denial != null)
                _popup.PopupEntity(denial, user, user, PopupType.SmallCaution);

            return false;
        }

        var near = TryComp(old, out BadBloodHuntGroundComponent? oldHunt) && oldHunt.Near;

        // the old ground still stands while the new one is picked, so the new one keeps its distance from it
        if (!TryPickSites(rule, _transform.GetMapCoordinates(lodge), near, _sites, 1))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-hunt-order-petition-no-site"), user, user, PopupType.SmallCaution);
            return false;
        }

        if (!_rule.TrySpendSp(comp.TotemCost))
        {
            _popup.PopupEntity(Loc.GetString("cmu-bad-blood-hunt-order-petition-cannot-afford",
                    ("cost", BadBloodCampSystem.FormatSp(comp.TotemCost))),
                user,
                user,
                PopupType.SmallCaution);
            return false;
        }

        comp.NextRerollAt = now + comp.RerollCooldown;
        PetitionsGranted++;

        var oldArea = _area.GetAreaName(old);
        comp.HuntOrders.Remove(old);
        Del(old);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: {ToPrettyString(user):player} petitioned the Clan Leader for a new Hunt Ground in place of {oldArea}");
        _rule.AnnounceToClan(Loc.GetString("cmu-bad-blood-hunt-order-petition-granted-clan", ("area", oldArea)));

        IssueOrderAt(_sites[0], near);
        return true;
    }

    // the first open order nobody has started on
    private EntityUid? FindPetitionTarget(Entity<BadBloodCampRuleComponent> rule)
    {
        foreach (var ground in rule.Comp.HuntOrders)
        {
            if (TerminatingOrDeleted(ground) || !TryComp(ground, out BadBloodHuntGroundComponent? hunt))
                continue;

            if (hunt.Progress <= 0f && !hunt.Contested && !hunt.Filling)
                return ground;
        }

        return null;
    }

    #endregion
}
