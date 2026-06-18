using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Machines;

[RegisterComponent, ComponentProtoName("CMUAutodocPod")]
public sealed partial class CMUAutodocPodComponent : Component
{
    public const string BodyContainerId = "cmu-autodoc-bodyContainer";

    [DataField]
    public float StepDelay = 8f;

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    [ViewVariables]
    public readonly List<CMUAutodocQueuedStep> Queue = new();

    [ViewVariables]
    public bool IsRunning;

    [ViewVariables]
    public EntityUid Operator;

    [ViewVariables]
    public TimeSpan NextStepAt;

    [ViewVariables]
    public string? CurrentStep;

    [ViewVariables]
    public SurgeryAttempt CurrentAttempt;
}

[RegisterComponent, ComponentProtoName("CMUAutodocConsole")]
public sealed partial class CMUAutodocConsoleComponent : Component
{
    [DataField]
    public float LinkRange = 4f;

    [ViewVariables]
    public EntityUid LastViewer;
}

[RegisterComponent, ComponentProtoName("CMUBodyScannerPod")]
public sealed partial class CMUBodyScannerPodComponent : Component
{
    public const string BodyContainerId = "cmu-body-scanner-bodyContainer";

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;
}

[RegisterComponent, ComponentProtoName("CMUBodyScannerConsole")]
public sealed partial class CMUBodyScannerConsoleComponent : Component
{
    [DataField]
    public float LinkRange = 4f;

    [DataField]
    public float BoostDurationSeconds = 600f;

    [DataField]
    public float CalibrationDurationSeconds = 120f;

    [DataField]
    public float CalibrationLockoutSeconds = 600f;

    [DataField]
    public float WrongMovePenaltySeconds = 8f;

    [DataField]
    public float PulsePeriodSeconds = 2.4f;

    [DataField]
    public float MinPulsePeriodSeconds = 1.35f;

    [DataField]
    public float PulseTargetPhase = 0.25f;

    [DataField]
    public float PulseTargetShiftPerLock = 0.19f;

    [DataField]
    public float PulseWindowSize = 0.2f;

    [DataField]
    public float MinPulseWindowSize = 0.09f;

    [DataField]
    public float PulseGraceSize = 0.1f;

    [ViewVariables]
    public EntityUid LastViewer;
}

[RegisterComponent]
public sealed partial class CMUBodyScannerPuzzleProgressComponent : Component
{
    [ViewVariables]
    public EntityUid Patient;

    [ViewVariables]
    public readonly List<CMUBodyScannerPuzzleAssignment> Assignments = new();

    [ViewVariables]
    public TimeSpan StartedAt;

    [ViewVariables]
    public TimeSpan EndsAt;

    [ViewVariables]
    public TimeSpan PulseStartedAt;

    [ViewVariables]
    public TimeSpan LastPenaltyAt;

    [ViewVariables]
    public float LastPenaltySeconds;

    [ViewVariables]
    public TimeSpan LastFeedbackAt;

    [ViewVariables]
    public CMUBodyScannerFeedbackKind LastFeedbackKind;
}

[RegisterComponent]
public sealed partial class CMUBodyScannerSurgerySpeedComponent : Component
{
    [DataField]
    public EntityUid Patient;

    [DataField]
    public TimeSpan ExpiresAt;

    [DataField]
    public float DelayMultiplier = 0.5f;
}

[RegisterComponent]
public sealed partial class CMUBodyScannerCalibrationLockoutComponent : Component
{
    [ViewVariables]
    public EntityUid Patient;

    [ViewVariables]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, ComponentProtoName("CMULimbPrinter")]
public sealed partial class CMULimbPrinterComponent : Component
{
    public const string BeakerSlotId = "cmu-limb-printer-beakerSlot";
    public const string SyringeSlotId = "cmu-limb-printer-syringeSlot";
    public const string MetalSlotId = "cmu-limb-printer-metalSlot";
    public const string CableSlotId = "cmu-limb-printer-cableSlot";

    [DataField]
    public ProtoId<ReagentPrototype> SynthesisReagent = "CMUBiogenicMatrix";

    [DataField]
    public ProtoId<StackPrototype> MetalStack = "Steel";

    [DataField]
    public ProtoId<StackPrototype> CableStack = "Cable";

    [DataField]
    public FixedPoint2 SynthesisCost = FixedPoint2.New(30);

    [DataField]
    public FixedPoint2 BloodCost = FixedPoint2.New(7.5);

    [DataField]
    public int MetalCost = 10;

    [DataField]
    public int CableCost = 10;

    [DataField]
    public EntProtoId LeftArmPrototype = "CMUPartHumanLeftArm";

    [DataField]
    public EntProtoId RightArmPrototype = "CMUPartHumanRightArm";

    [DataField]
    public EntProtoId LeftLegPrototype = "CMUPartHumanLeftLeg";

    [DataField]
    public EntProtoId RightLegPrototype = "CMUPartHumanRightLeg";

    [DataField]
    public EntProtoId LeftHandPrototype = "CMUPartHumanLeftHand";

    [DataField]
    public EntProtoId RightHandPrototype = "CMUPartHumanRightHand";

    [DataField]
    public EntProtoId LeftFootPrototype = "CMUPartHumanLeftFoot";

    [DataField]
    public EntProtoId RightFootPrototype = "CMUPartHumanRightFoot";

    [DataField]
    public EntProtoId RoboticLeftArmPrototype = "CMUPartRoboticLeftArm";

    [DataField]
    public EntProtoId RoboticRightArmPrototype = "CMUPartRoboticRightArm";

    [DataField]
    public EntProtoId RoboticLeftLegPrototype = "CMUPartRoboticLeftLeg";

    [DataField]
    public EntProtoId RoboticRightLegPrototype = "CMUPartRoboticRightLeg";

    [DataField]
    public EntProtoId RoboticLeftHandPrototype = "CMUPartRoboticLeftHand";

    [DataField]
    public EntProtoId RoboticRightHandPrototype = "CMUPartRoboticRightHand";

    [DataField]
    public EntProtoId RoboticLeftFootPrototype = "CMUPartRoboticLeftFoot";

    [DataField]
    public EntProtoId RoboticRightFootPrototype = "CMUPartRoboticRightFoot";

    [ViewVariables]
    public TimeSpan WorkingUntil;
}

[RegisterComponent, ComponentProtoName("CMUProstheticLimb")]
public sealed partial class CMUProstheticLimbComponent : Component
{
}

public sealed record CMUAutodocQueuedStep(
    EntityUid Part,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    BodyRegion Region,
    SurgeryProcedureId ProcedureId,
    string SurgeryDisplayName,
    string Category,
    int StepIndex,
    string StepLabel,
    string PartDisplayName,
    float DurationSeconds);

[RegisterComponent]
public sealed partial class CMUAutodocContainedPatientComponent : Component
{
}

[Serializable, NetSerializable]
public sealed partial class CMUMedicalPodInsertDoAfterEvent : SimpleDoAfterEvent
{
}
