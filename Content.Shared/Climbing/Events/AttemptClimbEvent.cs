namespace Content.Shared.Climbing.Events;

[ByRefEvent]
public record struct AttemptClimbEvent(EntityUid User, EntityUid Climber, EntityUid Climbable)
{
    public bool Cancelled;

    // RMC14: prevents the fallback obstruction popup when a handler supplied its own.
    public bool PopupHandled;
}
