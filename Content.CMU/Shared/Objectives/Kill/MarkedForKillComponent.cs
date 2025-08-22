using Robust.Shared.GameStates;

namespace Content.Shared.AU14.Objectives.Kill;
[RegisterComponent, NetworkedComponent]
public sealed partial class MarkedForKillComponent : Component
{
    public Dictionary<EntityUid, string> AssociatedObjectives = new();

    // objective, faction marked for
}
