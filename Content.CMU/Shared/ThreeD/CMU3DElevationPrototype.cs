using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>Authored, grid-local presentation heights. Logical Z levels and collision remain source-owned.</summary>
[Prototype("cmu3DElevation")]
public sealed partial class CMU3DElevationPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public List<CMU3DElevationRegion> Regions = [];

    [DataField]
    public List<CMU3DElevationRamp> Ramps = [];

    /// <summary>Duplicate lower-map artwork for stairs whose landing is rendered on the map above.</summary>
    [DataField]
    public Dictionary<string, List<Vector2i>> SuppressedStairs = [];

    /// <summary>Open upper-floor tiles over an inter-floor stair flight.</summary>
    [DataField]
    public List<Vector2i> StairOpenings = [];
}

[DataDefinition]
public sealed partial class CMU3DElevationRegion
{
    [DataField(required: true)]
    public Box2 Bounds;

    [DataField(required: true)]
    public float Height;
}

[DataDefinition]
public sealed partial class CMU3DElevationRamp
{
    [DataField(required: true)]
    public Vector2i Tile;

    /// <summary>Ascent in grid coordinates, independent of camera and grid rotation.</summary>
    [DataField(required: true)]
    public Vector2i Direction;

    [DataField(required: true)]
    public float Bottom;

    [DataField(required: true)]
    public float Top;

    /// <summary>Empty for a terrain ramp; otherwise fits the named stair model at this tile.</summary>
    [DataField]
    public string SourcePrototype = string.Empty;

    /// <summary>False for the camera-only copy on the other logical map.</summary>
    [DataField]
    public bool Geometry = true;

    /// <summary>Source physics support along the ascent; residual height preserves jumps and falls.</summary>
    [DataField]
    public List<float> PhysicsCurve = [];

    [DataField]
    public float PhysicsOffset;
}
