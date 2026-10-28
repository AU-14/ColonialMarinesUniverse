using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Audio;

/// <summary>
/// Stops all currently playing admin sounds on the receiving client.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUStopAdminSoundEvent : EntityEventArgs
{
}
