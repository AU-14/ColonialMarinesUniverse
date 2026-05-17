using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// DoAfter event for a xeno digging an underground entrance.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class XenoDigUndergroundDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Coordinates;

    [DataField]
    public bool DiggingUp;

    public XenoDigUndergroundDoAfterEvent(NetCoordinates coordinates, bool diggingUp)
    {
        Coordinates = coordinates;
        DiggingUp = diggingUp;
    }
}
