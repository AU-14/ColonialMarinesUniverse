using System.Numerics;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.CMU14.ThreeD;

public sealed partial class CMU3DModelPrototype
{
    /// <summary>Exact terrain source prototypes whose presentation solids may clear this fixture's authored alcove.</summary>
    [DataField]
    public ProtoId<EntityPrototype>[] TerrainCutoutTargets = [];

    /// <summary>Fixture-local bounds in tiles. This changes presentation geometry only, never gameplay fixtures.</summary>
    [DataField]
    public Vector3 TerrainCutoutMin;

    [DataField]
    public Vector3 TerrainCutoutMax;
}
