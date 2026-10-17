using Content.Server.Shuttles.Components;
using Content.Shared._RMC14.Dropship;
using Content.Shared.CMU14.Hospital;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Hospital;

/// <summary>
/// Lives on a small independent entity so console, shuttle, or map deletion cannot
/// discard ownership of the remaining transport maps and occupants.
/// </summary>
[RegisterComponent]
public sealed partial class HospitalTransportLeaseComponent : Component
{
    public EntityUid Shuttle;
    public EntityUid? Computer;
    public HospitalEmergencyComputerComponent? Controller;
    public EntityUid HospitalMap;
    public MapComponent HospitalMapComponent = default!;
    public EntityUid HospitalDestination;
    public EntityUid? ReturnDestination;
    public readonly HashSet<EntityUid> Roots = new();
    public readonly Dictionary<EntityUid, MapComponent> Maps = new();
    public readonly HashSet<EntityUid> AuthoredEntities = new();
    public HospitalTransportFlight? Flight;
    public bool Retiring;
    public TimeSpan NextAction;
    public string Failure = string.Empty;
    public float StartupTime;
    public float TravelTime;
}

/// <summary>Links a hospital shuttle to its durable lease and exempts it from generic retirement.</summary>
[RegisterComponent]
public sealed partial class HospitalTransportShuttleComponent : Component
{
    public EntityUid Lease;
}

public sealed record HospitalTransportFlight(
    EntityUid Destination,
    DropshipDestinationComponent DestinationComponent,
    EntityUid Map,
    MapComponent MapComponent,
    MapCoordinates DestinationPosition,
    EntityCoordinates TargetCoordinates,
    DropshipComponent Dropship,
    ShuttleComponent Shuttle,
    FTLComponent Ftl,
    HospitalShuttlePurpose Purpose,
    EntityUid? SourceMap,
    EntityUid? PreviousDestination,
    DropshipDestinationComponent? PreviousDestinationComponent,
    EntityUid? PreviousShip,
    EntityUid? DestinationPreviousShip);
