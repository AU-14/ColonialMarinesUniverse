using Content.Shared.CMU14.ShipWeapons;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Overwatch;

public abstract partial class SharedOverwatchConsoleSystem
{
    public void CMUSetShipWeapons(
        Entity<OverwatchConsoleComponent> console,
        CMUShipWeaponStatus[] weapons,
        ProtoId<CMUShipWeaponPrototype>? selected,
        CMUShipWeaponBearing bearing,
        TimeSpan nextLaunch)
    {
        console.Comp.CMUShipWeapons = weapons;
        console.Comp.CMUSelectedShipWeapon = selected;
        console.Comp.CMUShipWeaponBearing = bearing;
        console.Comp.CMUNextShipWeaponLaunch = nextLaunch;
        Dirty(console);
    }
}
