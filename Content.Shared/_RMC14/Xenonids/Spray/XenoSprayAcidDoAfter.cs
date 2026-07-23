using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Spray;

[Serializable, NetSerializable]
public sealed partial class XenoSprayAcidDoAfter : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates StartCoordinates;

    [DataField]
    public NetCoordinates Coordinates;

    public XenoSprayAcidDoAfter(NetCoordinates startCoordinates, NetCoordinates coordinates)
    {
        StartCoordinates = startCoordinates;
        Coordinates = coordinates;
    }

    public override DoAfterEvent Clone()
    {
        return new XenoSprayAcidDoAfter(StartCoordinates, Coordinates);
    }
}
