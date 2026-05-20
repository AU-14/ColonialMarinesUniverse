using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Surgery;

[Serializable, NetSerializable]
public sealed partial class CMUSurgeryStepDoAfterEvent : DoAfterEvent
{
    public readonly string SurgeryId;
    public readonly string LeafSurgeryId;
    public readonly int StepIndex;
    public readonly BodyPartType TargetPartType;
    public readonly BodyPartSymmetry TargetSymmetry;

    public CMUSurgeryStepDoAfterEvent(
        string surgeryId,
        string leafSurgeryId,
        int stepIndex,
        BodyPartType targetPartType,
        BodyPartSymmetry targetSymmetry)
    {
        SurgeryId = surgeryId;
        LeafSurgeryId = leafSurgeryId;
        StepIndex = stepIndex;
        TargetPartType = targetPartType;
        TargetSymmetry = targetSymmetry;
    }

    public override DoAfterEvent Clone()
    {
        return new CMUSurgeryStepDoAfterEvent(
            SurgeryId,
            LeafSurgeryId,
            StepIndex,
            TargetPartType,
            TargetSymmetry);
    }
}
