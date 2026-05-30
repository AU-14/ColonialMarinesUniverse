using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.ZLevels.Ghost;

/// <summary>
/// component that allows you to quickly move between Z levels
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUZLevelGhostMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "CMUActionZLevelUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelUpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "CMUActionZLevelDown";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelDownActionEntity;
}

/// <summary>
/// Should be relayed upon using the action.
/// </summary>
public sealed partial class CMUZLevelActionUp : InstantActionEvent
{
}

/// <summary>
/// Should be relayed upon using the action.
/// </summary>
public sealed partial class CMUZLevelActionDown : InstantActionEvent
{
}
