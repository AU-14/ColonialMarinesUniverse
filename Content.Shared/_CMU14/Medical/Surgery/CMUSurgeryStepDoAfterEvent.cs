using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Surgery;

[Serializable, NetSerializable]
public sealed partial class CMUSurgeryStepDoAfterEvent : SimpleDoAfterEvent
{
    public readonly string SurgeryId;
    public readonly int StepIndex;

    public CMUSurgeryStepDoAfterEvent(string surgeryId, int stepIndex)
    {
        SurgeryId = surgeryId;
        StepIndex = stepIndex;
    }
}
