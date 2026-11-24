using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Round.Antags.Rider;

/// <summary>
/// Gives the player controlling this body a health bar for the Rider's current host.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RiderHostHealthComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [AutoNetworkedField]
    public EntityUid? Host;
}
