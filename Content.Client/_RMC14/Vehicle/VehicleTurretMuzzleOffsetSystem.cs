using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.CMU14.Vehicle.Jeep; // CMU14
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Vehicle;

public sealed partial class VehicleTurretMuzzleOffsetSystem : EntitySystem
{
    [Dependency] private IEyeManager _eye = default!; // CMU14: the flash follows the camera's rotation
    [Dependency] private GunMuzzleOffsetSystem _gunMuzzleOffset = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretMuzzleSystem _turretMuzzle = default!;
    [Dependency] private VehicleTurretVisualSystem _turretVisual = default!; // CMU14: the turret's drawn pivot

    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleWeaponsOperatorComponent, BeforeAttemptShootEvent>(OnBeforeAttemptShoot);
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<VehicleTurretTrackedMuzzleFlashComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var trackedFlash, out var xform))
        {
            if (TerminatingOrDeleted(trackedFlash.Weapon))
                continue;

            if (!TryGetGunPose(trackedFlash.Weapon, null, out var origin, out var rotation))
                continue;

            var originMap = _transform.ToMapCoordinates(origin);
            xform.ActivelyLerping = false;
            var effectRotation = (rotation + trackedFlash.RotationOffset).Reduced();
            _transform.SetWorldRotationNoLerp((uid, xform), effectRotation);
            _transform.SetWorldPosition((uid, xform), originMap.Position + effectRotation.RotateVec(trackedFlash.Offset));
        }
    }

    public bool TryGetGunOrigin(EntityUid weaponUid, EntityCoordinates? target, out EntityCoordinates origin)
    {
        return TryGetGunPose(weaponUid, target, out origin, out _);
    }

    public bool TryGetGunPose(
        EntityUid weaponUid,
        EntityCoordinates? target,
        out EntityCoordinates origin,
        out Angle rotation)
    {
        origin = default;
        rotation = Angle.Zero;

        if (!TryComp(weaponUid, out VehicleTurretComponent? turret))
            return false;

        // CMU14 VehicleGunOrigin Begin: a gun with a CMU origin flashes at its drawn barrel tip: the
        // turret's pivot where this client draws it, out along the aim, and up the screen by the
        // barrel's height in the art.
        if (TryComp(weaponUid, out CMUVehicleGunOriginComponent? cmuOrigin) &&
            _turretVisual.TryGetRenderedPose(weaponUid, out var pivot, out _))
        {
            rotation = _transform.GetWorldRotation(weaponUid);
            var pivotMap = _transform.ToMapCoordinates(pivot);
            var up = (-_eye.CurrentEye.Rotation).RotateVec(new Vector2(0f, cmuOrigin.MuzzleUp));
            var tip = pivotMap.Position + rotation.ToWorldVec() * cmuOrigin.Barrel + up;
            origin = _transform.ToCoordinates(new MapCoordinates(tip, pivotMap.MapId));
            return true;
        }
        // CMU14 End

        origin = _transform.GetMoverCoordinates(weaponUid);
        rotation = _transform.GetWorldRotation(weaponUid);

        EntityCoordinates? aimTarget = target;
        if (aimTarget == null &&
            TryComp(weaponUid, out GunComponent? gun) &&
            gun.ShootCoordinates is { } shootCoordinates)
        {
            aimTarget = shootCoordinates;
        }

        if (TryComp(weaponUid, out GunMuzzleOffsetComponent? gunMuzzle))
        {
            if (_gunMuzzleOffset.TryGetMuzzleCoordinates(weaponUid, gunMuzzle, aimTarget, out var muzzleCoords, out var muzzleRotation))
            {
                origin = muzzleCoords;
                rotation = muzzleRotation;
            }
        }

        if (TryComp(weaponUid, out VehicleTurretMuzzleComponent? turretMuzzle))
            origin = _turretMuzzle.GetMuzzleCoordinates(weaponUid, turretMuzzle, origin);

        return true;
    }

    private void OnBeforeAttemptShoot(Entity<VehicleWeaponsOperatorComponent> ent, ref BeforeAttemptShootEvent args)
    {
        if (ent.Comp.SelectedWeapon is not { } selectedWeapon)
            return;

        if (!TryGetGunOrigin(selectedWeapon, null, out var origin))
            return;

        args.Origin = origin;
        args.Handled = true;
    }
}
