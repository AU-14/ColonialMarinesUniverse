using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

[Serializable, NetSerializable]
public sealed partial class HumanCastTreatmentDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    public HumanCastTreatmentDoAfterEvent(TreatmentAttempt attempt)
    {
        Attempt = attempt;
    }

    public HumanCastTreatmentDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new HumanCastTreatmentDoAfterEvent(Attempt);
}
