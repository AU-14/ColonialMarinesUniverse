using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Ape;

[Serializable, NetSerializable]
public sealed partial class ApeLeapDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates TargetCoords;

    public ApeLeapDoAfterEvent(NetCoordinates coordinates)
    {
        TargetCoords = coordinates;
    }
}

