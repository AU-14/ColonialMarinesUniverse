using Content.Client.Weapons.Ranged.Components;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private void InitializeRmcGun()
    {
        SubscribeLocalEvent<AmmoCounterComponent, UpdateClientAmmoEvent>(OnRmcUpdateClientAmmo);
    }

    private void OnRmcUpdateClientAmmo(Entity<AmmoCounterComponent> ent, ref UpdateClientAmmoEvent args)
    {
        UpdateAmmoCount(ent, args.AritifialIncrease);
    }

    private bool ApplyRmcContinuousFirePolicy(Entity<GunComponent> gun, bool continuous)
    {
        return continuous && !HasComp<GunClickToFireComponent>(gun);
    }

    private EntityUid GetRmcShootCoordinateEntity(EntityUid user, Entity<GunComponent> gun)
    {
        return HasComp<GunUseGunOriginComponent>(gun) ? gun.Owner : user;
    }
}
