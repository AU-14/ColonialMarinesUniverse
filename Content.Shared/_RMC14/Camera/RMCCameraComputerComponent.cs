using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Camera;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedRMCCameraSystem))]
public sealed partial class RMCCameraComputerComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId? Title;
}
