namespace Content.Shared.CMU14.Sentry;

[ByRefEvent]
public record struct CMUSentryRepairAttemptEvent(EntityUid User, bool Cancelled = false);

[ByRefEvent]
public readonly record struct CMUSentryBrokenEvent;

[ByRefEvent]
public readonly record struct CMUSentryRepairedEvent;

/// <summary>Allows variants to restore their collision contract after the common mode update.</summary>
[ByRefEvent]
public readonly record struct CMUSentryStateUpdatedEvent;
