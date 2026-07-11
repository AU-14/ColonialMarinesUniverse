using Content.Shared._CMU14.Medical.Core;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

/// <summary>
///     Stable identity for one surgery session on a patient and logical body site.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct CMUSurgerySessionId(ulong Value);

/// <summary>
///     Correlates one active action with the session that authorized it.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct CMUSurgeryAttemptToken(CMUSurgerySessionId Session, uint Attempt);

/// <summary>
///     Identifies one exact compatibility-layer armed state. It changes when
///     a step is selected, re-selected, or advanced so delayed UI commands
///     cannot mutate a newer waiting state.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct CMUSurgeryArmedStateId(ulong Value);

[Serializable, NetSerializable]
public enum CMUSurgerySessionPhase : byte
{
    AwaitingAction,
    Performing,
    AwaitingDecision,
}

public enum CMUSurgeryAttemptStartResult : byte
{
    Started,
    Busy,
    SiteConflict,
    NotAuthoritative,
}

/// <summary>
///     Read-only view of server-owned surgery session state.
/// </summary>
public readonly record struct CMUSurgerySessionSnapshot(
    CMUSurgerySessionId Id,
    CMUMedicalBodyPartKey Site,
    EntProtoId<CMSurgeryComponent> Procedure,
    EntProtoId<CMSurgeryStepComponent> CurrentStep,
    CMUSurgerySessionPhase Phase,
    EntityUid? ActiveSurgeon,
    CMUSurgeryAttemptToken? ActiveAttempt,
    EntityUid? ActiveTarget);

/// <summary>
///     Raised on the patient when the surgeon for an active attempt disappears.
/// </summary>
[ByRefEvent]
public readonly record struct CMUSurgeryAttemptActorLostEvent(CMUSurgeryAttemptToken Attempt);

/// <summary>
///     Authoritative runtime state for a surgery. The session belongs to the
///     patient and logical site; only the short-lived active attempt has a surgeon.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUSurgerySessionSystem))]
public sealed partial class CMUSurgerySessionComponent : Component
{
    [ViewVariables]
    internal CMUSurgerySessionId Id;

    [ViewVariables]
    internal uint LastAttempt;

    [ViewVariables]
    internal CMUMedicalBodyPartKey Site;

    [ViewVariables]
    internal EntProtoId<CMSurgeryComponent> Procedure;

    [ViewVariables]
    internal EntProtoId<CMSurgeryStepComponent> CurrentStep;

    [ViewVariables]
    internal CMUSurgerySessionPhase Phase;

    [ViewVariables]
    internal CMUSurgeryAttemptToken? ActiveAttempt;

    [ViewVariables]
    internal EntityUid? ActiveSurgeon;

    [ViewVariables]
    internal EntityUid? ActiveTool;

    [ViewVariables]
    internal EntityUid? ActiveTarget;
}

/// <summary>
///     Reverse link that releases an active attempt if its surgeon is deleted.
/// </summary>
[RegisterComponent]
[Access(typeof(CMUSurgerySessionSystem))]
public sealed partial class CMUSurgeryAttemptActorComponent : Component
{
    [ViewVariables]
    internal EntityUid Patient;

    [ViewVariables]
    internal CMUSurgeryAttemptToken Attempt;
}
