using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Medical.Anatomy.BodyParts.Events;

/// <summary>
///     Raised by the client HUD widget when the local shooter clicks a zone on the
///     CM13 aim picker. The authenticated player's selection remains until they
///     choose another zone; the server validates the zone before storing it.
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
