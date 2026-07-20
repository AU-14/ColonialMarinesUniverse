using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    /// <summary>
    /// RMC compatibility overload for callers that still consume the gun UID and component separately.
    /// </summary>
    public bool TryGetGun(EntityUid entity, out EntityUid gunUid, out GunComponent gun)
    {
        if (TryGetGun(entity, out Entity<GunComponent> gunEntity))
        {
            gunUid = gunEntity.Owner;
            gun = gunEntity.Comp;
            return true;
        }

        gunUid = default;
        gun = default!;
        return false;
    }

    public void ResetShotCounter(EntityUid uid, GunComponent gun)
    {
        if (gun.ShotCounter == 0)
            return;

        gun.ShotCounter = 0;
        DirtyField(uid, gun, nameof(GunComponent.ShotCounter));
    }

    /// <summary>
    /// RMC assisted reload compatibility. Current ballistic loading owns validation and stack splitting.
    /// </summary>
    public bool TryAmmoInsert(
        EntityUid providerUid,
        BallisticAmmoProviderComponent provider,
        EntityUid ammo,
        EntityUid loader,
        EntityUid weapon,
        TimeSpan insertDelay)
    {
        _ = weapon;
        _ = insertDelay;
        return TryBallisticInsert((providerUid, provider), ammo, loader);
    }

    public void SetProjectileTarget(EntityUid projectile, EntityUid target)
    {
        var targeted = EnsureComp<TargetedProjectileComponent>(projectile);
        targeted.Target = target;
        Dirty(projectile, targeted);
    }
}
