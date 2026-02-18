using Robust.Shared.GameStates;

namespace Content.Server.AU14.Objectives.Destroy;

[RegisterComponent]
public sealed partial class DestroyObjectiveTrackerComponent : Component
{
    // Link back to the objective entity that cares about this target
    public EntityUid ObjectiveUid;
}

