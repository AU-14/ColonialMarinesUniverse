using System.Numerics;
using Content.Server.CMU14.Round;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Marines.Announce;
using Content.Shared.CMU14.Dropship.Exchange;
using Content.Shared.CMU14.Dropship.MultiDeck;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Dropship.Exchange;

/// <summary>
///     Lets the dynamic gunship's navigation computer trade the ship for the
///     platoon's standard Mohawk before its first takeoff. Once requested, the
///     gunship holds on the pad until every mob has stepped off, then it flies
///     back to the fleet (an off-round scratch map where it is deleted) while
///     the replacement Mohawk lands on the vacated pad.
/// </summary>
public sealed class CMUDropshipMohawkExchangeSystem : EntitySystem
{
    [Dependency] private SharedDropshipSystem _dropship = default!;
    [Dependency] private EntityLookupSystem _entityLookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMarineAnnounceSystem _marineAnnounce = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoonSpawn = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextCheck;

    private readonly List<Entity<CMUDropshipMohawkExchangeComponent, DropshipComponent>> _readyToDepart = new();
    private readonly HashSet<EntityUid> _gridEntities = new();
    private readonly List<EntityUid> _cancelledFlights = new();
    private readonly List<EntityUid> _departedFlights = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<DropshipNavigationComputerComponent, CMUDropshipMohawkExchangeMsg>(OnExchangeMsg);
        SubscribeLocalEvent<CMUDropshipMohawkExchangeComponent, FTLStartedEvent>(OnFTLStarted);
        SubscribeLocalEvent<CMUDropshipMohawkExchangeComponent, FTLCompletedEvent>(OnFTLCompleted);
        SubscribeLocalEvent<CMUDropshipMohawkExchangeComponent, ShuttleFTLSafetyEvent>(OnFTLSafety);
    }

    private void OnExchangeMsg(Entity<DropshipNavigationComputerComponent> ent, ref CMUDropshipMohawkExchangeMsg args)
    {
        if (_transform.GetGrid(ent.Owner) is not { } grid ||
            !TryComp(grid, out CMUDropshipMohawkExchangeComponent? exchange) ||
            !TryComp(grid, out DropshipComponent? dropship))
        {
            return;
        }

        if (!exchange.Parked || exchange.Departing || dropship.Crashed ||
            !_dropship.CanUseNavigation(ent, args.Actor))
            return;

        // A flight already committed by the pilot is about to withdraw the offer.
        if (TryComp(grid, out FTLComponent? ftl) &&
            ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving)
        {
            return;
        }

        exchange.Pending = !exchange.Pending;
        if (args.Actor.Valid)
        {
            _popup.PopupEntity(Loc.GetString(exchange.Pending
                    ? "cmu-dropship-exchange-requested"
                    : "cmu-dropship-exchange-cancelled"),
                ent,
                args.Actor);
        }

        RefreshNavigationUis(grid);
    }

    private void OnFTLStarted(Entity<CMUDropshipMohawkExchangeComponent> ent, ref FTLStartedEvent args)
    {
        if (ent.Comp.Departing)
        {
            _departedFlights.Add(ent);
            return;
        }

        // The round-start parking flight hasn't completed yet; the offer outlives it.
        if (!ent.Comp.Parked)
            return;

        // Any normal takeoff or flyby permanently withdraws the offer.
        RemCompDeferred<CMUDropshipMohawkExchangeComponent>(ent);
    }

    private void LaunchReplacement(Entity<CMUDropshipMohawkExchangeComponent> ent)
    {
        var exchange = ent.Comp;
        if (exchange.ExchangePad is not { } pad || exchange.ReplacementMap is not { } replacement)
            return;

        _dropship.SetDestinationShip(pad, null);
        if (!_platoonSpawn.TryLaunchExchangeShuttle(exchange.ReplacementGrids, exchange.Faction, pad))
        {
            Log.Error($"Failed to launch the exchange Mohawk {exchange.MohawkPath} to {ToPrettyString(pad)}.");
            QueueDel(replacement);
        }
        else
        {
            _marineAnnounce.AnnounceARES(ent,
                Loc.GetString("cmu-dropship-exchange-departing", ("ship", Name(ent))),
                faction: exchange.Faction);
        }

        exchange.ExchangePad = null;
        exchange.ReplacementMap = null;
        exchange.ReplacementGrids.Clear();
    }

    private void OnFTLSafety(Entity<CMUDropshipMohawkExchangeComponent> ent, ref ShuttleFTLSafetyEvent args)
    {
        if (args.Phase != ShuttleFTLSafetyPhase.Startup || !ent.Comp.Departing || !HasBoardedMobs(ent))
            return;

        args.Cancelled = true;
        args.Reason = "Mohawk exchange is waiting for everyone to disembark.";
        var exchange = ent.Comp;
        if (exchange.ExchangePad is { } pad)
        {
            _dropship.SetDropshipDestination(ent, pad);
            _dropship.SetDestinationShip(pad, ent);
        }

        if (exchange.ReplacementMap is { } replacement)
            QueueDel(replacement);
        if (exchange.DepartureMap is { } departure)
            QueueDel(departure);

        exchange.Departing = false;
        exchange.Pending = true;
        exchange.ExchangePad = null;
        exchange.ReplacementMap = null;
        exchange.DepartureMap = null;
        exchange.ReplacementGrids.Clear();
        _cancelledFlights.Add(ent);
    }

    private void OnFTLCompleted(Entity<CMUDropshipMohawkExchangeComponent> ent, ref FTLCompletedEvent args)
    {
        if (!ent.Comp.Departing)
        {
            ent.Comp.Parked = true;
            return;
        }

        // Keep the final deletion guard for mobs moved aboard after takeoff.
        if (HasBoardedMobs(ent.Owner))
        {
            Log.Warning($"Exchanged gunship {ToPrettyString(ent)} arrived at the fleet with mobs aboard, keeping it.");
            return;
        }

        QueueDel(ent.Owner);
        if (ent.Comp.DepartureMap is { } map)
            QueueDel(map);
    }

    public override void Update(float frameTime)
    {
        // FTLStarted runs inside the shuttle query; adding the replacement's
        // FTL component there would invalidate that query's enumeration.
        foreach (var grid in _departedFlights)
        {
            if (TryComp(grid, out CMUDropshipMohawkExchangeComponent? exchange) && exchange.Departing)
                LaunchReplacement((grid, exchange));
        }
        _departedFlights.Clear();

        // The safety gate removes FTL at the end of its tick. Refresh after
        // that removal so the pilot gets the waiting controls, not a countdown.
        for (var i = _cancelledFlights.Count - 1; i >= 0; i--)
        {
            var grid = _cancelledFlights[i];
            if (HasComp<FTLComponent>(grid))
                continue;

            if (!TerminatingOrDeleted(grid))
                RefreshNavigationUis(grid);
            _cancelledFlights.RemoveAt(i);
        }

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        _readyToDepart.Clear();
        var query = EntityQueryEnumerator<CMUDropshipMohawkExchangeComponent, DropshipComponent>();
        while (query.MoveNext(out var grid, out var exchange, out var dropship))
        {
            if (!exchange.Pending || exchange.Departing || !exchange.Parked || dropship.Crashed)
                continue;

            if (TryComp(grid, out FTLComponent? ftl) &&
                ftl.State is FTLState.Starting or FTLState.Travelling or FTLState.Arriving)
            {
                continue;
            }

            if (HasBoardedMobs(grid))
                continue;

            _readyToDepart.Add((grid, exchange, dropship));
        }

        foreach (var gunship in _readyToDepart)
        {
            BeginExchange(gunship);
        }
    }

    private void BeginExchange(Entity<CMUDropshipMohawkExchangeComponent, DropshipComponent> gunship)
    {
        var exchange = gunship.Comp1;
        exchange.Pending = false;

        if (gunship.Comp2.Destination is not { } pad ||
            !HasComp<DropshipDestinationComponent>(pad) ||
            HasComp<EphemeralDropshipDestinationComponent>(pad) ||
            FindNavComputer(gunship) is not { } computer)
        {
            Log.Warning($"Cancelled Mohawk exchange for {ToPrettyString(gunship)}: no usable pad or navigation computer.");
            RefreshNavigationUis(gunship);
            return;
        }

        // Load the replacement before committing: a broken Mohawk map must not launch the gunship.
        if (!_platoonSpawn.TryLoadExchangeShuttle(exchange.MohawkPath, out var mohawkMap, out var mohawkGrids))
        {
            Log.Error($"Failed to load exchange Mohawk map {exchange.MohawkPath} for {ToPrettyString(gunship)}.");
            RefreshNavigationUis(gunship);
            return;
        }

        // The round-start engine cooldown would reject the departure flight.
        if (TryComp(gunship, out FTLComponent? ftl) && ftl.State is FTLState.Cooldown or FTLState.Available)
            RemComp<FTLComponent>(gunship);

        var departureMap = _map.CreateMap(out var departureMapId);
        var departure = Spawn(null, new MapCoordinates(Vector2.Zero, departureMapId));
        EnsureComp<DropshipDestinationComponent>(departure);
        EnsureComp<EphemeralDropshipDestinationComponent>(departure);
        _dropship.SetDestinationType(departure, DropshipDestinationComponent.DestinationType.Dropship.ToString());
        _dropship.SetFactionController(departure, exchange.Faction);

        if (!_dropship.FlyTo(computer, departure, null))
        {
            Log.Error($"Failed to launch {ToPrettyString(gunship)} for its Mohawk exchange.");
            QueueDel(mohawkMap);
            QueueDel(departureMap);
            RefreshNavigationUis(gunship);
            return;
        }

        exchange.Departing = true;
        exchange.DepartureMap = departureMap;
        exchange.ExchangePad = pad;
        exchange.ReplacementMap = mohawkMap;
        exchange.ReplacementGrids = mohawkGrids;
        // FlyTo releases the old reservation during setup. Hold it through the
        // countdown so a cancelled departure cannot lose its occupied pad.
        _dropship.SetDestinationShip(pad, gunship);
    }

    private bool HasBoardedMobs(EntityUid grid)
    {
        if (!TryQueryGridEntities(grid))
            return false;

        foreach (var uid in _gridEntities)
        {
            if (HasComp<MobStateComponent>(uid))
                return true;
        }

        return false;
    }

    private Entity<DropshipNavigationComputerComponent>? FindNavComputer(EntityUid grid)
    {
        if (!TryQueryGridEntities(grid))
            return null;

        foreach (var uid in _gridEntities)
        {
            if (TryComp(uid, out DropshipNavigationComputerComponent? computer))
                return (uid, computer);
        }

        return null;
    }

    private bool TryQueryGridEntities(EntityUid grid)
    {
        if (!TryComp(grid, out MapGridComponent? mapGrid) ||
            !TryComp(grid, out TransformComponent? xform))
        {
            return false;
        }

        var worldBounds = _transform.GetWorldMatrix(xform).TransformBox(mapGrid.LocalAABB);
        _gridEntities.Clear();
        _entityLookup.GetEntitiesIntersecting(grid, worldBounds, _gridEntities, LookupFlags.All);
        return true;
    }

    private void RefreshNavigationUis(EntityUid grid)
    {
        // DropshipSystem refreshes every open navigation screen on this event.
        var changed = new DropshipBoardingChangedEvent();
        RaiseLocalEvent(grid, ref changed);
    }
}
