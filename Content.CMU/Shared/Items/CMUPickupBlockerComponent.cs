using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.CMU14.Items;

/// <summary>
///     Prevents this entity from picking up items matching the blacklist,
///     which blocks using them too: guns, grenades and medical items all
///     need to be held first.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(CMUPickupBlockerSystem))]
public sealed partial class CMUPickupBlockerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityWhitelist Blocked = new();

    [DataField]
    public LocId BlockedPopup = "cmu-pickup-blocker-blocked";
}
