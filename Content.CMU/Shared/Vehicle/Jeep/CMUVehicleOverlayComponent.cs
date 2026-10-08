using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Spawns a child entity drawn over riders, for open vehicles whose near side must cover the crew.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUVehicleOverlaySystem))]
public sealed partial class CMUVehicleOverlayComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField, AutoNetworkedField]
    public EntityUid? Overlay;
}

/// <summary>
/// Visual state for the overlay entity, read by the client to follow its vehicle.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUVehicleOverlayVisualsComponent : Component
{
    /// <summary>
    /// Per-direction pixel offsets (x right, y up) for layers drawn around a pivot, by layer key.
    /// </summary>
    [DataField]
    public Dictionary<string, Dictionary<Direction, Vector2>> LayerOffsets = new();

    /// <summary>
    /// Per-direction pixel offset for every other layer. The overlay frames are stored slid down so
    /// Robust y-sorts them at the vehicle's near edge, and are drawn back up by this much.
    /// </summary>
    [DataField]
    public Dictionary<Direction, Vector2> DirectionOffsets = new();

    [DataField]
    public TimeSpan BrakeHold = TimeSpan.FromSeconds(0.3);

    public float LastSpeed;

    public TimeSpan BrakeUntil;
}
