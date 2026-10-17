using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>Original surface artwork, packed without resampling in the bounded review atlas.</summary>
[Prototype("cmu3DSurface")]
public sealed partial class CMU3DSurfacePrototype : IPrototype
{
    public const ushort MaximumAtlasIndex = 4095;
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public ResPath Texture;

    /// <summary>Stable unique slot, 1..4095. Zero means an untextured part.</summary>
    [DataField(required: true)]
    public ushort AtlasIndex;
}

/// <summary>Planar coordinates: image right/up are +X/+Z, +X/+Y, or -Y/+Z respectively.</summary>
public enum CMU3DSurfaceAxis : byte
{
    XZ = 1,
    XY = 2,
    YZ = 3,
}
