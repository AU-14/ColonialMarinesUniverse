using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.JoinXeno;

/// <summary>
/// Marks a xeno that is currently reserved by a timed larva-pool offer.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class LarvaPoolOfferComponent : Component
{
    [DataField, AutoNetworkedField]
    public TimeSpan ExpiresAt;
}
