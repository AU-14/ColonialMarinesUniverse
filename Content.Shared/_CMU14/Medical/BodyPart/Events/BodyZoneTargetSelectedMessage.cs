using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.Medical.BodyPart.Events;

/// <summary>
///     Raised by the client HUD widget when the local shooter clicks a zone on the
///     CM13 aim picker. The server clock is authoritative for <c>LastSelectedAt</c>
///     so the freshness window cannot be gamed by manipulating client time.
/// </summary>
[Serializable, NetSerializable]
public sealed class BodyZoneTargetSelectedMessage : EntityEventArgs
{
    public TargetBodyZone Zone { get; }

    public BodyZoneTargetSelectedMessage(TargetBodyZone zone)
    {
        Zone = zone;
    }
}
