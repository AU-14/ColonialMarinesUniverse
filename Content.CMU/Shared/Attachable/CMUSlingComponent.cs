using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Attachable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CMUSlingSystem))]
public sealed partial class CMUSlingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Underbarrel;
}
