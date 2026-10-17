using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.CMU14.ShipWeapons;

[RegisterComponent]
public sealed partial class CMUShipWeaponsTerminalComponent : Component;

[Serializable, NetSerializable]
public enum CMUShipWeaponsUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CMUShipWeaponsBuiState(bool available, CMUShipWeaponStatus[] weapons) : BoundUserInterfaceState
{
    public readonly bool Available = available;
    public readonly CMUShipWeaponStatus[] Weapons = weapons;
}

[Serializable, NetSerializable]
public sealed class CMUShipWeaponsArmBuiMsg(
    ProtoId<CMUShipWeaponPrototype> first,
    ProtoId<CMUShipWeaponPrototype> second) : BoundUserInterfaceMessage
{
    public readonly ProtoId<CMUShipWeaponPrototype> First = first;
    public readonly ProtoId<CMUShipWeaponPrototype> Second = second;
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
