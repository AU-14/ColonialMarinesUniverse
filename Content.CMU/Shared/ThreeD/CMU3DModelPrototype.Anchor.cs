using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Explicit source Transform.Anchored pose; null opts out of the audited disposal pair.</summary>
    [DataField]
    public bool? Anchored;

    /// <summary>Reciprocal installed/loose assembly for the same exact disposal source.</summary>
    [DataField]
    public ProtoId<CMU3DModelPrototype>? AlternateAnchorModel;

}
