using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

[Serializable, NetSerializable]
public sealed partial class HumanBleedClampTreatmentDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    public HumanBleedClampTreatmentDoAfterEvent(TreatmentAttempt attempt)
    {
        Attempt = attempt;
    }

    public HumanBleedClampTreatmentDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new HumanBleedClampTreatmentDoAfterEvent(Attempt);
}
