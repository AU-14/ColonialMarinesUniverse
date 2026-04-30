using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Ordnance;

/// <summary>
///     Marker placed on the entity that is spawned when a mine casing is planted on the floor.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RMCPlantedMineComponent : Component
{
    /// <summary>
    ///     The faction the claymore will ignore.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId<IFFFactionComponent>? Faction;

}
