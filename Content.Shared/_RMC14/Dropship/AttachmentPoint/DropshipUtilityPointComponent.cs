using Content.Shared._RMC14.Dropship.Utility.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._RMC14.Dropship.AttachmentPoint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem), typeof(DropshipUtilitySystem))]
public sealed partial class DropshipUtilityPointComponent : Component
{
    [DataField, AutoNetworkedField]
    public string UtilitySlotId = "rmc_dropship_utility_point_container_slot";

    [DataField, AutoNetworkedField]
    public string DeployableContainerSlotId = "rmc_orbital_deployer_deployable_container_slot";

    /// <summary>
    ///     Ammo container for weapons (e.g. the 40mm BOFORS and 105mm howitzer)
    ///     mounted on this crew compartment attach point.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string AmmoContainerSlotId = "rmc_dropship_utility_point_ammo_container_slot";
}
