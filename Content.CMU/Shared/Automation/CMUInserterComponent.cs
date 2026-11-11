using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.CMU14.Automation;

/// <summary>Moves an item from the tile behind the arm to the tile in front of it.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class CMUInserterComponent : Component
{
    public const string ClawContainerId = "cmu-inserter-claw";

    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>Seconds between transfers. A transfer can move one item or merge one stack.</summary>
    [DataField]
    public float Interval = 1f;

    /// <summary>Seconds between checks when no item could move, so passing belt items are not missed.</summary>
    [DataField]
    public float RetryInterval = 0.1f;

    [DataField, AutoNetworkedField]
    public CMUInserterPhase Phase;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan PhaseStartedAt;

    [DataField, AutoNetworkedField]
    public float PhaseDuration;

    /// <summary>Retains the transfer's direction if the player rotates the arm during a cycle.</summary>
    [DataField, AutoNetworkedField]
    public Angle TransferRotation;

    [DataField, AutoNetworkedField]
    public float PausedProgress;

    [ViewVariables]
    public ContainerSlot Claw = default!;

    [ViewVariables]
    public float Remaining;

    [ViewVariables]
    public float Next;
}

[Serializable, NetSerializable]
public enum CMUInserterPhase : byte
{
    Idle,
    Swinging,
    Waiting,
    Returning,
}
