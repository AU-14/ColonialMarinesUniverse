namespace Content.Server.CMU14.Chat;

/// <summary>
/// Broadcast after an entity's emote is shown in chat, so listeners such as boom mics can pick it up.
/// Speech has <see cref="Content.Shared.Chat.EntitySpokeEvent"/>; emotes had nothing.
/// </summary>
[ByRefEvent]
public readonly record struct CMUEntityEmotedEvent(EntityUid Source, string Action);
