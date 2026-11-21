using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ZLevels.Ghost;

/// <summary>
/// component that allows you to quickly move between Z levels
/// </summary>
/// <remarks>
/// Despite the name, this is the generic Z up/down action mover, not a ghost feature: its system only grants the
/// two actions and calls <c>TryMoveUp</c>/<c>TryMoveDown</c> (same X/Y, next level). Any entity can carry it,
/// including a remote camera eye that is a session's <c>EyeComponent.Target</c> and has no <c>CMUZPhysics</c>.
/// Such an eye owns its own z-level view probes on its new level; the body's probes are separate.
/// </remarks>
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
