using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.K9.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class K9EvacuateTargetComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Handler;

    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
