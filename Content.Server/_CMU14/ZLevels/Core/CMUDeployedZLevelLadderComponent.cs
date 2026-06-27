using Content.Shared.Physics;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.ZLevels.Core;

[RegisterComponent]
[Access(typeof(CMUDeployableZLevelLadderSystem))]
public sealed partial class CMUDeployedZLevelLadderComponent : Component
{
    [DataField]
    public EntityUid? OtherLadder;

    [DataField]
    public EntProtoId PackedPrototype = "CMUDeployableZLevelLadder";

    [DataField]
    public bool Retractable = true;

    [DataField]
    public CollisionGroup SupportCollisionMask = CMUDeployableZLevelLadderComponent.DefaultSupportCollisionMask;

    [DataField]
    public float UnsupportedCollapseDelay = 2f;

    [DataField]
    public bool UnsupportedCollapse;

    [DataField]
    public bool ReturnPackedOnUnsupportedCollapse = true;

    [DataField]
    public float UnsupportedShakeDegrees = 5f;

    [DataField]
    public float UnsupportedShakeInterval = 0.12f;

    public TimeSpan NextUnsupportedShake;

    public bool UnsupportedShakePositive;

    public Angle UnsupportedOriginalRotation;
}
