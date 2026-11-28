namespace Content.Shared.CMU14.Combat.Drown;

/// <summary>
/// Raised on the drowner when its Drown action is used on <paramref name="Target"/>, before the grab and organic
/// checks. A handler that takes the attempt over (the Early Warning Joe's Overpressure burst on xenofauna) sets
/// <paramref name="Handled"/>; the action is then marked handled and no hold starts.
/// </summary>
[ByRefEvent]
public record struct CMUDrownAttemptEvent(EntityUid Target, bool Handled = false);
