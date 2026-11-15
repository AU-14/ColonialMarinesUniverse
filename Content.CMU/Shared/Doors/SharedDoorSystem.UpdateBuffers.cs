using Content.Shared.Doors.Components;

namespace Content.Shared.Doors.Systems;

public abstract partial class SharedDoorSystem
{
    // State-change callbacks may update the active set or re-enter the update.
    // Keep each invocation's snapshot independent and clear component references on return.
    private readonly Stack<List<Entity<DoorComponent>>> _cmuActiveDoorSnapshots = new();
    private Predicate<EntityUid>? _cmuTerminatingCrushTarget;
}
