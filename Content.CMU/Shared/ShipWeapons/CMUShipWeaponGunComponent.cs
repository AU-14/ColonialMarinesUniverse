using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.ShipWeapons;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUShipWeaponGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<CMUShipWeaponPrototype>> Weapons = new();

    [DataField, AutoNetworkedField]
    public string AmmoSlot = "mission-round";
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CMUShipWeaponAmmoComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public ProtoId<CMUShipWeaponPrototype> Weapon;
}
