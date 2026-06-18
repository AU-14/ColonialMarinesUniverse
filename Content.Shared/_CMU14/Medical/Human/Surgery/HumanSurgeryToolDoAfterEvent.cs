using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Human.Surgery;

[Serializable, NetSerializable]
public sealed partial class HumanSurgeryToolDoAfterEvent : DoAfterEvent
{
    [DataField]
    public SurgeryAttempt Attempt;

    public HumanSurgeryToolDoAfterEvent(SurgeryAttempt attempt)
    {
        Attempt = attempt;
    }

    public HumanSurgeryToolDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new HumanSurgeryToolDoAfterEvent(Attempt);
}
