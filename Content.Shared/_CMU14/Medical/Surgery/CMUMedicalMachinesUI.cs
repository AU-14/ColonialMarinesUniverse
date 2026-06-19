using System.Collections.Generic;
using Content.Shared.Body.Part;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Surgery;

[Serializable, NetSerializable]
public enum CMUAutodocUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUBodyScannerUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMULimbPrinterUIKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum CMUAutodocVisuals : byte
{
    Operating,
}

[Serializable, NetSerializable]
public enum CMULimbPrinterVisuals : byte
{
    Working,
}

[Serializable, NetSerializable]
public enum CMUMedicalPodVisuals : byte
{
    Occupied,
}

[Serializable, NetSerializable]
public sealed class CMUAutodocBuiState : BoundUserInterfaceState
{
    public NetEntity? Pod;
    public NetEntity? Patient;
    public string PatientName;
    public bool PodLinked;
    public bool CanQueue;
    public bool Running;
    public string Status;
    public string? CurrentStep;
    public TimeSpan? NextStepAt;
    public List<CMUSurgeryPartEntry> Parts;
    public List<CMUAutodocQueueEntry> Queue;

    public CMUAutodocBuiState(
        NetEntity? pod,
        NetEntity? patient,
        string patientName,
        bool podLinked,
        bool canQueue,
        bool running,
        string status,
        string? currentStep,
        TimeSpan? nextStepAt,
        List<CMUSurgeryPartEntry> parts,
        List<CMUAutodocQueueEntry> queue)
    {
        Pod = pod;
        Patient = patient;
        PatientName = patientName;
        PodLinked = podLinked;
        CanQueue = canQueue;
        Running = running;
        Status = status;
        CurrentStep = currentStep;
        NextStepAt = nextStepAt;
        Parts = parts;
        Queue = queue;
    }
}

[Serializable, NetSerializable]
public sealed record CMUAutodocQueueEntry(
    int Index,
    NetEntity Part,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    string PartDisplayName,
    string SurgeryId,
    string SurgeryDisplayName,
    string Category,
    int StepIndex,
    string StepLabel,
    float DurationSeconds);

[Serializable, NetSerializable]
public sealed class CMUAutodocQueueStepMessage : BoundUserInterfaceMessage
{
    public NetEntity Part;
    public BodyPartType TargetPartType;
    public BodyPartSymmetry TargetSymmetry;
    public string SurgeryId;
    public int StepIndex;

    public CMUAutodocQueueStepMessage(NetEntity part, BodyPartType type, BodyPartSymmetry symmetry, string surgeryId, int stepIndex)
    {
        Part = part;
        TargetPartType = type;
        TargetSymmetry = symmetry;
        SurgeryId = surgeryId;
        StepIndex = stepIndex;
    }
}

[Serializable, NetSerializable]
public sealed class CMUAutodocRemoveQueueStepMessage : BoundUserInterfaceMessage
{
    public int Index;

    public CMUAutodocRemoveQueueStepMessage(int index)
    {
        Index = index;
    }
}

[Serializable, NetSerializable]
public sealed class CMUAutodocClearQueueMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMUAutodocStartMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMUAutodocStopMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMUAutodocEjectPatientMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMUBodyScannerBuiState : BoundUserInterfaceState
{
    public NetEntity? Pod;
    public NetEntity? Patient;
    public string PatientName;
    public bool PodLinked;
    public bool CanScan;
    public bool PuzzleComplete;
    public string Status;
    public TimeSpan? BoostExpiresAt;
    public TimeSpan? CalibrationLockoutExpiresAt;
    public TimeSpan? CalibrationStartedAt;
    public TimeSpan? CalibrationEndsAt;
    public TimeSpan? PulseStartedAt;
    public float PulsePeriod;
    public float PulseTargetPhase;
    public float PulseWindowSize;
    public float PulseGraceSize;
    public TimeSpan? LastPenaltyAt;
    public float LastPenaltySeconds;
    public TimeSpan? LastFeedbackAt;
    public CMUBodyScannerFeedbackKind LastFeedbackKind;
    public List<CMUBodyScannerScanLine> ScanLines;
    public List<CMUBodyScannerPuzzleChoice> Terms;
    public List<CMUBodyScannerSliceSignal> Targets;
    public List<CMUBodyScannerPuzzleAssignment> Assignments;

    public CMUBodyScannerBuiState(
        NetEntity? pod,
        NetEntity? patient,
        string patientName,
        bool podLinked,
        bool canScan,
        bool puzzleComplete,
        string status,
        TimeSpan? boostExpiresAt,
        TimeSpan? calibrationLockoutExpiresAt,
        TimeSpan? calibrationStartedAt,
        TimeSpan? calibrationEndsAt,
        TimeSpan? pulseStartedAt,
        float pulsePeriod,
        float pulseTargetPhase,
        float pulseWindowSize,
        float pulseGraceSize,
        TimeSpan? lastPenaltyAt,
        float lastPenaltySeconds,
        TimeSpan? lastFeedbackAt,
        CMUBodyScannerFeedbackKind lastFeedbackKind,
        List<CMUBodyScannerScanLine> scanLines,
        List<CMUBodyScannerPuzzleChoice> terms,
        List<CMUBodyScannerSliceSignal> targets,
        List<CMUBodyScannerPuzzleAssignment> assignments)
    {
        Pod = pod;
        Patient = patient;
        PatientName = patientName;
        PodLinked = podLinked;
        CanScan = canScan;
        PuzzleComplete = puzzleComplete;
        Status = status;
        BoostExpiresAt = boostExpiresAt;
        CalibrationLockoutExpiresAt = calibrationLockoutExpiresAt;
        CalibrationStartedAt = calibrationStartedAt;
        CalibrationEndsAt = calibrationEndsAt;
        PulseStartedAt = pulseStartedAt;
        PulsePeriod = pulsePeriod;
        PulseTargetPhase = pulseTargetPhase;
        PulseWindowSize = pulseWindowSize;
        PulseGraceSize = pulseGraceSize;
        LastPenaltyAt = lastPenaltyAt;
        LastPenaltySeconds = lastPenaltySeconds;
        LastFeedbackAt = lastFeedbackAt;
        LastFeedbackKind = lastFeedbackKind;
        ScanLines = scanLines;
        Terms = terms;
        Targets = targets;
        Assignments = assignments;
    }
}

[Serializable, NetSerializable]
public sealed record CMUBodyScannerPuzzleChoice(string Id, string Text);

[Serializable, NetSerializable]
public enum CMUBodyScannerFeedbackKind : byte
{
    None,
    Correct,
    WrongTiming,
    WrongLayer,
}

[Serializable, NetSerializable]
public sealed record CMUBodyScannerSliceSignal(string Id, string LayerId, string Text, string Detail, bool IsDecoy = false);

[Serializable, NetSerializable]
public enum CMUBodyScannerScanCategory : byte
{
    Vitals,
    Body,
    Organs,
}

[Serializable, NetSerializable]
public sealed record CMUBodyScannerScanLine(CMUBodyScannerScanCategory Category, string Text);

[Serializable, NetSerializable]
public sealed record CMUBodyScannerPuzzleAssignment(string LayerId, string SignalId);

[Serializable, NetSerializable]
public sealed class CMUBodyScannerConfirmPuzzleMessage : BoundUserInterfaceMessage
{
    public string LayerId;
    public string SignalId;
    public float ClientPhase;

    public CMUBodyScannerConfirmPuzzleMessage(string layerId, string signalId, float clientPhase)
    {
        LayerId = layerId;
        SignalId = signalId;
        ClientPhase = clientPhase;
    }
}

[Serializable, NetSerializable]
public sealed class CMUBodyScannerResetPuzzleMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMUBodyScannerEjectPatientMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMULimbPrinterBuiState : BoundUserInterfaceState
{
    public string Status;
    public string SynthesisReagentName;
    public string? BeakerName;
    public string? SyringeName;
    public float SynthesisUnits;
    public float SynthesisMaxUnits;
    public float BloodUnits;
    public float BloodMaxUnits;
    public float SynthesisCost;
    public float BloodCost;
    public TimeSpan? WorkingUntil;
    public List<CMULimbPrinterOption> Options;

    public CMULimbPrinterBuiState(
        string status,
        string synthesisReagentName,
        string? beakerName,
        string? syringeName,
        float synthesisUnits,
        float synthesisMaxUnits,
        float bloodUnits,
        float bloodMaxUnits,
        float synthesisCost,
        float bloodCost,
        TimeSpan? workingUntil,
        List<CMULimbPrinterOption> options)
    {
        Status = status;
        SynthesisReagentName = synthesisReagentName;
        BeakerName = beakerName;
        SyringeName = syringeName;
        SynthesisUnits = synthesisUnits;
        SynthesisMaxUnits = synthesisMaxUnits;
        BloodUnits = bloodUnits;
        BloodMaxUnits = bloodMaxUnits;
        SynthesisCost = synthesisCost;
        BloodCost = bloodCost;
        WorkingUntil = workingUntil;
        Options = options;
    }
}

[Serializable, NetSerializable]
public sealed record CMULimbPrinterOption(
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    string Name,
    string Prototype,
    bool CanPrint,
    string DisabledReason);

[Serializable, NetSerializable]
public sealed class CMULimbPrinterPrintMessage : BoundUserInterfaceMessage
{
    public BodyPartType Type;
    public BodyPartSymmetry Symmetry;

    public CMULimbPrinterPrintMessage(BodyPartType type, BodyPartSymmetry symmetry)
    {
        Type = type;
        Symmetry = symmetry;
    }
}

[Serializable, NetSerializable]
public sealed class CMULimbPrinterEjectBeakerMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CMULimbPrinterEjectSyringeMessage : BoundUserInterfaceMessage
{
}
