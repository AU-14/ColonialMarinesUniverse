using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

/// <summary>
/// Approximate per-variant floor colors sampled from the existing tile sprite.
/// This bridges the offline asset inventory to the native 3D inspection view.
/// Source metadata records attribution references without granting a new license.
/// </summary>
[Prototype("cmu3DTileMaterial")]
public sealed partial class CMU3DTileMaterialPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField(required: true)]
    public string Tile = string.Empty;

    [DataField(required: true)]
    public Color[] Colors = [];

    [DataField]
    public string SourceSprite = string.Empty;

    [DataField]
    public string[] SourceMetadata = [];

    [DataField]
    public string Description = string.Empty;
}
