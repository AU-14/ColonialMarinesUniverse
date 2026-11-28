using Content.Server._RMC14.Dropship;
using Content.Server.Administration.Logs;
using Content.Server.CMU14.Ops.ThirdParty;
using Content.Server.CMU14.Round;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.CMU14.Round;
using Content.Shared.CMU14.Threats;
using Content.Shared.Database;
using Content.Shared.Doors.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Threats.Shuttle;

/// <summary>
///     Flies <see cref="ThreatEntryMethod.Shuttle"/> threat ships to a landing zone once the threat deploys,
///     opens them on landing, and removes unclaimed threat bodies still aboard when the ship leaves again
///     (the third-party auto-return or a manual flight).
/// </summary>
public sealed class ThreatShuttleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private AuRoundSystem _auRound = default!;
    [Dependency] private DropshipSystem _dropship = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string ThirdPartyFaction = "thirdparty";

    private readonly List<EntityUid> _departureCleanup = new();

    /// <summary>How often a ship past its launch deadline keeps looking for a free landing zone.</summary>
    private static readonly TimeSpan LateRetryInterval = TimeSpan.FromSeconds(30);

    public override void Initialize()
    {
        base.Initialize();
        // Both are broadcasts, which are not bus-unique.
        SubscribeLocalEvent<ThreatDeployedEvent>(OnThreatDeployed);
        SubscribeLocalEvent<FTLCompletedEvent>(OnFTLCompleted);
    }

    private void OnThreatDeployed(ref ThreatDeployedEvent ev)
    {
        if (ev.Shuttle is not { } ship || !TryComp(ship, out CMUThreatShuttleComponent? comp))
            return;

        var now = _timing.CurTime;
        comp.LaunchPending = true;
        comp.RetryAt = now;
        comp.LaunchDeadline = now + comp.LaunchTimeout;
        TryLaunch((ship, comp), now);
    }

    private void OnFTLCompleted(ref FTLCompletedEvent args)
    {
        if (!TryComp(args.Entity, out CMUThreatShuttleComponent? comp))
            return;

        var destination = CompOrNull<DropshipComponent>(args.Entity)?.Destination;
        if (destination is not { } zone || HasComp<ThirdPartyDropshipReturnDestinationComponent>(zone))
        {
            // Back at (or never left) its deep-space pad: nothing to open, and the launch is over.
            if (comp.State == CMUThreatShuttleState.InFlight)
            {
                comp.State = CMUThreatShuttleState.Loaded;
                comp.LaunchPending = false;
            }

            return;
        }

        if (comp.State == CMUThreatShuttleState.Landed)
            return;

        Land((args.Entity, comp), zone);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CMUThreatShuttleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            switch (comp.State)
            {
                case CMUThreatShuttleState.Loaded when comp.LaunchPending && now >= comp.RetryAt:
                    TryLaunch((uid, comp), now);
                    break;
                case CMUThreatShuttleState.InFlight when now >= comp.LaunchDeadline && !HasComp<FTLComponent>(uid):
                    // The flight ended without landing anywhere we recognise; the console stays usable.
                    Log.Warning($"Threat ship {ToPrettyString(uid)} did not reach {ToPrettyString(comp.Destination)} in time.");
                    comp.State = CMUThreatShuttleState.Loaded;
                    comp.LaunchPending = false;
                    break;
                case CMUThreatShuttleState.Landed when IsLeaving(uid):
                    _departureCleanup.Add(uid);
                    break;
            }
        }

        foreach (var ship in _departureCleanup)
        {
            if (TryComp(ship, out CMUThreatShuttleComponent? comp))
                Depart((ship, comp));
        }

        _departureCleanup.Clear();
    }

    private void TryLaunch(Entity<CMUThreatShuttleComponent> ship, TimeSpan now)
    {
        var comp = ship.Comp;
        if (now >= comp.LaunchDeadline && comp.StartWinConditionsOnLanding)
        {
            // No zone came free in time. The round must still be able to end, so the win conditions start now;
            // the ship keeps trying to land, more slowly, and the console stays usable for the crew.
            Log.Warning($"Threat ship {ToPrettyString(ship)} found no free landing zone in time; starting its win conditions and retrying slowly.");
            comp.StartWinConditionsOnLanding = false;
            if (_prototypes.TryIndex(comp.Threat, out var deferred))
                _auRound.StartThreatWinConditions(deferred);
        }

        comp.RetryAt = now + (now >= comp.LaunchDeadline ? LateRetryInterval : comp.RetryInterval);
        if (HasComp<FTLComponent>(ship))
        {
            // Already flying (the crew launched it themselves); landing is handled on arrival.
            comp.LaunchPending = false;
            return;
        }

        if (comp.Navigation is not { } navUid
            || !TryComp(navUid, out DropshipNavigationComputerComponent? nav)
            || !_prototypes.TryIndex(comp.Threat, out var threat))
        {
            Log.Error($"Threat ship {ToPrettyString(ship)} has no navigation console or threat; it cannot launch.");
            comp.LaunchPending = false;
            return;
        }

        if (!TryChooseLandingZone(comp, threat, out var zone, out var thirdPartyZone))
            return;

        // The console keeps auto-returning; only its landing rights follow the chosen zone.
        SetConsoleFaction(comp, thirdPartyZone ? ThirdPartyFaction : string.Empty);

        try
        {
            _dropship.FlyTo((navUid, nav),
                zone,
                null,
                startupTime: (float) comp.StartupTime.TotalSeconds,
                hyperspaceTime: (float) comp.HyperspaceTime.TotalSeconds);
        }
        catch (Exception ex)
        {
            Log.Warning($"Threat ship {ToPrettyString(ship)} failed to launch to {ToPrettyString(zone)}: {ex}");
            SetConsoleFaction(comp, ThirdPartyFaction);
            return;
        }

        // FlyTo can refuse after its own checks, so the flight is confirmed by what it left behind.
        if (!HasComp<FTLComponent>(ship))
        {
            SetConsoleFaction(comp, ThirdPartyFaction);
            return;
        }

        comp.State = CMUThreatShuttleState.InFlight;
        comp.Destination = zone;
        comp.LaunchPending = false;
        comp.LaunchDeadline = now + comp.LaunchTimeout;
    }

    private bool TryChooseLandingZone(CMUThreatShuttleComponent comp,
        ThreatPrototype threat,
        out EntityUid zone,
        out bool thirdPartyZone)
    {
        zone = default;
        thirdPartyZone = false;

        var thirdParty = new List<EntityUid>();
        var neutral = new List<EntityUid>();
        var query = EntityQueryEnumerator<DropshipDestinationComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var destination, out var xform))
        {
            // The trade shuttle's filter: real, free dropship pads on the planet.
            if (xform.MapID != comp.PlanetMap
                || destination.Destinationtype != DropshipDestinationComponent.DestinationType.Dropship
                || destination.Ship != null
                || HasComp<EphemeralDropshipDestinationComponent>(uid)
                || HasComp<ThirdPartyDropshipReturnDestinationComponent>(uid))
                continue;

            if (string.Equals(destination.FactionController, ThirdPartyFaction, StringComparison.OrdinalIgnoreCase))
                thirdParty.Add(uid);
            else if (string.IsNullOrEmpty(destination.FactionController))
                neutral.Add(uid);
        }

        (List<EntityUid> Pool, bool ThirdParty)[] pools = threat.LandingZone switch
        {
            ThreatLandingZonePolicy.ThirdParty => new[] { (thirdParty, true) },
            ThreatLandingZonePolicy.Neutral => new[] { (neutral, false) },
            _ => new[] { (thirdParty, true), (neutral, false) },
        };

        if (_auRound.GetSelectedPlanetId() is { } planet
            && threat.LandingZoneByPlanet.TryGetValue(planet, out var preferredName))
        {
            foreach (var (pool, isThirdParty) in pools)
            {
                var named = pool.FindAll(uid =>
                    string.Equals(Name(uid), preferredName, StringComparison.OrdinalIgnoreCase));
                if (named.Count == 0)
                    continue;

                zone = _random.Pick(named);
                thirdPartyZone = isThirdParty;
                return true;
            }
        }

        foreach (var (pool, isThirdParty) in pools)
        {
            if (pool.Count == 0)
                continue;

            zone = _random.Pick(pool);
            thirdPartyZone = isThirdParty;
            return true;
        }

        return false;
    }

    private void Land(Entity<CMUThreatShuttleComponent> ship, EntityUid zone)
    {
        var comp = ship.Comp;
        comp.State = CMUThreatShuttleState.Landed;
        comp.Destination = zone;
        comp.LaunchPending = false;

        // The deep-space return pad is third-party controlled; a ship that came down on a neutral zone must get
        // its third-party rights back or the auto-return (a plain FlyTo) is refused forever.
        SetConsoleFaction(comp, ThirdPartyFaction);

        foreach (var grid in GetShipGrids(ship))
        {
            var children = Transform(grid).ChildEnumerator;
            while (children.MoveNext(out var child))
            {
                if (HasComp<DockingComponent>(child) && TryComp(child, out DoorBoltComponent? bolt))
                    _dropship.UnlockDoor((child, bolt));
            }
        }

        if (comp.StartWinConditionsOnLanding && _prototypes.TryIndex(comp.Threat, out var threat))
        {
            // Once: a later manual landing elsewhere must not start them again.
            comp.StartWinConditionsOnLanding = false;
            _auRound.StartThreatWinConditions(threat);
        }

        Log.Info($"Threat ship {ToPrettyString(ship)} landed at {ToPrettyString(zone)}.");
    }

    private void Depart(Entity<CMUThreatShuttleComponent> ship)
    {
        ship.Comp.State = CMUThreatShuttleState.Departed;

        var grids = GetShipGrids(ship);
        var bodies = new List<EntityUid>();
        var query = EntityQueryEnumerator<UnclaimedForceRoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.GridUid is { } grid && grids.Contains(grid))
                bodies.Add(uid);
        }

        foreach (var body in bodies)
        {
            _adminLog.Add(LogType.EntityDelete,
                LogImpact.Low,
                $"Unclaimed threat body {ToPrettyString(body):entity} was removed as {ToPrettyString(ship):ship} departed.");
            QueueDel(body);
        }

        _adminLog.Add(LogType.EntityDelete,
            LogImpact.Medium,
            $"Threat ship {ToPrettyString(ship):ship} left {ToPrettyString(ship.Comp.Destination)}; removed {bodies.Count} unclaimed threat body/bodies aboard.");
    }

    /// <summary>
    ///     Sets the landing rights of the ship's console: <c>"thirdparty"</c> for third-party zones and the return pad,
    ///     empty for neutral zones. The console always keeps its auto-return.
    /// </summary>
    private void SetConsoleFaction(CMUThreatShuttleComponent comp, string faction)
    {
        if (comp.Navigation is not { } navUid || TerminatingOrDeleted(navUid))
            return;

        var whitelist = EnsureComp<WhitelistedShuttleComponent>(navUid);
        whitelist.Faction = faction;
        whitelist.AutoReturn = true;
        Dirty(navUid, whitelist);
    }

    private List<EntityUid> GetShipGrids(EntityUid ship)
    {
        var grids = new List<EntityUid> { ship };
        if (TryComp(ship, out MultiDeckDropshipComponent? multiDeck))
        {
            foreach (var deck in multiDeck.Decks.Values)
            {
                if (!grids.Contains(deck))
                    grids.Add(deck);
            }
        }

        return grids;
    }

    private bool IsLeaving(EntityUid ship)
        => TryComp(ship, out FTLComponent? ftl)
            && ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving;
}
