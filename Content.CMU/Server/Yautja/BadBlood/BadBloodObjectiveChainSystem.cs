using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Communications;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared._RMC14.Power;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Sensor;
using Content.Shared.CMU14.Yautja;
using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Yautja.BadBlood;

/// <summary>
/// ARCH-503/504, FLOW-511: the marine objective chain against the Bad Blood clan
/// (docs/GraphAudit-BadBlood-Objectives.md §5.3, §5.3a). Infrastructure gate (any <see
/// cref="BadBloodObjectiveChainComponent.GatePartsRequired"/> of power, comms, sensor), the clan signature (bracer,
/// Totem or Lodge) and the uplink at any working uplink site. Every step latches and every timer pauses instead of
/// resetting. State lives on the rule entity (<see cref="BadBloodObjectiveChainComponent"/>).
/// <para>
/// The core file holds the public API, the tick and the infrastructure gate. Uplink sites and the uplink are in
/// .Uplink.cs, the three signature avenues in .Signature.cs.
/// </para>
/// </summary>
public sealed partial class BadBloodObjectiveChainSystem : EntitySystem
{
    /// <summary>
    /// The IFF faction a sensor tower must be set to for the sensor part of the gate. Marines on Bad Blood rounds are
    /// GOVFOR (<c>AU14IDCardGOVFOR</c> carries <c>ItemIFF: GOVFOR</c>) and switching a tower on sets its faction to
    /// the operator's IFF faction (SensorTowerSystem). A tower that is On with no faction does not feed the GOVFOR
    /// tactical map (SensorTowerSystem.HasOnlineSensorForFaction), so it does not count either.
    /// </summary>
    public const string GovforIffFaction = "GOVFOR";

    /// <summary>
    /// The announcement faction for the marine side (SharedMarineAnnounceSystem lower-cases it anyway).
    /// </summary>
    public const string GovforAnnounceFaction = "govfor";

    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private BadBloodSystem _badBlood = default!;
    [Dependency] private BadBloodCampSystem _camp = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private RMCPlanetSystem _planet = default!;
    [Dependency] private SharedPointLightSystem _pointLight = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private BadBloodFieldRelaySystem _relay = default!;
    [Dependency] private BadBloodCampRuleSystem _rule = default!;
    [Dependency] private YautjaSelfDestructSystem _selfDestruct = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedToolSystem _tool = default!;

    private readonly List<(EntityUid Uid, BadBloodUplinkSiteKind Kind)> _siteScratch = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodRuleStartedEvent>(OnRuleStarted);

        InitializeUplink();
        InitializeSignature();
    }

    #region Public API

    /// <summary>
    /// The running Bad Blood rule's objective chain. False on rounds without the rule.
    /// </summary>
    public bool TryGetChain(out Entity<BadBloodObjectiveChainComponent> chain)
    {
        chain = default;
        if (!_rule.TryGetRule(out var rule) || !TryComp(rule.Owner, out BadBloodObjectiveChainComponent? comp))
            return false;

        chain = (rule.Owner, comp);
        return true;
    }

    /// <summary>
    /// Whether the uplink site can be used now: a sensor tower that is On, a comms tower that is On, or a field relay
    /// that is anchored and has charge left. False for anything that is not an uplink site.
    /// </summary>
    public bool IsSiteWorking(EntityUid site)
    {
        if (!TryComp(site, out BadBloodUplinkSiteComponent? comp) || TerminatingOrDeleted(site))
            return false;

        return comp.Kind switch
        {
            BadBloodUplinkSiteKind.Sensor => TryComp(site, out SensorTowerComponent? sensor) &&
                                             sensor.State == SensorTowerState.On,
            BadBloodUplinkSiteKind.Comms => TryComp(site, out CommunicationsTowerComponent? comms) &&
                                            comms.State == CommunicationsTowerState.On,
            BadBloodUplinkSiteKind.Relay => _relay.IsRelayWorking(site),
            _ => false,
        };
    }

    public bool IsUplinked()
    {
        return TryGetChain(out var chain) && chain.Comp.Uplinked;
    }

    public EntityUid? GetLockSite()
    {
        return TryGetChain(out var chain) ? chain.Comp.LockSite : null;
    }

    /// <summary>
    /// Moves the Vessel Strike lock to another site. Raises nothing: the caller (the re-route) reports it.
    /// </summary>
    public void SetLockSite(EntityUid site)
    {
        if (TryGetChain(out var chain))
            chain.Comp.LockSite = site;
    }

    /// <summary>
    /// Every uplink site that is working right now.
    /// </summary>
    public IEnumerable<EntityUid> GetWorkingSites()
    {
        var sites = new List<EntityUid>();
        var query = EntityQueryEnumerator<BadBloodUplinkSiteComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (IsSiteWorking(uid))
                sites.Add(uid);
        }

        return sites;
    }

    #endregion

    #region Rule start and the tick

    private void OnRuleStarted(ref BadBloodRuleStartedEvent ev)
    {
        if (!TryComp(ev.Rule.Owner, out BadBloodObjectiveChainComponent? comp))
            return;

        comp.NextTick = _timing.CurTime;
        SweepSites();
        WarnIfGateUnreachable((ev.Rule.Owner, comp));
    }

    public override void Update(float frameTime)
    {
        if (!TryGetChain(out var chain))
            return;

        var now = _timing.CurTime;
        if (now < chain.Comp.NextTick)
            return;

        chain.Comp.NextTick = now + chain.Comp.Tick;
        var elapsed = chain.Comp.Tick;

        // towers can be spawned after the rule started (comms tower spawners, admins), so every tick makes sure each
        // planet tower is a site. A sweep instead of ComponentStartup keeps clear of the RMC tower subscriptions
        SweepSites();
        TickGate(chain, elapsed);
    }

    #endregion

    #region Infrastructure gate

    private void TickGate(Entity<BadBloodObjectiveChainComponent> chain, TimeSpan elapsed)
    {
        var comp = chain.Comp;
        if (comp.GateMet)
            return;

        // A1 power: any planet reactor running. The hold pauses while none runs and is never reset
        if (!comp.PowerMet && TryFindWorkingReactor(out var reactor))
        {
            comp.ReactorHeld += elapsed;
            if (comp.ReactorHeld >= comp.ReactorHold)
            {
                comp.PowerMet = true;
                LatchGatePart(chain, "Power", reactor);
            }
        }

        // A2 comms: any planet comms tower On
        if (!comp.CommsMet && TryFindPlanetCommsOn(out var comms))
        {
            comp.CommsMet = true;
            LatchGatePart(chain, "Comms", comms);
        }

        // A3 sensor: any planet sensor tower On for GOVFOR
        if (!comp.SensorMet && TryFindPlanetSensorOn(out var sensor))
        {
            comp.SensorMet = true;
            LatchGatePart(chain, "Sensor", sensor);
        }

        if (CountGateParts(comp) < comp.GatePartsRequired)
            return;

        comp.GateMet = true;
        var parts = DescribeGateParts(comp);
        var stage = new BadBloodObjectiveStageEvent(BadBloodObjectiveStage.GateMet, parts);
        RaiseLocalEvent(ref stage);

        _marineAnnounce.AnnounceToMarines(Loc.GetString("cmu-bad-blood-obj-gate-met"), faction: GovforAnnounceFaction);
        _adminLog.Add(LogType.Action, LogImpact.Medium, $"BadBlood obj: infrastructure gate met ({parts})");
    }

    private void LatchGatePart(Entity<BadBloodObjectiveChainComponent> chain, string part, EntityUid site)
    {
        var stage = new BadBloodObjectiveStageEvent(BadBloodObjectiveStage.GatePart, part, site);
        RaiseLocalEvent(ref stage);

        var have = CountGateParts(chain.Comp);
        var need = chain.Comp.GatePartsRequired;
        _marineAnnounce.AnnounceToMarines(
            Loc.GetString("cmu-bad-blood-obj-gate-part",
                ("part", Loc.GetString($"cmu-bad-blood-obj-part-{part.ToLowerInvariant()}")),
                ("have", Math.Min(have, need)),
                ("need", need)),
            faction: GovforAnnounceFaction);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"BadBlood obj: gate part {part} met at {ToPrettyString(site):site} ({have}/{need})");
    }

    private static int CountGateParts(BadBloodObjectiveChainComponent comp)
    {
        return (comp.PowerMet ? 1 : 0) + (comp.CommsMet ? 1 : 0) + (comp.SensorMet ? 1 : 0);
    }

    private static string DescribeGateParts(BadBloodObjectiveChainComponent comp)
    {
        var parts = new List<string>(3);
        if (comp.PowerMet)
            parts.Add("Power");
        if (comp.CommsMet)
            parts.Add("Comms");
        if (comp.SensorMet)
            parts.Add("Sensor");

        return string.Join("+", parts);
    }

    private bool TryFindWorkingReactor(out EntityUid reactor)
    {
        var query = EntityQueryEnumerator<RMCFusionReactorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != RMCFusionReactorState.Working || !_planet.IsOnPlanetLevel(xform))
                continue;

            reactor = uid;
            return true;
        }

        reactor = default;
        return false;
    }

    private bool TryFindPlanetCommsOn(out EntityUid tower)
    {
        var query = EntityQueryEnumerator<CommunicationsTowerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != CommunicationsTowerState.On || !_planet.IsOnPlanetLevel(xform))
                continue;

            tower = uid;
            return true;
        }

        tower = default;
        return false;
    }

    private bool TryFindPlanetSensorOn(out EntityUid tower)
    {
        var query = EntityQueryEnumerator<SensorTowerComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (comp.State != SensorTowerState.On ||
                !string.Equals(comp.Faction, GovforIffFaction, StringComparison.OrdinalIgnoreCase) ||
                !_planet.IsOnPlanetLevel(xform))
            {
                continue;
            }

            tower = uid;
            return true;
        }

        tower = default;
        return false;
    }

    // a map with fewer part kinds than the gate needs can never open it: say so in the server log and the admin log
    private void WarnIfGateUnreachable(Entity<BadBloodObjectiveChainComponent> chain)
    {
        var kinds = 0;
        if (AnyOnPlanet<RMCFusionReactorComponent>())
            kinds++;
        if (AnyOnPlanet<CommunicationsTowerComponent>())
            kinds++;
        if (AnyOnPlanet<SensorTowerComponent>())
            kinds++;

        if (kinds >= chain.Comp.GatePartsRequired)
            return;

        Log.Warning($"Bad Blood objective gate needs {chain.Comp.GatePartsRequired} part kinds but the planet has {kinds}");
        _adminLog.Add(LogType.Action, LogImpact.High,
            $"BadBlood obj: the planet has {kinds} of the {chain.Comp.GatePartsRequired} infrastructure part kinds the gate needs");
    }

    private bool AnyOnPlanet<T>() where T : IComponent
    {
        var query = EntityQueryEnumerator<T, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (_planet.IsOnPlanetLevel(xform))
                return true;
        }

        return false;
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A living marine who is not a Bad Blood. Only they work the chain.
    /// </summary>
    private bool IsMarine(EntityUid user)
    {
        return HasComp<MarineComponent>(user) &&
               !_badBlood.IsBadBlood(user) &&
               !_mobState.IsIncapacitated(user);
    }

    private string SiteKindName(BadBloodUplinkSiteKind kind)
    {
        return Loc.GetString($"cmu-bad-blood-obj-site-{kind.ToString().ToLowerInvariant()}");
    }

    #endregion
}
