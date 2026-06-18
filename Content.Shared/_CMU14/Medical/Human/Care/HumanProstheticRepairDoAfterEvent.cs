using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Care;

[Serializable, NetSerializable]
public sealed partial class HumanProstheticRepairDoAfterEvent : DoAfterEvent
{
    [DataField]
    public TreatmentAttempt Attempt;

    public HumanProstheticRepairDoAfterEvent(TreatmentAttempt attempt)
    {
        Attempt = attempt;
    }

    public HumanProstheticRepairDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new HumanProstheticRepairDoAfterEvent(Attempt);
}
