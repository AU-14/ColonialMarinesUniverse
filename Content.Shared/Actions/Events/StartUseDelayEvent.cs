namespace Content.Shared.Actions.Events;

/// <summary>
/// Raised before an action use delay becomes its cooldown so fork systems can adjust the interval.
/// </summary>
[ByRefEvent]
public record struct StartUseDelayEvent(TimeSpan Delay, TimeSpan Start, TimeSpan End);
