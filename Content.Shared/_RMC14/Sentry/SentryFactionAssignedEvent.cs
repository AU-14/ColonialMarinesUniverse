namespace Content.Shared._RMC14.Sentry;

/// <summary>
/// Raised on the sentry entity after its faction assignment has been applied or changed.
/// Allows other systems (e.g. AllianceConsoleSystem) to react.
/// </summary>
[ByRefEvent]
public record struct SentryFactionAssignedEvent(EntityUid User);
