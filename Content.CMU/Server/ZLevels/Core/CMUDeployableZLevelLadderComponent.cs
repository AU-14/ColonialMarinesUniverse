using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.ZLevels.Core;

[RegisterComponent]
[Access(typeof(CMUDeployableZLevelLadderSystem))]
public sealed partial class CMUDeployableZLevelLadderComponent : Component
{
    [DataField]
    public EntProtoId UpLadderPrototype = "CMUZLevelLadderThroughUp3";

    [DataField]
    public EntProtoId DownLadderPrototype = "CMUZLevelLadderThroughDown3";

    [DataField]
    public EntProtoId? PackedPrototype;
}
