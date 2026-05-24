using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._RMC14.Xenonids.Bombard;

[Serializable, NetSerializable]
public sealed partial class XenoBombardDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public MapCoordinates SourceCoordinates;

    [DataField]
    public MapCoordinates Coordinates;

    public override DoAfterEvent Clone()
    {
        return new XenoBombardDoAfterEvent
        {
            SourceCoordinates = SourceCoordinates,
            Coordinates = Coordinates,
        };
    }
}
