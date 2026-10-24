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

    // CMU14 Begin: optional extra ammunition feeds.
    [DataField, AutoNetworkedField]
    public string? SecondAmmoContainerSlotId;

    // CMU14: restricted extra feed, for example the M90's Squall-only third feed.
    [DataField, AutoNetworkedField]
    public string? ThirdAmmoContainerSlotId;

    /// <summary>Ammo types accepted by <see cref="ThirdAmmoContainerSlotId"/>. Empty accepts any ammo.</summary>
    [DataField, AutoNetworkedField, Access(Other = AccessPermissions.ReadExecute)]
    public List<string> ThirdAmmoTypes = new();

    [Access(Other = AccessPermissions.ReadExecute)]
    public IEnumerable<string> GetAmmoSlots()
    {
        yield return AmmoContainerSlotId;
        if (SecondAmmoContainerSlotId is { } second)
            yield return second;
        if (ThirdAmmoContainerSlotId is { } third)
            yield return third;
    }

    [Access(Other = AccessPermissions.ReadExecute)]
    public string? GetFeedSlot(int feed)
    {
        return feed switch
        {
            0 => AmmoContainerSlotId,
            1 => SecondAmmoContainerSlotId,
            2 => ThirdAmmoContainerSlotId,
            _ => null,
        };
    }

    [Access(Other = AccessPermissions.ReadExecute)]
    public bool ThirdSlotAccepts(string? ammoType)
    {
        return ThirdAmmoTypes.Count == 0 || ammoType != null && ThirdAmmoTypes.Contains(ammoType);
    }
    // CMU14 End

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
    Chin = 5, // CMU14: midway's fixed chin gun, its own slot so it doesn't share a number with the port fore mount
}
