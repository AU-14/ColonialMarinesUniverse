using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility; // CMU14

namespace Content.Shared._RMC14.Dropship.AttachmentPoint;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedDropshipSystem))]
public sealed partial class DropshipWeaponPointComponent : Component
{
    [DataField, AutoNetworkedField]
    public string WeaponContainerSlotId = "rmc_dropship_weapon_point_weapon_container_slot";

    [DataField, AutoNetworkedField]
    public string AmmoContainerSlotId = "rmc_dropship_weapon_point_ammo_container_slot";

    // CMU14: fixed weapons can still have their ammunition serviced.
    [DataField, AutoNetworkedField, Access(Other = AccessPermissions.ReadExecute)]
    public bool FixedWeapon;

    [DataField, AutoNetworkedField]
    public string DirOffset = string.Empty;

    [DataField, AutoNetworkedField]
    public DropshipWeaponPointLocation? Location;

    // CMU14 field
    /// <summary>Mount-specific artwork, for example complete weapons on an exposed underside.</summary>
    [DataField, AutoNetworkedField, Access(Other = AccessPermissions.ReadExecute)]
    public Dictionary<string, SpriteSpecifier.Rsi> SpriteOverrides = new();
}

[Serializable, NetSerializable]
public enum DropshipWeaponPointLayers
{
    Layer,
}

[Serializable, NetSerializable]
public enum DropshipWeaponPointLocation
{
    PortWing = 1,
    StarboardWing = 2,
    PortFore = 3,
    StarboardFore = 4,
}
