using Robust.Shared.Prototypes;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.GameStates;

namespace Content.Shared._AU14.Weapons;

/// <summary>
///     Component put on entities to block them from hitting matching IFFs
/// </summary>

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MeleeIFFComponent : Component
{
    /// <summary>
    ///     Factions that are ignored by this entity's melee attacks
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<EntProtoId<IFFFactionComponent>> Factions;
}
