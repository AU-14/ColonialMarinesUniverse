using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Injuries.Wounds;

[Serializable, NetSerializable]
public sealed partial class CMUBandageDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Part;

    [DataField]
    public bool ApplyInstantTreatment;

    public CMUBandageDoAfterEvent(NetEntity part, bool applyInstantTreatment = false)
    {
        Part = part;
        ApplyInstantTreatment = applyInstantTreatment;
    }

    public CMUBandageDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new CMUBandageDoAfterEvent(Part, ApplyInstantTreatment);
}
