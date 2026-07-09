using Content.Shared.Body.Part;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUAutodocPodComponent : Component
{
    public const string BodyContainerId = "cmu-autodoc-bodyContainer";

    [DataField]
    public float StepDelay = 45f;

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    [ViewVariables]
    public readonly List<CMUAutodocQueuedStep> Queue = new();

    [ViewVariables]
    public bool IsRunning;

    [ViewVariables]
    public EntityUid Operator;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextStepAt;

    [ViewVariables]
    public string? CurrentStep;
}

[RegisterComponent]
public sealed partial class CMUAutodocConsoleComponent : Component
{
    [DataField]
    public float LinkRange = 4f;
}

[RegisterComponent]
public sealed partial class CMUBodyScannerPodComponent : Component
{
    public const string BodyContainerId = "cmu-body-scanner-bodyContainer";

    [DataField]
    public TimeSpan EntryDelay = TimeSpan.FromSeconds(2);

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;
}

[RegisterComponent]
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

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUBodyScannerSurgerySpeedComponent : Component
{
    [DataField]
    public EntityUid Patient;

    [DataField, AutoPausedField]
    public TimeSpan ExpiresAt;

    [DataField]
    public float DelayMultiplier = 0.5f;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMUBodyScannerCalibrationLockoutComponent : Component
{
    [ViewVariables]
    public EntityUid Patient;

    [ViewVariables, AutoPausedField]
    public TimeSpan ExpiresAt;
}

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class CMULimbPrinterComponent : Component
{
    public const string BeakerSlotId = "cmu-limb-printer-beakerSlot";
    public const string SyringeSlotId = "cmu-limb-printer-syringeSlot";
    public const string MaterialSlotId = "cmu-limb-printer-materialSlot";

    [DataField]
    public ProtoId<ReagentPrototype> SynthesisReagent = "CMUBiogenicMatrix";

    [DataField]
    public FixedPoint2 SynthesisCost = FixedPoint2.New(30);

    [DataField]
    public FixedPoint2 BloodCost = FixedPoint2.New(7.5);

    [DataField]
    public ProtoId<StackPrototype> RoboticMetalStack = "CMSteel";

    [DataField]
    public int RoboticMetalCost = 15;

    [DataField]
    public EntProtoId LeftArmPrototype = "CMUPartHumanLeftArm";

    [DataField]
    public EntProtoId RightArmPrototype = "CMUPartHumanRightArm";

    [DataField]
    public EntProtoId LeftLegPrototype = "CMUPartHumanLeftLeg";

    [DataField]
    public EntProtoId RightLegPrototype = "CMUPartHumanRightLeg";

    [DataField]
    public EntProtoId RoboticLeftArmPrototype = "CMUPartRoboticLeftArm";

    [DataField]
    public EntProtoId RoboticRightArmPrototype = "CMUPartRoboticRightArm";

    [DataField]
    public EntProtoId RoboticLeftLegPrototype = "CMUPartRoboticLeftLeg";

    [DataField]
    public EntProtoId RoboticRightLegPrototype = "CMUPartRoboticRightLeg";

    [ViewVariables, AutoPausedField]
    public TimeSpan WorkingUntil;
}

public sealed record CMUAutodocQueuedStep(
    EntityUid Part,
    BodyPartType Type,
    BodyPartSymmetry Symmetry,
    string SurgeryId,
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
