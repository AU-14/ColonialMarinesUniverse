using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.Wounds;

[Serializable, NetSerializable]
public sealed partial class CMUBandageDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Part;

    public CMUBandageDoAfterEvent(NetEntity part)
    {
        Part = part;
    }

    public CMUBandageDoAfterEvent()
    {
    }

    public override DoAfterEvent Clone() => new CMUBandageDoAfterEvent(Part);
}
