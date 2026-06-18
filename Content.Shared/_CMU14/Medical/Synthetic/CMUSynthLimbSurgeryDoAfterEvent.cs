using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Synthetic;

[Serializable, NetSerializable]
public enum CMUSynthLimbSurgeryStep : byte
{
    CutChassis,
    StripWiring,
    DetachLimb,
    AttachLimb,
    WeldChassis,
}

[Serializable, NetSerializable]
public sealed partial class CMUSynthLimbSurgeryDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public CMUSynthLimbSurgeryStep Step;

    [DataField]
    public string SlotId = string.Empty;
}
