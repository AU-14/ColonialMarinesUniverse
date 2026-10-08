using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>
    /// Optional opaque bottom Box whose center may select a support after the source pivot misses.
    /// Restricted to a static rotating sprite pose; affects render height without moving source X/Y.
    /// </summary>
    [DataField]
    public string? SupportProbePart;
}
