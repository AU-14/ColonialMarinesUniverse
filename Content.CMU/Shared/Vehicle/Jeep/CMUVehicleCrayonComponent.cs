using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Crayon drawings stored on a vehicle's body: each is anchored to a point on a panel, so it stays on
/// the same spot as the vehicle turns and only shows where that panel faces the camera.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMUVehicleCrayonComponent : Component
{
    /// <summary>
    /// Surface lookup written by the jeep generator: per pixel and direction, the body voxel and face shown.
    /// </summary>
    [DataField(required: true)]
    public ResPath Map;

    [DataField]
    public int MaxDrawings = 50;

    [DataField, AutoNetworkedField]
    public List<CMUCrayonDrawing> Drawings = new();

    /// <summary>
    /// Blood splashed on the body, stored like the drawings and drawn under them.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CMUCrayonDrawing> Blood = new();

    [DataField]
    public int MaxBlood = 40;
}

/// <summary>
/// Hinged panel a crayon map pixel is on. The map has them shut, so paint on one hides while it is open.
/// </summary>
public enum CMUCrayonPanel : byte
{
    Body,
    Hood,
    FuelDoor,
    DriverDoor,
    PassengerDoor,
    Tailgate,
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CMUCrayonDrawing
{
    [DataField]
    public string Decal = string.Empty;

    [DataField]
    public Color Color = Color.White;

    /// <summary>
    /// Anchor voxel (forward, right, up) in vehicle space.
    /// </summary>
    [DataField]
    public Vector3 Anchor;

    /// <summary>
    /// Panel the drawing is on: up, +f, -f, +r or -r.
    /// </summary>
    [DataField]
    public string Normal = "up";

    /// <summary>
    /// The decal's right and down axes in vehicle space, from the direction it was drawn in.
    /// </summary>
    [DataField]
    public Vector3 Right;

    [DataField]
    public Vector3 Down;
}
