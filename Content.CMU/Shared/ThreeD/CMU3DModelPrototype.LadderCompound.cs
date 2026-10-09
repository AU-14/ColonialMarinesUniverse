using System.Numerics;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>
    /// Inferred companion poses for the explicitly authored Down2 ladder assembly. These do not
    /// create prototype mappings. The complete current source composition and aperture must be admitted together.
    /// </summary>
    [DataField]
    public List<CMU3DFloorOpeningCompanion> FloorOpeningCompanions = [];
}

[DataDefinition]
public sealed partial class CMU3DFloorOpeningCompanion
{
    [DataField(required: true)] public string Prototype = string.Empty;
    [DataField(required: true)] public string Model = string.Empty;
    [DataField(required: true)] public string ReferenceRsi = string.Empty;
    [DataField(required: true)] public string ReferenceState = string.Empty;
    [DataField(required: true)] public int ReferenceDirection;
    [DataField(required: true)] public int SourceDirections;
    [DataField(required: true)] public int SourceFrame;
    [DataField(required: true)] public Vector2 SourceSpriteOffset;
    [DataField(required: true)] public bool SourceNoRotation;
    [DataField(required: true)] public bool SourceSnapCardinals;

    /// <summary>Degrees of source entity yaw relative to the ladder's rendered yaw. Never rotates the entity.</summary>
    [DataField(required: true)] public float SourceYaw;

    /// <summary>Degrees of existing companion render yaw relative to the ladder's rendered yaw.</summary>
    [DataField(required: true)] public float RenderYaw;

    /// <summary>Required existing presentation offset after ordinary support placement; applied exactly once.</summary>
    [DataField(required: true)] public Vector3 RenderOffset;

    /// <summary>Companion model-local geometry; it retains that companion's existing render yaw and support offset.</summary>
    [DataField(required: true)] public List<CMU3DModelPart> Parts = [];
}
