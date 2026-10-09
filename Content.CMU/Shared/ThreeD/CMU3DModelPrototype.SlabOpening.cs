using System.Numerics;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Inferred presentation-only aperture in the local floor slab; gameplay tiles remain unchanged.</summary>
    [DataField]
    public CMU3DSlabOpeningBounds? FloorOpening;

    /// <summary>Inferred presentation-only aperture in the local ceiling slab; gameplay roofs remain unchanged.</summary>
    [DataField]
    public CMU3DSlabOpeningBounds? CeilingOpening;

    /// <summary>Retain source grating above a recessed service opening.</summary>
    [DataField]
    public bool PreserveSlabCladding;
}

[DataDefinition]
public sealed partial class CMU3DSlabOpeningBounds
{
    /// <summary>Entity-local XY minimum, contained in the centered unit tile.</summary>
    [DataField(required: true)]
    public Vector2 Min;

    /// <summary>Entity-local XY maximum, strictly greater than Min on both axes.</summary>
    [DataField(required: true)]
    public Vector2 Max;
}
