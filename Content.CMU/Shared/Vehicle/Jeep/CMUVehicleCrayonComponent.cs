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

    /// <summary>
    /// The hinged panels the map's pixels can be on, by the number the map gives them (0 is the body,
    /// 1 the first panel here). The map has them shut, so paint on one hides while it is open.
    /// </summary>
    [DataField]
    public List<string> Panels = new() { "hood", "fuel_door", "door_driver", "door_passenger", "tailgate" };

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

    /// <summary>
    /// Free drawn crayon: a dot on each body face it covers, oldest first, drawn over the drawings.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<CMUCrayonDot> Dots = new();

    /// <summary>
    /// The most dots kept; past it the oldest go.
    /// </summary>
    [DataField]
    public int MaxDots = 1500;

    /// <summary>
    /// Dots of free drawing one crayon charge lasts for.
    /// </summary>
    [DataField]
    public int DotsPerCharge = 40;
}

/// <summary>
/// A dot of free drawn crayon on one face of a body voxel.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public partial record struct CMUCrayonDot
{
    /// <summary>
    /// The voxel (forward, right, up) in vehicle space.
    /// </summary>
    [DataField]
    public Vector3 Voxel;

    /// <summary>
    /// 0 for the voxel's top face, else one more than the index in
    /// <see cref="SharedCMUVehicleCrayonSystem.MapDirections"/> of the direction whose camera-facing
    /// panel it is on.
    /// </summary>
    [DataField]
    public byte Face;

    /// <summary>
    /// The crayon's colour.
    /// </summary>
    [DataField]
    public Color Color;
}

/// <summary>
/// A crayon set to draw freely on vehicles instead of stamping its symbol: dragged across a body it
/// leaves a line.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CMUCrayonFreeDrawComponent : Component
{
    /// <summary>
    /// Dots drawn since a charge was last used.
    /// </summary>
    [DataField]
    public int Drawn;
}

/// <summary>
/// Points a free drawing crayon was dragged through over a vehicle, in map space, in order. Each
/// batch starts where the last one ended, unless it starts a new stroke.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUCrayonStrokeEvent(NetEntity vehicle, List<Vector2> points, bool start) : EntityEventArgs
{
    public readonly NetEntity Vehicle = vehicle;
    public readonly List<Vector2> Points = points;
    public readonly bool Start = start;
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
