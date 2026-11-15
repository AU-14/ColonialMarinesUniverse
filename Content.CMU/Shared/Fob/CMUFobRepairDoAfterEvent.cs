using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Fob;

[Serializable, NetSerializable]
public sealed partial class CMUFobRepairDoAfterEvent : DoAfterEvent
{
    [DataField] public NetEntity Network;
    [DataField] public int Generation;
    [DataField] public int Attempt;
    [DataField] public bool Control;

    public override DoAfterEvent Clone() => (CMUFobRepairDoAfterEvent) MemberwiseClone();
}
