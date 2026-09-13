using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds;

[Serializable, NetSerializable]
public sealed partial class CMUBandageDoAfterEvent : DoAfterEvent
{
    [DataField] public NetEntity Medic;
    [DataField] public NetEntity Patient;
    [DataField] public NetEntity Treater;
    [DataField] public NetEntity Part;
    [DataField] public FixedPoint2? PartHealthCap;
    [DataField] public bool ApplyInstantTreatment;
    [DataField] public bool AutoReapplyKit;

    public CMUBandageDoAfterEvent()
    {
    }

    public CMUBandageDoAfterEvent(NetEntity medic, NetEntity patient, NetEntity treater,
        NetEntity part, FixedPoint2? partHealthCap, bool applyInstantTreatment, bool autoReapplyKit)
    {
        Medic = medic;
        Patient = patient;
        Treater = treater;
        Part = part;
        PartHealthCap = partHealthCap;
        ApplyInstantTreatment = applyInstantTreatment;
        AutoReapplyKit = autoReapplyKit;
    }

    public override DoAfterEvent Clone() => new CMUBandageDoAfterEvent(
        Medic, Patient, Treater, Part, PartHealthCap, ApplyInstantTreatment, AutoReapplyKit);
}
