using Content.Shared.Chemistry.Components;

namespace Content.Shared.CMU14.Medical.Injuries.Wounds.Events;

/// <summary>Raised when accumulated external wound blood is ready to spill onto the floor.</summary>
[ByRefEvent]
public record struct CMUWoundBloodSpillEvent(Solution Solution)
{
    public bool Handled;
}
