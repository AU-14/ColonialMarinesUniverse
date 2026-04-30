using System.Collections.Generic;
using Content.Shared.Body.Part;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Surgery;

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

    public CMUSurgeryBuiState(
        NetEntity patient,
        string patientName,
        List<CMUSurgeryPartEntry> parts,
        CMUArmedStepInfo? currentArmedStep,
        CMUSurgeryInFlightInfo? inFlight)
    {
        Patient = patient;
        PatientName = patientName;
        Parts = parts;
        CurrentArmedStep = currentArmedStep;
        InFlight = inFlight;
    }
}

[Serializable, NetSerializable]
public sealed record CMUSurgeryInFlightInfo(
    NetEntity Part,
    string PartDisplayName,
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
    public BodyPartType TargetPartType;
    public BodyPartSymmetry TargetSymmetry;
    public string SurgeryId;
    public int StepIndex;

    public CMUSurgeryArmStepMessage(NetEntity part, BodyPartType type, BodyPartSymmetry symmetry, string surgeryId, int stepIndex)
    {
        Part = part;
        TargetPartType = type;
        TargetSymmetry = symmetry;
        SurgeryId = surgeryId;
        StepIndex = stepIndex;
    }
}

[Serializable, NetSerializable]
public sealed class CMUSurgeryClearArmedMessage : BoundUserInterfaceMessage
{
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
