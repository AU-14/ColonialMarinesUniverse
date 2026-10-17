using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.IconSmoothing;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CMIconSmoothComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Smooth;
}
