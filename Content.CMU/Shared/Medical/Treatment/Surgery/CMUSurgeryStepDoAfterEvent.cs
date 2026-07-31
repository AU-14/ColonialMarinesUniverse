using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

[Serializable, NetSerializable]
public sealed partial class CMUSurgeryStepDoAfterEvent : DoAfterEvent
{
    public readonly CMUSurgeryAttemptToken Attempt;
    public readonly string SurgeryId;
    public readonly string LeafSurgeryId;
    public readonly int StepIndex;
    public readonly EntProtoId<CMSurgeryStepComponent> StepId;
    public readonly BodyPartType TargetPartType;
    public readonly BodyPartSymmetry TargetSymmetry;

    public CMUSurgeryStepDoAfterEvent(
        CMUSurgeryAttemptToken attempt,
        string surgeryId,
        string leafSurgeryId,
        int stepIndex,
        EntProtoId<CMSurgeryStepComponent> stepId,
        BodyPartType targetPartType,
        BodyPartSymmetry targetSymmetry)
    {
        Attempt = attempt;
        SurgeryId = surgeryId;
        LeafSurgeryId = leafSurgeryId;
        StepIndex = stepIndex;
        StepId = stepId;
        TargetPartType = targetPartType;
        TargetSymmetry = targetSymmetry;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUSurgeryStepDoAfterEvent(
            Attempt,
            SurgeryId,
            LeafSurgeryId,
            StepIndex,
            StepId,
            TargetPartType,
            TargetSymmetry);
    }
}
