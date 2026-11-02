using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.VoiceChat;

/// <summary>
/// Sent from an announcement console's window to start or stop a live voice announcement through it.
/// </summary>
[Serializable, NetSerializable]
public sealed class CMUVoiceAnnounceToggleBuiMsg : BoundUserInterfaceMessage;
