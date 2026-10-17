using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Camera;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(SharedRMCCameraSystem))]
public sealed partial class RMCCameraComputerComponent : Component
{
    [DataField, AutoNetworkedField]
    public LocId? Title;

    [DataField, AutoNetworkedField]
    public Vector2i ViewportSize = new(1200, 1200);
}
