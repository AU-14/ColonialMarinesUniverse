using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.Replicant;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ReplicantComponent : Component
{
    /// <summary>
    /// Set once the identity copy has completed; the assume-identity action and picker
    /// are spent.
    /// </summary>
    [AutoNetworkedField]
    public bool Transformed;

    /// <summary>
    /// The copied original, kept as a name for round summaries because the entity may not
    /// survive the round.
    /// </summary>
    [AutoNetworkedField]
    public string? TargetName;

    /// <summary>
    /// Target chosen in the picker, held while the transform channel runs.
    /// </summary>
    [DataField]
    public NetEntity? PendingTarget;

    /// <summary>
    /// How long the copy takes once a target is chosen; the dark-alley window.
    /// </summary>
    [DataField]
    public TimeSpan ChannelDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public EntityUid? Action;
}

public sealed partial class ReplicantTransformActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class ReplicantTransformDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum ReplicantUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ReplicantTargetInfo
{
    public NetEntity Entity;
    public string Name;

    public ReplicantTargetInfo(NetEntity entity, string name)
    {
        Entity = entity;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class ReplicantPickerState : BoundUserInterfaceState
{
    public List<ReplicantTargetInfo> Targets;

    public ReplicantPickerState(List<ReplicantTargetInfo> targets)
    {
        Targets = targets;
    }
}

[Serializable, NetSerializable]
public sealed class ReplicantPickedMessage : BoundUserInterfaceMessage
{
    public NetEntity Target;

    public ReplicantPickedMessage(NetEntity target)
    {
        Target = target;
    }
}
