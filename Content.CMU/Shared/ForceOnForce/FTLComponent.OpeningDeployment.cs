using Robust.Shared.GameStates;

namespace Content.Shared.Shuttles.Components;

public sealed partial class FTLComponent
{
    /// <summary>This flight must finish the shared first-drop countdown before takeoff.</summary>
    [DataField, AutoNetworkedField]
    public bool OpeningDeployment;

    /// <summary>Pad to restore if the server interrupts this flight before liftoff.</summary>
    [DataField]
    public EntityUid? OpeningDeploymentOrigin;
}
