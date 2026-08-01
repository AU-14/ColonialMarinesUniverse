using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Destroy;

[RegisterComponent, NetworkedComponent]
public sealed partial class MarkedForDestroyComponent : Component
{
    // objective uid -> faction to credit
    public Dictionary<EntityUid, string> AssociatedObjectives = new();
    public Dictionary<EntityUid, string?> AssociatedObjectiveJobs = new();
}

