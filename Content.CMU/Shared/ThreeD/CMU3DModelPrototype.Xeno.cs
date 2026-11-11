using System.Numerics;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Audited hive layers, keyed by source RSI then state. Empty frames represent transparent source layers.</summary>
    [DataField]
    public Dictionary<string, Dictionary<string, CMU3DSpriteState>> XenoStates = [];

    [DataField]
    public Vector2 XenoSpriteOffset;
}
