using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Underground;

/// <summary>
/// DoAfter event for digging a tunnel entrance to/from the underground.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class UndergroundDigDoAfterEvent : SimpleDoAfterEvent
{
    [DataField(required: true)]
    public NetCoordinates Coordinates;

    /// <summary>
    /// True if the player is digging from underground back to the surface.
    /// </summary>
    [DataField]
    public bool DiggingUp;

    public UndergroundDigDoAfterEvent(NetCoordinates coordinates, bool diggingUp)
    {
        Coordinates = coordinates;
        DiggingUp = diggingUp;
    }
}
