using Robust.Shared.Player;

namespace Content.Shared.Chat;

/// <summary>
/// Event fired before a player's entity speaks, emotes, or whispers in-game.
/// </summary>
[ByRefEvent]
// CMU14 type: the Rider antag's voice hijack cancels and re-sends IC chat through it
public record struct InGameICMessageAttemptEvent(ICommonSession? Session, InGameICChatType Type, string Message, bool Cancelled = false);
