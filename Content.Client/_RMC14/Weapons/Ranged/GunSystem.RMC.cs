using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    private bool ApplyRmcContinuousFirePolicy(Entity<GunComponent> gun, bool continuous)
    {
        return continuous && !HasComp<GunClickToFireComponent>(gun);
    }

    private EntityUid GetRmcShootCoordinateEntity(EntityUid user, Entity<GunComponent> gun)
    {
        return HasComp<GunUseGunOriginComponent>(gun) ? gun.Owner : user;
    }
}
