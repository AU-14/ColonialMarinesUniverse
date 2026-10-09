using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

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

    /// <summary>
    /// Spread added to every rider's gun, in degrees, at the vehicle's top speed; less at lower
    /// speeds. Shooting from a moving vehicle is hard.
    /// </summary>
    [DataField]
    public float MovingSpread = 18f;

    /// <summary>
    /// Time to climb in or out over a seat's shut door. Through an open door, or a seat with none,
    /// it is instant.
    /// </summary>
    [DataField]
    public TimeSpan ClosedDoorDelay = TimeSpan.FromSeconds(2);
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
    /// The hinged panel (a door, the tailgate) riders climb through to reach this seat, if any.
    /// </summary>
    [DataField]
    public string? Door;

    /// <summary>
    /// Screen pixels the rider's sprite is raised, for seats above the tub floor. Unused for
    /// directions listed in <see cref="PixelOffsets"/>, which already include it.
    /// </summary>
    [DataField]
    public float Lift;

    /// <summary>
    /// Where the art draws the rider's sprite centre, in pixels from the vehicle origin (x right, y
    /// up), per direction. The side views are squashed, so a turned seat offset alone misses the seat.
    /// From the generator's seat_offsets.json.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> PixelOffsets = new();

    /// <summary>
    /// Pixels below the rider sprite's centre where a seated rider is cut off, per direction, so
    /// their legs never show through the seats in front. Directions not listed show the whole
    /// sprite, e.g. for a standing gunner.
    /// </summary>
    [DataField]
    public Dictionary<Direction, float> Clips = new();
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

    [DataField, AutoNetworkedField]
    public Dictionary<Direction, Vector2> PixelOffsets = new();

    [DataField, AutoNetworkedField]
    public Dictionary<Direction, float> Clips = new();

    [DataField, AutoNetworkedField]
    public string? Door;
}

[Serializable, NetSerializable]
public sealed partial class CMUVehicleSeatEnterDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class CMUVehicleSeatExitDoAfterEvent : SimpleDoAfterEvent;
