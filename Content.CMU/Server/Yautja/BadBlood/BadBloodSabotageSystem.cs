using System.Diagnostics.CodeAnalysis;
using Content.Server.Administration.Logs;
using Content.Server.GameTicking;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// FLOW-509: Bad Bloods sabotage marine objective sites (uplink sites and planet colony reactors). Sabotage breaks the
/// site the way the site's own systems break it, so marines repair it the usual way. A repaired site is immune to
/// sabotage for <see cref="BadBloodVesselStrikeComponent.RepairImmunity"/>. Raises
/// <see cref="BadBloodSiteSabotagedEvent"/>; the Vessel Strike turns a sabotage of its lock site into a delay.
/// docs/GraphAudit-BadBlood-Objectives.md §5.3, §5.3a.
/// </summary>
public sealed class BadBloodSabotageSystem : EntitySystem
{
    private static readonly ProtoId<DamageTypePrototype> CommsDamageType = "Blunt";
    private static readonly TimeSpan DefaultRepairImmunity = TimeSpan.FromSeconds(180);
    private static readonly TimeSpan SweepEvery = TimeSpan.FromSeconds(1);

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodObjectiveChainSystem _chain = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedRMCPowerSystem _power = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private SensorTowerSystem _sensor = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private GameTicker _gameTicker = default!;

    private TimeSpan _nextSweep;
    private readonly List<EntityUid> _scratch = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);

        SubscribeLocalEvent<BadBloodSabotageTargetComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BadBloodSabotageTargetComponent, BadBloodSiteSabotageDoAfterEvent>(OnSabotageDoAfter);
        // the clan cannot just switch a tower off: it has to sabotage it and live with the immunity rules
        SubscribeLocalEvent<BadBloodSabotageTargetComponent, InteractHandEvent>(OnInteractHand,
            before: new[] { typeof(CommunicationsTowerSystem), typeof(SensorTowerSystem) });
        SubscribeLocalEvent<BadBloodSabotageTargetComponent, SensorTowerStateChangedEvent>(OnSensorStateChanged);
        SubscribeLocalEvent<BadBloodSabotageTargetComponent, CommunicationsTowerStateChangedEvent>(OnCommsStateChanged);
        // the immunity holds against plain damage too, or a comms tower or relay could simply be shot down
        SubscribeLocalEvent<BadBloodSabotageTargetComponent, BeforeDamageChangedEvent>(OnBeforeDamageChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _nextSweep = TimeSpan.Zero;
        _scratch.Clear();
    }

    private void OnBeforeDamageChanged(Entity<BadBloodSabotageTargetComponent> ent, ref BeforeDamageChangedEvent args)
    {
        // healing (repairs) always goes through
        if (args.Cancelled || _timing.CurTime >= ent.Comp.ImmuneUntil || !args.Damage.AnyPositive())
            return;

        args.Cancelled = true;
    }

    #region Targets

    private void OnRuleStarted(ref BadBloodRuleStartedEvent ev)
    {
        EnsureTargets();
        _nextSweep = _timing.CurTime + SweepEvery;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;

        _nextSweep = now + SweepEvery;
        // nothing after the round ended. Not gated on InRound: rules started in the lobby (admins, tests) keep running
        if (_gameTicker.RunLevel == GameRunLevel.PostRound || !_rule.IsRuleActive())
            return;

        // towers from spawners, new relays and late reactors become targets too
        EnsureTargets();

        // reactors raise nothing when they are repaired, so the repair immunity is polled for every kind
        var query = EntityQueryEnumerator<BadBloodSabotageTargetComponent>();
        while (query.MoveNext(out var uid, out var target))
        {
            UpdateRepairState((uid, target), now);
        }
    }

    /// <summary>
    /// Makes every uplink site, and every planet sensor tower, comms tower and colony reactor, a sabotage target.
    /// </summary>
    public void EnsureTargets()
    {
        _scratch.Clear();

        var sites = EntityQueryEnumerator<BadBloodUplinkSiteComponent>();
        while (sites.MoveNext(out var uid, out _))
        {
            if (!HasComp<BadBloodSabotageTargetComponent>(uid))
                _scratch.Add(uid);
        }

        var sensors = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        while (sensors.MoveNext(out var uid, out _, out var xform))
        {
            if (!HasComp<BadBloodSabotageTargetComponent>(uid) && _planet.IsOnPlanetLevel(xform))
                _scratch.Add(uid);
        }

        var comms = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();
        while (comms.MoveNext(out var uid, out _, out var xform))
        {
            if (!HasComp<BadBloodSabotageTargetComponent>(uid) && _planet.IsOnPlanetLevel(xform))
                _scratch.Add(uid);
        }

        var reactors = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (reactors.MoveNext(out var uid, out _, out var xform))
        {
            if (!HasComp<BadBloodSabotageTargetComponent>(uid) && _planet.IsOnPlanetLevel(xform))
                _scratch.Add(uid);
        }

        foreach (var uid in _scratch)
        {
            if (!TerminatingOrDeleted(uid))
                EnsureComp<BadBloodSabotageTargetComponent>(uid);
        }

        _scratch.Clear();
    }

    /// <summary>
    /// The site needs repairs: a sensor tower in a repair state, a broken comms tower, a damaged reactor.
    /// A field relay is never "broken", it is destroyed.
    /// </summary>
    public bool IsBroken(EntityUid site)
    {
        if (TryComp(site, out SensorTowerComponent? sensor))
            return sensor.State < SensorTowerState.Off;

        if (TryComp(site, out CommunicationsTowerComponent? comms))
            return comms.State == CommunicationsTowerState.Broken;

        if (TryComp(site, out RMCFusionReactorComponent? reactor))
            return reactor.State != RMCFusionReactorState.Working;

        return false;
    }

    /// <summary>
    /// The site does its job: sensor or comms tower On, reactor Working, a relay the chain counts as working.
    /// </summary>
    public bool IsWorking(EntityUid site)
    {
        if (TryComp(site, out SensorTowerComponent? sensor))
            return sensor.State == SensorTowerState.On;

        if (TryComp(site, out CommunicationsTowerComponent? comms))
            return comms.State == CommunicationsTowerState.On;

        if (TryComp(site, out RMCFusionReactorComponent? reactor))
            return reactor.State == RMCFusionReactorState.Working;

        return _chain.IsSiteWorking(site);
    }

    /// <summary>
    /// Bad Bloods cannot sabotage <paramref name="site"/> for <paramref name="duration"/> from now. Never shortens an
    /// immunity that is already longer.
    /// </summary>
    public void GrantImmunity(EntityUid site, TimeSpan duration)
    {
        var target = EnsureComp<BadBloodSabotageTargetComponent>(site);
        var until = _timing.CurTime + duration;
        if (until > target.ImmuneUntil)
            target.ImmuneUntil = until;
    }

    /// <summary>
    /// The sabotage immunity a repaired or re-routed site gets, from the Vessel Strike tunables.
    /// </summary>
    public TimeSpan GetRepairImmunity()
    {
        return _rule.TryGetRule(out var rule) && TryComp(rule.Owner, out BadBloodVesselStrikeComponent? strike)
            ? strike.RepairImmunity
            : DefaultRepairImmunity;
    }

    private void OnSensorStateChanged(Entity<BadBloodSabotageTargetComponent> ent, ref SensorTowerStateChangedEvent args)
    {
        UpdateRepairState(ent, _timing.CurTime);
    }

    private void OnCommsStateChanged(Entity<BadBloodSabotageTargetComponent> ent, ref CommunicationsTowerStateChangedEvent args)
    {
        UpdateRepairState(ent, _timing.CurTime);
    }

    // a target seen broken waits for its repair; the moment it works again it is immune for a while.
    // switching a working tower off and on again is not a repair
    private void UpdateRepairState(Entity<BadBloodSabotageTargetComponent> ent, TimeSpan now)
    {
        if (IsBroken(ent.Owner))
        {
            ent.Comp.AwaitingRepair = true;
            return;
        }

        if (!ent.Comp.AwaitingRepair || !IsWorking(ent.Owner))
            return;

        ent.Comp.AwaitingRepair = false;
        var immunity = GetRepairImmunity();
        if (now + immunity > ent.Comp.ImmuneUntil)
            ent.Comp.ImmuneUntil = now + immunity;

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: {ToPrettyString(ent.Owner):site} was repaired and cannot be sabotaged for {(int) immunity.TotalSeconds}s");
    }

    #endregion

    #region Sabotage

    private void OnGetVerbs(Entity<BadBloodSabotageTargetComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !_badBlood.IsBadBlood(args.User) || !_rule.IsRuleActive())
            return;

        var user = args.User;
        var site = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cmu-bad-blood-sabotage-verb"),
            Message = Loc.GetString("cmu-bad-blood-sabotage-verb-message"),
            Act = () => TryStartSabotage(user, site),
        });
    }

    /// <summary>
    /// Whether <paramref name="user"/> may sabotage <paramref name="site"/> now. <paramref name="reason"/> is the
    /// localized refusal.
    /// </summary>
    public bool CanSabotage(EntityUid user, EntityUid site, [NotNullWhen(false)] out string? reason)
    {
        reason = null;
        if (!_rule.IsRuleActive() ||
            !TryComp(site, out BadBloodSabotageTargetComponent? target) ||
            TerminatingOrDeleted(site) ||
            EntityManager.IsQueuedForDeletion(site))
        {
            reason = Loc.GetString("cmu-bad-blood-sabotage-not-target");
            return false;
        }

        if (!_badBlood.IsBadBlood(user) || !_mobState.IsAlive(user))
        {
            reason = Loc.GetString("cmu-bad-blood-sabotage-not-clan");
            return false;
        }

        var now = _timing.CurTime;
        if (now < target.ImmuneUntil)
        {
            reason = Loc.GetString("cmu-bad-blood-sabotage-immune",
                ("seconds", (int) Math.Ceiling((target.ImmuneUntil - now).TotalSeconds)));
            return false;
        }

        if (IsBroken(site))
        {
            reason = Loc.GetString("cmu-bad-blood-sabotage-broken");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Starts the sabotage do-after. Tells <paramref name="user"/> why when it is refused.
    /// </summary>
    public bool TryStartSabotage(EntityUid user, EntityUid site)
    {
        if (!CanSabotage(user, site, out var reason))
        {
            _popup.PopupEntity(reason, user, user, PopupType.SmallCaution);
            return false;
        }

        var target = Comp<BadBloodSabotageTargetComponent>(site);
        var doAfter = new DoAfterArgs(EntityManager, user, target.SabotageTime, new BadBloodSiteSabotageDoAfterEvent(),
            site, target: site)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BlockDuplicate = true,
            CancelDuplicate = true,
            DuplicateCondition = DuplicateConditions.SameTarget | DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-sabotage-start", ("site", site)), user, user);
        return true;
    }

    private void OnSabotageDoAfter(Entity<BadBloodSabotageTargetComponent> ent, ref BadBloodSiteSabotageDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        TrySabotage(args.User, ent.Owner);
    }

    /// <summary>
    /// Sabotages <paramref name="site"/> right now, without the do-after: the end of the do-after, and tests.
    /// Sensor tower: breaks to its Wrench repair state. Comms tower: damage past its Breakage threshold. Reactor:
    /// <see cref="BadBloodSabotageTargetComponent.SabotageStages"/> damage stages. Field relay: destroyed.
    /// </summary>
    public bool TrySabotage(EntityUid user, EntityUid site)
    {
        if (!CanSabotage(user, site, out var reason))
        {
            _popup.PopupEntity(reason, user, user, PopupType.SmallCaution);
            return false;
        }

        var target = Comp<BadBloodSabotageTargetComponent>(site);
        string kind;
        var destroy = false;
        if (TryComp(site, out SensorTowerComponent? sensor))
        {
            kind = "sensor tower";
            if (!_sensor.CMUBreak((site, sensor)))
                return false;
        }
        else if (TryComp(site, out CommunicationsTowerComponent? comms))
        {
            kind = "comms tower";
            var damage = new DamageSpecifier(_prototypes.Index(CommsDamageType), FixedPoint2.New(target.CommsDamage));
            _damageable.TryChangeDamage(site, damage, ignoreResistances: true, origin: user);
            if (comms.State != CommunicationsTowerState.Broken)
            {
                Log.Warning($"Bad Blood sabotage dealt {target.CommsDamage} {CommsDamageType} to {ToPrettyString(site)} but it did not break");
                return false;
            }
        }
        else if (TryComp(site, out RMCFusionReactorComponent? reactor))
        {
            kind = "reactor";
            var stages = Math.Max(1, target.SabotageStages);
            for (var i = 0; i < stages && reactor.State != RMCFusionReactorState.Weld; i++)
            {
                _power.DestroyReactor((site, reactor), user);
            }
        }
        else if (HasComp<BadBloodUplinkSiteComponent>(site))
        {
            kind = "field relay";
            destroy = true;
        }
        else
        {
            return false;
        }

        if (!destroy)
            target.AwaitingRepair = true;

        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-sabotage-done", ("site", site)), user, user, PopupType.Medium);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"BadBlood obj: {ToPrettyString(user):player} sabotaged the {kind} {ToPrettyString(site):site}");

        // raised while the site still exists, a destroyed relay is deleted right after
        var ev = new BadBloodSiteSabotagedEvent(site, user);
        RaiseLocalEvent(ref ev);

        if (destroy)
            QueueDel(site);

        return true;
    }

    private void OnInteractHand(Entity<BadBloodSabotageTargetComponent> ent, ref InteractHandEvent args)
    {
        // comms and sensor towers: a Bad Blood hand-toggle would switch them off without the do-after or immunity
        if (args.Handled ||
            !HasComp<CommunicationsTowerComponent>(ent.Owner) && !HasComp<SensorTowerComponent>(ent.Owner) ||
            !_badBlood.IsBadBlood(args.User))
        {
            return;
        }

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("cmu-bad-blood-sabotage-use-verb"), ent.Owner, args.User,
            PopupType.SmallCaution);
    }

    #endregion
}
