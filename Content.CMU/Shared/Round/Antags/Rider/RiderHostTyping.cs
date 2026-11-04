using Content.Shared.Chat.TypingIndicator;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.Round.Antags.Rider;

// stock TypingChangedEvent carries no channel, and only the client knows whether the rider is
// about to talk out of the host's mouth or just whisper to them
[Serializable, NetSerializable]
public sealed class RiderHostTypingEvent(TypingIndicatorState state) : EntityEventArgs
{
    public readonly TypingIndicatorState State = state;
}
