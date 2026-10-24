using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Hygiene;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUShowerControlsComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Running;
}
