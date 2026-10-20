using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ShipWeapons;

[RegisterComponent]
public sealed partial class CMUShipWeaponsTerminalComponent : Component
{
    [DataField]
    public int MaxSelections = 2;

    /// <summary>
    ///     The team whose tech tree purchases add fire missions to this terminal's ship, matching the tech tree's
    ///     team ids (govfor, opfor). Empty for a terminal no team buys for.
    /// </summary>
    [DataField]
    public string Faction = string.Empty;

    /// <summary>
    /// Fire missions shared by all selected weapons. Only read when this terminal locks the ship's loadout.
    /// </summary>
    [DataField]
    public int Uses = 2;
}

[Serializable, NetSerializable]
public enum CMUShipWeaponsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUShipWeaponsBuiState(
    bool available,
    CMUShipWeaponStatus[] weapons,
    ProtoId<CMUShipWeaponPrototype>[] availableWeapons,
    bool requiresGuns,
    int maxSelections,
    int uses,
    bool locked = false) : BoundUserInterfaceState
{
    /// <summary>Whether an arming terminal has already locked in this ship's loadout.</summary>
    public readonly bool Locked = locked;

    public readonly bool Available = available;
    public readonly CMUShipWeaponStatus[] Weapons = weapons;
    public readonly ProtoId<CMUShipWeaponPrototype>[] AvailableWeapons = availableWeapons;
    public readonly bool RequiresGuns = requiresGuns;
    public readonly int MaxSelections = maxSelections;
    public readonly int Uses = uses;
}

[Serializable, NetSerializable]
public sealed class CMUShipWeaponsArmBuiMsg(params ProtoId<CMUShipWeaponPrototype>[] weapons) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUShipWeaponPrototype>[] Weapons = weapons;
}

[Serializable, NetSerializable]
public sealed class CMUShipWeaponSelectBuiMsg(
    ProtoId<CMUShipWeaponPrototype>? weapon,
    CMUShipWeaponBearing bearing) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUShipWeaponPrototype>? Weapon = weapon;
    public readonly CMUShipWeaponBearing Bearing = bearing;
}

[ByRefEvent]
public readonly record struct CMUShipWeaponLaunchEvent(EntityUid User);

[ByRefEvent]
public readonly record struct CMUShipWeaponsRefreshEvent;
