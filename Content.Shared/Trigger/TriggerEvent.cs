namespace Content.Shared.Trigger;

/// <summary>
/// Raised whenever something is Triggered on the entity.
/// </summary>
/// <param name="User">The entity that activated the trigger.</param>
/// <param name="Key">
/// Allows to have multiple independent triggers on the same entity.
/// Setting this to null will activate all triggers.
/// </param>
/// <param name="Handled">Marks the event as handled if at least one trigger effect was activated.</param>
/// <param name="Predicted">Marks that this trigger is being replicated on the client.</param>
[ByRefEvent]
public record struct TriggerEvent(EntityUid? User = null, string? Key = null, bool Predicted = true, bool Handled = false);

/// <summary>
/// Raised before a trigger is activated.
/// Cancelling prevents it from triggering.
/// </summary>
/// <param name="User">The entity that activated the trigger.</param>
/// <param name="Key">
/// Allows to have multiple independent triggers on the same entity.
/// Setting this to null will activate all triggers.
/// </param>
/// <param name="Handled">Marks the event as handled if at least one trigger effect was activated.</param>
[ByRefEvent]
public record struct AttemptTriggerEvent(EntityUid? User, string? Key = null, bool Cancelled = false);

/// <summary>
/// Raised when a timer trigger becomes active.
/// </summary>
/// <param name="User">The entity that activated the trigger.</param>
[ByRefEvent]
public readonly record struct ActiveTimerTriggerEvent(EntityUid? User);

/// <summary>
/// Raised before a timer trigger becomes active, allowing server-specific validation and audit details.
/// </summary>
/// <param name="User">The entity that activated the timer.</param>
/// <param name="Delay">The configured timer delay.</param>
/// <param name="Cancelled">Whether timer activation should be cancelled.</param>
/// <param name="LogMessage">An optional admin log message that replaces the generic timer message.</param>
[ByRefEvent]
public record struct AttemptTimerTriggerEvent(
    EntityUid? User,
    TimeSpan Delay,
    bool Cancelled = false,
    string? LogMessage = null);
