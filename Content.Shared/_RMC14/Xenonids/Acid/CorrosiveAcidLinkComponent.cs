using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Xenonids.Acid;

/// <summary>
///     Placed on a spawned corrosive acid visual entity, pointing back to the item it was applied to.
///     Lets vapor collisions on the acid sensor wash the acid off the linked target.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CorrosiveAcidLinkComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Target;
}
