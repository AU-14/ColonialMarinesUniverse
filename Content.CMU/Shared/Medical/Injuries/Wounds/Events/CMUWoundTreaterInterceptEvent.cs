namespace Content.Shared._CMU14.Medical.Injuries.Wounds.Events;

[ByRefEvent]
public record struct CMUWoundTreaterInterceptEvent(EntityUid User, EntityUid Treater, EntityUid Patient, bool Handled = false);
