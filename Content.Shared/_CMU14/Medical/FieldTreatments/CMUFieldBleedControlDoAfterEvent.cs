using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.FieldTreatments;

[Serializable, NetSerializable]
public sealed partial class CMUFieldBleedControlDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Part;

    public CMUFieldBleedControlDoAfterEvent(NetEntity part)
    {
        Part = part;
    }

    public CMUFieldBleedControlDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new CMUFieldBleedControlDoAfterEvent(Part);
}
