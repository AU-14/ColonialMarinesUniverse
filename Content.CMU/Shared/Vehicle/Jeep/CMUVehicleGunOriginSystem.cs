using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Vehicle.Jeep;

/// <summary>
/// Starts a vehicle-mounted gun's shots and muzzle flash at its <see cref="CMUVehicleGunOriginComponent"/>.
/// </summary>
public sealed class CMUVehicleGunOriginSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleGunOriginComponent, AttemptShootEvent>(OnAttemptShoot, after: [typeof(GunMuzzleOffsetSystem)]);
        SubscribeLocalEvent<CMUVehicleGunOriginComponent, RMCBeforeMuzzleFlashEvent>(OnBeforeMuzzleFlash, after: [typeof(GunMuzzleOffsetSystem)]);
    }

    private void OnAttemptShoot(Entity<CMUVehicleGunOriginComponent> ent, ref AttemptShootEvent args)
    {
        if (!args.Cancelled && TryGetMuzzle(ent, args.ToCoordinates, out var muzzle))
            args.FromCoordinates = muzzle;
    }

    private void OnBeforeMuzzleFlash(Entity<CMUVehicleGunOriginComponent> ent, ref RMCBeforeMuzzleFlashEvent args)
    {
        if (!TryGetMuzzle(ent, null, out var muzzle))
            return;

        var muzzleMap = _transform.ToMapCoordinates(muzzle);
        var weaponMap = _transform.GetMapCoordinates(args.Weapon);
        if (muzzleMap.MapId != weaponMap.MapId)
            return;

        args.Offset = (-_transform.GetWorldRotation(args.Weapon)).RotateVec(muzzleMap.Position - weaponMap.Position);
    }

    /// <summary>
    /// The muzzle, on the vehicle: its pivot, then out by the barrel towards the target, or along the
    /// gun's own heading without one.
    /// </summary>
    private bool TryGetMuzzle(Entity<CMUVehicleGunOriginComponent> ent, EntityCoordinates? target, out EntityCoordinates muzzle)
    {
        muzzle = default;
        if (!_turret.TryGetVehicle(ent.Owner, out var vehicle))
            return false;

        var (vehiclePos, vehicleRot) = _transform.GetWorldPositionRotation(vehicle);
        var pivot = vehiclePos + vehicleRot.RotateVec(ent.Comp.Pivot);
        var aim = _transform.GetWorldRotation(ent.Owner).ToWorldVec();
        if (target is { } to)
        {
            var toMap = _transform.ToMapCoordinates(to);
            var towards = toMap.Position - pivot;
            if (toMap.MapId == _transform.GetMapId(vehicle) && towards.LengthSquared() > 0.0001f)
                aim = Vector2.Normalize(towards);
        }

        var local = ent.Comp.Pivot + (-vehicleRot).RotateVec(aim * ent.Comp.Barrel);
        muzzle = new EntityCoordinates(vehicle, local);
        return true;
    }
}
