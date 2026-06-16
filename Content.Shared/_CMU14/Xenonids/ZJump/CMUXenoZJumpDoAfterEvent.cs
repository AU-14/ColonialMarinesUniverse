using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Xenonids.ZJump;

[Serializable, NetSerializable]
public sealed partial class CMUXenoZJumpDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public NetCoordinates Coordinates;

    public CMUXenoZJumpDoAfterEvent(NetCoordinates coordinates)
    {
        Coordinates = coordinates;
    }
}
