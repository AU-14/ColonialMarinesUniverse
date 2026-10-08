using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Open-vehicle seats: one strap entity per seat, spawned on the vehicle. Sitting in the driver
/// seat gives control of the vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleSeatsComponent : Component
{
    [DataField(required: true)]
    public List<CMUVehicleSeatData> Seats = new();

    [DataField]
    public EntProtoId SeatPrototype = "CMUVehicleSeat";

    [DataField, AutoNetworkedField]
    public List<EntityUid> SeatEntities = new();

    /// <summary>
    /// Spread added to a passenger's gun, in degrees, at no firearms skill; each skill level takes
    /// off <see cref="SpreadPerFirearms"/> down to <see cref="MinPassengerSpread"/>.
    /// </summary>
    [DataField]
    public float PassengerSpread = 24f;

    [DataField]
    public float SpreadPerFirearms = 8f;

    [DataField]
    public float MinPassengerSpread = 4f;
}

[DataDefinition]
public sealed partial class CMUVehicleSeatData
{
    /// <summary>
    /// Seat id, matching its click_seat_* mask and seat_*_outline states.
    /// </summary>
    [DataField(required: true)]
    public string Id = string.Empty;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// Seat entity to spawn instead of the vehicle's default, e.g. a weapons seat.
    /// </summary>
    [DataField]
    public EntProtoId? Prototype;

    /// <summary>
    /// Seat position in vehicle space, with the vehicle facing south.
    /// </summary>
    [DataField]
    public Vector2 Offset;

    /// <summary>
    /// Where the rider steps out, in vehicle space.
    /// </summary>
    [DataField]
    public Vector2 Exit;

    [DataField]
    public bool Driver;

    /// <summary>
    /// Screen pixels the rider's sprite is raised, for seats above the tub floor.
    /// </summary>
    [DataField]
    public float Lift;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUVehicleSeatComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Vehicle;

    [DataField, AutoNetworkedField]
    public bool Driver;

    [DataField, AutoNetworkedField]
    public Vector2 Exit;

    [DataField, AutoNetworkedField]
    public float Lift;
}
