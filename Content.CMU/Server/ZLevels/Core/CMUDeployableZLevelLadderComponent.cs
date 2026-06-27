using Content.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.ZLevels.Core;

[RegisterComponent]
[Access(typeof(CMUDeployableZLevelLadderSystem))]
public sealed partial class CMUDeployableZLevelLadderComponent : Component
{
    public const CollisionGroup DefaultSupportCollisionMask =
        CollisionGroup.Impassable |
        CollisionGroup.HighImpassable |
        CollisionGroup.BarricadeImpassable |
        CollisionGroup.DropshipImpassable;

    [DataField]
    public EntProtoId UpLadderPrototype = "CMUZLevelLadderThroughUp3";

    [DataField]
    public EntProtoId DownLadderPrototype = "CMUZLevelLadderThroughDown3";

    [DataField]
    public EntProtoId? PackedPrototype;

    [DataField]
    public CollisionGroup SupportCollisionMask = DefaultSupportCollisionMask;

    [DataField]
    public float UnsupportedCollapseDelay = 2f;

    [DataField]
    public bool ReturnPackedOnUnsupportedCollapse = true;

    [DataField]
    public float UnsupportedShakeDegrees = 5f;

    [DataField]
    public float UnsupportedShakeInterval = 0.12f;
}
