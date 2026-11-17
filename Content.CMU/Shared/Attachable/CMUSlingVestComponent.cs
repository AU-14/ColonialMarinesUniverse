using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Attachable;

[RegisterComponent, NetworkedComponent]
[Access(typeof(CMUSlingSystem))]
public sealed partial class CMUSlingVestComponent : Component
{
    public const string ContainerId = "cmu-sling-vest";
}
