using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Shared._RMC14.Dropship;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.CMU14.Hospital;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Hospital;

public sealed partial class HospitalEmergencySystem
{
    private bool TransportUnavailable(EntityUid uid)
        => TerminatingOrDeleted(uid) || EntityManager.IsQueuedForDeletion(uid);

    private bool IsCurrentHospitalComputer(Entity<HospitalEmergencyComputerComponent> computer)
        => !TransportUnavailable(computer) &&
           TryComp<HospitalEmergencyComputerComponent>(computer, out var current) && ReferenceEquals(current, computer.Comp);

    private bool TryGetTransportLease(EntityUid shuttle, out Entity<HospitalTransportLeaseComponent> lease)
    {
        lease = default;
        if (!TryComp<HospitalTransportShuttleComponent>(shuttle, out var marker) ||
            !TryComp<HospitalTransportLeaseComponent>(marker.Lease, out var comp) || comp.Shuttle != shuttle ||
            TransportUnavailable(marker.Lease))
            return false;
        lease = (marker.Lease, comp);
        return true;
    }

    private bool TryGetTransportController(Entity<HospitalTransportLeaseComponent> lease,
        out Entity<HospitalEmergencyComputerComponent> computer)
    {
        computer = default;
        if (lease.Comp.Computer is not { } uid || TransportUnavailable(uid) ||
            !TryComp<HospitalEmergencyComputerComponent>(uid, out var comp) ||
            !ReferenceEquals(comp, lease.Comp.Controller) || comp.ActiveShuttle != lease.Comp.Shuttle)
            return false;
        computer = (uid, comp);
        return true;
    }

    private bool IsOriginalHospitalMap(HospitalTransportLeaseComponent lease)
        => !TransportUnavailable(lease.HospitalMap) &&
           TryComp<MapComponent>(lease.HospitalMap, out var map) && ReferenceEquals(map, lease.HospitalMapComponent);

    private bool TryStartHospitalFlight(Entity<HospitalTransportLeaseComponent> lease, EntityUid destination,
        EntityUid? user, float startupTime, HospitalShuttlePurpose purpose)
    {
        var ship = lease.Comp.Shuttle;
        lease.Comp.NextAction = _timing.CurTime + UiRefreshInterval;
        if (TransportUnavailable(ship) || HasComp<FTLComponent>(ship) ||
            !TryComp<DropshipComponent>(ship, out var dropship) ||
            !TryComp<ShuttleComponent>(ship, out var shuttle) ||
            TransportUnavailable(destination) || !TryComp<DropshipDestinationComponent>(destination, out var target) ||
            Transform(destination).MapUid is not { } mapUid || TransportUnavailable(mapUid) ||
            !TryComp<MapComponent>(mapUid, out var map) || !TryFindNavigationComputer(ship, out var nav))
        {
            lease.Comp.Failure = "Hospital transport is waiting for navigation, a valid destination, or flight cooldown.";
            return false;
        }
        if ((target.Ship is { } occupant && occupant != ship) ||
            ((purpose is HospitalShuttlePurpose.InboundPatients or HospitalShuttlePurpose.PickupInbound or HospitalShuttlePurpose.None) &&
             (!IsOriginalHospitalMap(lease.Comp) || mapUid != lease.Comp.HospitalMap)))
        {
            lease.Comp.Failure = "Hospital transport requires an unoccupied destination on its original hospital map.";
            return false;
        }

        var destinationPosition = _transform.GetMapCoordinates(destination);
        var sourceMap = Transform(ship).MapUid;
        var previousDestination = dropship.Destination;
        var previousDestinationComponent = CompOrNull<DropshipDestinationComponent>(previousDestination);
        var previousShip = previousDestinationComponent?.Ship;
        var destinationShip = target.Ship;
        var interrupted = false;
        try
        {
            // FlyTo's bool is not a commit result: FTL setup can be vetoed inside
            // its void FTLToCoordinates call. Inspect the actual resulting flight.
            _dropship.FlyTo(nav, destination, user, startupTime: startupTime, hyperspaceTime: lease.Comp.TravelTime);
        }
        catch (Exception exception)
        {
            interrupted = true;
            Log.Warning($"Hospital transport launch failed: {exception}");
        }

        // Setup can create a Starting component before assigning its target.
        // An exception in that interval must not leave an invalid flight for the
        // next engine tick. A valid or rerouted flight remains owned and observed.
        if (interrupted && !TransportUnavailable(ship) && Transform(ship).MapUid == sourceMap &&
            TryComp<DropshipComponent>(ship, out var interruptedDropship) && ReferenceEquals(interruptedDropship, dropship) &&
            interruptedDropship.Destination == destination && TryComp<FTLComponent>(ship, out var incomplete) &&
            incomplete.State == FTLState.Starting &&
            (TransportUnavailable(incomplete.TargetCoordinates.EntityId) ||
             !TryComp<TransformComponent>(incomplete.TargetCoordinates.EntityId, out _)))
        {
            var startupStream = incomplete.StartupStream;
            RemComp<FTLComponent>(ship);
            _audio.Stop(startupStream);
        }

        if (!TransportUnavailable(ship) && !TransportUnavailable(destination) && !TransportUnavailable(mapUid) &&
            TryComp<DropshipComponent>(ship, out var currentDropship) && ReferenceEquals(currentDropship, dropship) &&
            TryComp<ShuttleComponent>(ship, out var currentShuttle) && ReferenceEquals(currentShuttle, shuttle) &&
            TryComp<DropshipDestinationComponent>(destination, out var currentTarget) && ReferenceEquals(currentTarget, target) &&
            TryComp<MapComponent>(mapUid, out var currentMap) && ReferenceEquals(currentMap, map) &&
            Transform(destination).MapUid == mapUid && dropship.Destination == destination &&
            TryComp<FTLComponent>(ship, out var ftl) && ftl.State == FTLState.Starting &&
            !TransportUnavailable(ftl.TargetCoordinates.EntityId) &&
            TryComp<TransformComponent>(ftl.TargetCoordinates.EntityId, out _) &&
            _transform.ToMapCoordinates(ftl.TargetCoordinates).MapId == destinationPosition.MapId &&
            (_transform.ToMapCoordinates(ftl.TargetCoordinates).Position - destinationPosition.Position).LengthSquared() < 0.0001f)
        {
            lease.Comp.Flight = new HospitalTransportFlight(destination, target, mapUid, map, destinationPosition,
                ftl.TargetCoordinates, dropship, shuttle, ftl, purpose, sourceMap,
                previousDestination, previousDestinationComponent, previousShip, destinationShip);
            lease.Comp.Failure = string.Empty;
            return true;
        }

        // An uncommitted setup may already have changed destination occupancy.
        // Restore only the same components and fields still owned by this attempt;
        // a nested reroute or another ship's reservation must remain untouched.
        if (!TransportUnavailable(ship) && !HasComp<FTLComponent>(ship) &&
            TryComp<DropshipComponent>(ship, out var remaining) && ReferenceEquals(remaining, dropship) &&
            dropship.Destination == destination)
        {
            _dropship.SetDropshipDestination(ship, previousDestination);
            if (!TransportUnavailable(destination) &&
                TryComp<DropshipDestinationComponent>(destination, out var sameTarget) &&
                ReferenceEquals(sameTarget, target) && target.Ship == ship)
                _dropship.SetDestinationShip(destination, destinationShip);
            if (previousDestination is { } previous && !TransportUnavailable(previous) &&
                TryComp<DropshipDestinationComponent>(previous, out var samePrevious) &&
                ReferenceEquals(samePrevious, previousDestinationComponent) && samePrevious.Ship == null)
                _dropship.SetDestinationShip(previous, previousShip);
        }
        lease.Comp.Failure = "Hospital flight was not committed. Passengers remain assigned; departure will be retried.";
        return false;
    }

    private bool IsExpectedHospitalArrival(Entity<HospitalTransportLeaseComponent> lease, FTLCompletedEvent args)
    {
        if (lease.Comp.Flight is not { } flight || args.Entity != lease.Comp.Shuttle || args.MapUid != flight.Map ||
            TransportUnavailable(args.Entity) || TransportUnavailable(flight.Destination) || TransportUnavailable(flight.Map) ||
            !TryComp<MapComponent>(flight.Map, out var map) || !ReferenceEquals(map, flight.MapComponent) ||
            !TryComp<DropshipDestinationComponent>(flight.Destination, out var destination) || !ReferenceEquals(destination, flight.DestinationComponent) ||
            !TryComp<DropshipComponent>(args.Entity, out var dropship) || !ReferenceEquals(dropship, flight.Dropship) ||
            !TryComp<ShuttleComponent>(args.Entity, out var shuttle) || !ReferenceEquals(shuttle, flight.Shuttle) ||
            !TryComp<FTLComponent>(args.Entity, out var ftl) || !ReferenceEquals(ftl, flight.Ftl) ||
            ftl.State != FTLState.Cooldown || ftl.TargetCoordinates != flight.TargetCoordinates ||
            dropship.Destination != flight.Destination || destination.Ship != args.Entity ||
            Transform(flight.Destination).MapUid != flight.Map || Transform(args.Entity).MapUid != flight.Map)
            return false;
        var currentPosition = _transform.GetMapCoordinates(flight.Destination);
        var landedPosition = _transform.GetMapCoordinates(args.Entity);
        return currentPosition.MapId == flight.DestinationPosition.MapId &&
               (currentPosition.Position - flight.DestinationPosition.Position).LengthSquared() < 0.0001f &&
               landedPosition.MapId == flight.DestinationPosition.MapId &&
               (landedPosition.Position - flight.DestinationPosition.Position).LengthSquared() < 0.0001f;
    }

    private void ReconcileUnfinishedHospitalFlight(Entity<HospitalTransportLeaseComponent> lease)
    {
        if (lease.Comp.Flight is not { } flight)
            return;
        lease.Comp.Flight = null;
        var ship = lease.Comp.Shuttle;
        // A startup safety veto leaves the grid on its original map. Retire the
        // unfulfilled reservation so the retry is not misclassified as a fly-by.
        if (TransportUnavailable(ship) || HasComp<FTLComponent>(ship) || Transform(ship).MapUid != flight.SourceMap ||
            flight.SourceMap == flight.Map || !TryComp<DropshipComponent>(ship, out var dropship) ||
            !ReferenceEquals(dropship, flight.Dropship) || dropship.Destination != flight.Destination)
            return;
        _dropship.SetDropshipDestination(ship, flight.PreviousDestination);
        if (!TransportUnavailable(flight.Destination) &&
            TryComp<DropshipDestinationComponent>(flight.Destination, out var target) &&
            ReferenceEquals(target, flight.DestinationComponent) && target.Ship == ship)
            _dropship.SetDestinationShip(flight.Destination, flight.DestinationPreviousShip);
        if (flight.PreviousDestination is { } previous && !TransportUnavailable(previous) &&
            TryComp<DropshipDestinationComponent>(previous, out var old) &&
            ReferenceEquals(old, flight.PreviousDestinationComponent) && old.Ship == null)
            _dropship.SetDestinationShip(previous, flight.PreviousShip);
    }

    private bool HasProtectedTransportContent(HospitalTransportLeaseComponent lease, bool offShuttleOnly = false)
    {
        var query = EntityManager.AllEntityQueryEnumerator<TransformComponent>();
        while (query.MoveNext(out var uid, out var transform))
        {
            var onShuttle = uid != lease.Shuttle &&
                (transform.GridUid == lease.Shuttle || transform.ParentUid == lease.Shuttle);
            var onOwnedMap = transform.MapUid is { } map && lease.Maps.ContainsKey(map);
            if ((!onShuttle && !onOwnedMap) || offShuttleOnly && onShuttle || PendingTransportRetirement(uid))
                continue;
            // Authored equipment can be reclaimed with its map. A live character,
            // player-controlled entity, or foreign movable object never can.
            if (HasComp<MobStateComponent>(uid) || HasComp<ActorComponent>(uid))
                return true;
            if (!lease.AuthoredEntities.Contains(uid) &&
                (HasComp<ItemComponent>(uid) || HasComp<BodyPartComponent>(uid) || HasComp<OrganComponent>(uid) ||
                 HasComp<MapGridComponent>(uid) || HasComp<PhysicsComponent>(uid) ||
                 HasComp<StorageComponent>(uid) || HasComp<EntityStorageComponent>(uid)))
                return true;
        }
        return false;
    }

    private bool PendingTransportRetirement(EntityUid uid)
    {
        while (TryComp<TransformComponent>(uid, out var transform))
        {
            if (TransportUnavailable(uid))
                return true;
            if (transform.ParentUid == EntityUid.Invalid)
                return false;
            uid = transform.ParentUid;
        }
        return false;
    }

    private bool TryReclaimTransport(Entity<HospitalTransportLeaseComponent> lease)
    {
        if (lease.Comp.Roots.Contains(lease.Comp.HospitalMap) || HasProtectedTransportContent(lease.Comp))
            return false;
        // If a managed map's component was replaced, ownership is uncertain.
        foreach (var (uid, original) in lease.Comp.Maps)
        {
            if (!TransportUnavailable(uid) &&
                (!TryComp<MapComponent>(uid, out var current) || !ReferenceEquals(original, current)))
                return false;
        }
        if (!TransportUnavailable(lease.Comp.Shuttle))
            QueueDel(lease.Comp.Shuttle);
        if (lease.Comp.ReturnDestination is { } destination && !TransportUnavailable(destination))
            QueueDel(destination);
        foreach (var root in lease.Comp.Roots)
        {
            if (!TransportUnavailable(root))
                QueueDel(root);
        }
        QueueDel(lease.Owner);
        return true;
    }

    private void UpdateTransportLeases(TimeSpan now)
    {
        var query = EntityQueryEnumerator<HospitalTransportLeaseComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (TransportUnavailable(uid) || now < comp.NextAction)
                continue;
            var lease = (uid, comp);
            comp.NextAction = now + UiRefreshInterval;
            if (!comp.Retiring && TryGetTransportController(lease, out _))
                continue;
            comp.Retiring = true;
            comp.Computer = null;
            comp.Controller = null;
            if (TryComp<FTLComponent>(comp.Shuttle, out var ftl) &&
                ftl.State is not (FTLState.Cooldown or FTLState.Available))
                continue;
            if (TryReclaimTransport(lease))
                continue;
            if (HasComp<FTLComponent>(comp.Shuttle))
                continue;
            ReconcileUnfinishedHospitalFlight(lease);
            TryRecoverHospitalTransport(lease);
        }
    }

    private bool TryRecoverHospitalTransport(Entity<HospitalTransportLeaseComponent> lease)
    {
        if (TransportUnavailable(lease.Comp.Shuttle) || !IsOriginalHospitalMap(lease.Comp))
        {
            lease.Comp.Failure = "Hospital transport recovery requires its original hospital map and surviving shuttle.";
            return false;
        }
        if (Transform(lease.Comp.Shuttle).MapUid == lease.Comp.HospitalMap)
        {
            lease.Comp.Failure = "Hospital transport recovered. Unload remaining occupants and belongings before retirement.";
            return true;
        }
        foreach (var (uid, original) in lease.Comp.Maps)
        {
            if (!TransportUnavailable(uid) &&
                (!TryComp<MapComponent>(uid, out var current) || !ReferenceEquals(original, current)))
            {
                lease.Comp.Failure = "Restore the original leased map identity before moving this transport or reclaiming its contents.";
                return false;
            }
        }
        if (HasProtectedTransportContent(lease.Comp, offShuttleOnly: true))
        {
            lease.Comp.Failure = "Board all people and belongings on the return map before hospital recovery.";
            return false;
        }
        var destination = lease.Comp.HospitalDestination;
        if (TransportUnavailable(destination) || !HasComp<HospitalDropshipLandingZoneComponent>(destination) ||
            !HasComp<DropshipDestinationComponent>(destination) || Transform(destination).MapUid != lease.Comp.HospitalMap)
        {
            lease.Comp.Failure = "Restore the hospital landing marker on its original map to recover this transport.";
            return false;
        }
        return TryStartHospitalFlight(lease, destination, null, lease.Comp.StartupTime, HospitalShuttlePurpose.None);
    }

    private void OnTransportRecoveryVerb(Entity<DropshipNavigationComputerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || Transform(ent).GridUid is not { } shuttle ||
            !TryGetTransportLease(shuttle, out var lease))
            return;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = "Recover hospital transport",
            Message = string.IsNullOrEmpty(lease.Comp.Failure)
                ? "Cancel this transport's trip and return its remaining passengers to the hospital without settling a bill."
                : lease.Comp.Failure,
            Act = () => RequestHospitalTransportRecovery(shuttle),
        });
    }

    public bool RequestHospitalTransportRecovery(EntityUid shuttle)
    {
        if (!TryGetTransportLease(shuttle, out var lease))
            return false;
        if (TryGetTransportController(lease, out var computer))
        {
            ClearComputerTransport(computer.Comp);
            computer.Comp.Status = HospitalEmergencyStatus.Treating;
            computer.Comp.TransportFailure = "Transport trip cancelled for recovery. Patients remain assigned to the hospital.";
            UpdateUi(computer);
        }
        lease.Comp.Computer = null;
        lease.Comp.Controller = null;
        lease.Comp.Retiring = true;
        lease.Comp.NextAction = _timing.CurTime;
        return TryRecoverHospitalTransport(lease);
    }

    private static void ClearComputerTransport(HospitalEmergencyComputerComponent comp)
    {
        comp.ActiveShuttle = null;
        comp.ReturnDestination = null;
        comp.ExpectedDestination = null;
        comp.ShuttlePurpose = HospitalShuttlePurpose.None;
        comp.TransportRoots.Clear();
    }
}
