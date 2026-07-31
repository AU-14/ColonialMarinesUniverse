using System.Collections.Generic;
using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

[Serializable, NetSerializable]
public enum CMUSurgeryUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUSurgeryBuiState : BoundUserInterfaceState
{
    public NetEntity Patient;
    public string PatientName;
    public List<CMUSurgeryPartEntry> Parts;
    public CMUArmedStepInfo? CurrentArmedStep;
    public CMUSurgeryInFlightInfo? InFlight;
    public CMUSurgerySessionId? SessionId;
    public CMUSurgeryAttemptToken? ActiveAttempt;
    public CMUSurgeryArmedStateId? ArmedStateId;
    public CMUSurgerySessionPhase? SessionPhase;
    public BodyPartType? SessionPartType;
    public BodyPartSymmetry? SessionPartSymmetry;
    public ulong ViewRevision;
    public bool CanAbandon;

    public CMUSurgeryBuiState(
        NetEntity patient,
        string patientName,
        List<CMUSurgeryPartEntry> parts,
        CMUArmedStepInfo? currentArmedStep,
        CMUSurgeryInFlightInfo? inFlight,
        CMUSurgerySessionId? sessionId,
        CMUSurgeryAttemptToken? activeAttempt,
        CMUSurgeryArmedStateId? armedStateId,
        CMUSurgerySessionPhase? sessionPhase,
        BodyPartType? sessionPartType,
        BodyPartSymmetry? sessionPartSymmetry)
    {
        Patient = patient;
        PatientName = patientName;
        Parts = parts;
        CurrentArmedStep = currentArmedStep;
        InFlight = inFlight;
        SessionId = sessionId;
        ActiveAttempt = activeAttempt;
        ArmedStateId = armedStateId;
        SessionPhase = sessionPhase;
        SessionPartType = sessionPartType;
        SessionPartSymmetry = sessionPartSymmetry;
    }
}

[Serializable, NetSerializable]
public sealed record CMUSurgeryInFlightInfo(
    NetEntity Part,
    string PartDisplayName,
    string LeafSurgeryId,
    string LeafSurgeryDisplayName,
    string SurgeonName,
    TimeSpan StartedAt);

[Serializable, NetSerializable]
public sealed record CMUSurgeryPartEntry(
    NetEntity Part,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    string DisplayName,
    string ConditionSummary,
    bool IsInFlightHere,
    bool LockedByOtherPart,
    List<CMUSurgeryEntry> EligibleSurgeries);

[Serializable, NetSerializable]
public sealed record CMUSurgeryEntry(
    string SurgeryId,
    string DisplayName,
    string NextStepLabel,
    string? NextStepToolCategory,
    int NextStepIndex,
    int TotalSteps,
    string? GatingSurgeryId,
    string Category);

[Serializable, NetSerializable]
public sealed record CMUArmedStepInfo(
    string SurgeryId,
    string SurgeryDisplayName,
    int StepIndex,
    string StepLabel,
    string? ToolCategory);

[Serializable, NetSerializable]
public sealed class CMUSurgeryArmStepMessage : BoundUserInterfaceMessage
{
    public NetEntity Part;
    public NetEntity Patient;
    public BodyPartType TargetPartType;
    public BodyPartSymmetry TargetSymmetry;
    public string SurgeryId;
    public int StepIndex;
    public CMUSurgerySessionId? ExpectedSession;
    public CMUSurgeryAttemptToken? ExpectedAttempt;
    public CMUSurgeryArmedStateId? ExpectedArmedState;
    public ulong ExpectedViewRevision;

    public CMUSurgeryArmStepMessage(
        NetEntity patient,
        NetEntity part,
        BodyPartType type,
        BodyPartSymmetry symmetry,
        string surgeryId,
        int stepIndex,
        CMUSurgerySessionId? expectedSession,
        CMUSurgeryAttemptToken? expectedAttempt,
        CMUSurgeryArmedStateId? expectedArmedState,
        ulong expectedViewRevision)
    {
        Patient = patient;
        Part = part;
        TargetPartType = type;
        TargetSymmetry = symmetry;
        SurgeryId = surgeryId;
        StepIndex = stepIndex;
        ExpectedSession = expectedSession;
        ExpectedAttempt = expectedAttempt;
        ExpectedArmedState = expectedArmedState;
        ExpectedViewRevision = expectedViewRevision;
    }
}

[Serializable, NetSerializable]
public sealed class CMUSurgeryClearArmedMessage : BoundUserInterfaceMessage
{
    public CMUSurgerySessionId? ExpectedSession;
    public NetEntity Patient;
    public CMUSurgeryAttemptToken? ExpectedAttempt;
    public CMUSurgeryArmedStateId? ExpectedArmedState;
    public ulong ExpectedViewRevision;

    public CMUSurgeryClearArmedMessage(
        NetEntity patient,
        CMUSurgerySessionId? expectedSession,
        CMUSurgeryAttemptToken? expectedAttempt,
        CMUSurgeryArmedStateId? expectedArmedState,
        ulong expectedViewRevision)
    {
        Patient = patient;
        ExpectedSession = expectedSession;
        ExpectedAttempt = expectedAttempt;
        ExpectedArmedState = expectedArmedState;
        ExpectedViewRevision = expectedViewRevision;
    }
}

public readonly record struct CMUResolvedStep(
    string ResolvedSurgeryId,
    int StepIndex,
    string StepLabel,
    string? ToolCategory,
    int TotalSteps,
    string? GatingSurgeryId)
{
    public int AbsoluteStepIndex => StepIndex;
}
