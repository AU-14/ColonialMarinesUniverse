using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Ordnance;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCExplosionSimulatorWatchingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Watching;
}
