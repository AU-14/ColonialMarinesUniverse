using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

[Serializable, NetSerializable]
public sealed partial class HumanSplintTreatmentDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    public HumanSplintTreatmentDoAfterEvent(TreatmentAttempt attempt)
    {
        Attempt = attempt;
    }

    public HumanSplintTreatmentDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new HumanSplintTreatmentDoAfterEvent(Attempt);
}
