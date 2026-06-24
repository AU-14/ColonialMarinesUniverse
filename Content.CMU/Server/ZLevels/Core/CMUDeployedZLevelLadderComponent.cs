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
}
