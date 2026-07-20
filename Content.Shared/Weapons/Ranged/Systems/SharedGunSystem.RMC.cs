using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.DoAfter;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private AttachableHolderSystem _rmcAttachableHolder = default!;
    [Dependency] private RMCSharedWeaponControllerSystem _rmcWeaponController = default!;
    [Dependency] private VehicleWeaponsSystem _rmcVehicleWeapons = default!;

    private bool TryGetRmcPriorityGun(EntityUid entity, out Entity<GunComponent> gun)
    {
        gun = default;

        if (TryComp(entity, out VehiclePortGunOperatorComponent? portGunOperator) &&
            portGunOperator.Gun is { } portGun &&
            TryComp(portGun, out VehiclePortGunComponent? portGunComponent) &&
            portGunComponent.Operator == entity &&
            TryComp(portGun, out GunComponent? portGunGun))
        {
            gun = (portGun, portGunGun);
            return true;
        }

        if (TryComp(entity, out VehicleWeaponsOperatorComponent? vehicleOperator) &&
            vehicleOperator.Vehicle is { } vehicle &&
            _rmcVehicleWeapons.TryGetSelectedWeaponForOperator(vehicle, entity, out var selected) &&
            TryComp(selected, out GunComponent? selectedGun))
        {
            gun = (selected, selectedGun);
            return true;
        }

        if (_rmcAttachableHolder.TryGetInhandSupercedingGun(entity, out var attachable, out var attachableGun))
        {
            gun = (attachable, attachableGun);
            return true;
        }

        return false;
    }

    private bool TryGetRmcFallbackGun(EntityUid entity, out Entity<GunComponent> gun)
    {
        gun = default;

        if (!_rmcWeaponController.TryGetControlledWeapon(entity, out var weapon, out var weaponGun))
            return false;

        gun = (weapon.Value, weaponGun);
        return true;
    }

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
    /// RMC delayed direct and assisted reload compatibility. Current ballistic loading owns final validation and stack splitting.
    /// </summary>
    public bool TryAmmoInsert(
        EntityUid providerUid,
        BallisticAmmoProviderComponent provider,
        EntityUid ammo,
        EntityUid loader,
        EntityUid weapon,
        TimeSpan insertDelay)
    {
        if (!CanInsertBallistic((providerUid, provider), ammo))
            return false;

        if (insertDelay <= TimeSpan.Zero)
            return TryBallisticInsert((providerUid, provider), ammo, loader);

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            loader,
            insertDelay,
            new DelayedAmmoInsertDoAfterEvent(),
            eventTarget: providerUid,
            target: weapon,
            used: ammo)
        {
            BreakOnMove = true,
            BreakOnDamage = false,
            NeedHand = true,
        });

        return true;
    }

    public void SetProjectileTarget(EntityUid projectile, EntityUid target)
    {
        var targeted = EnsureComp<TargetedProjectileComponent>(projectile);
        targeted.Target = target;
        Dirty(projectile, targeted);
    }
}
