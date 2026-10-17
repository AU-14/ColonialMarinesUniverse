using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Construction;

[RegisterComponent, NetworkedComponent]
public sealed partial class RMCConstructionPreventCollideComponent : Component
{
    [DataField]
    public float Range = 0.75f;

    [DataField]
    public EntityUid? Target;
}
