using Content.Client._RMC14.ItemPickup;
using Content.Client.Weapons.Ranged.Components;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    [Dependency] private ItemPickupSystem _rmcItemPickup = default!;

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
        return ShouldRearmSemiAuto(
            continuous,
            gun.Comp.SelectedMode,
            gun.Comp.AvailableModes,
            HasComp<GunClickToFireComponent>(gun));
    }

    private EntityUid GetRmcShootCoordinateEntity(EntityUid user, Entity<GunComponent> gun)
    {
        return HasComp<GunUseGunOriginComponent>(gun) ? gun.Owner : user;
    }

    private bool RMCRecentlyPickedUpItem()
    {
        return _rmcItemPickup.RecentItemPickUp;
    }
}
