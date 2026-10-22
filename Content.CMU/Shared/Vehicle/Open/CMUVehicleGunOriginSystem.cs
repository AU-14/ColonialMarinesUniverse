using System.Numerics;
using Content.Shared._RMC14.Vehicle;
using Content.Shared._RMC14.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;

namespace Content.Shared.CMU14.Vehicle.Open;

/// <summary>
/// Starts a vehicle-mounted gun's shots and muzzle flash at its <see cref="CMUVehicleGunOriginComponent"/>.
/// </summary>
public sealed class CMUVehicleGunOriginSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private VehicleTurretSystem _turret = default!;

    private const float PixelsPerMeter = 32f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUVehicleGunOriginComponent, AttemptShootEvent>(OnAttemptShoot, after: [typeof(GunMuzzleOffsetSystem)]);
        SubscribeLocalEvent<CMUVehicleGunOriginComponent, RMCBeforeMuzzleFlashEvent>(OnBeforeMuzzleFlash, after: [typeof(GunMuzzleOffsetSystem)]);
    }

    private void OnAttemptShoot(Entity<CMUVehicleGunOriginComponent> ent, ref AttemptShootEvent args)
    {
        if (!args.Cancelled && TryGetMuzzle(ent, args.ToCoordinates, ViewRotation(args.User), out var muzzle))
            args.FromCoordinates = muzzle;
    }

    private void OnBeforeMuzzleFlash(Entity<CMUVehicleGunOriginComponent> ent, ref RMCBeforeMuzzleFlashEvent args)
    {
        if (!TryGetMuzzle(ent, null, Angle.Zero, out var muzzle))
            return;

        var muzzleMap = _transform.ToMapCoordinates(muzzle);
        var weaponMap = _transform.GetMapCoordinates(args.Weapon);
        if (muzzleMap.MapId != weaponMap.MapId)
            return;

        args.Offset = (-_transform.GetWorldRotation(args.Weapon)).RotateVec(muzzleMap.Position - weaponMap.Position);
    }

    /// <summary>
    /// How the shooter's camera is turned, as their client reported it.
    /// </summary>
    private Angle ViewRotation(EntityUid? user)
    {
        return CompOrNull<CMUViewRotationComponent>(user)?.Rotation ?? Angle.Zero;
    }

    /// <summary>
    /// The muzzle, on the vehicle: the pivot where the shooter's screen draws it, then out by the
    /// barrel towards the target, or along the gun's own heading without one, and up the shooter's
    /// screen by the barrel's height in the art.
    /// </summary>
    private bool TryGetMuzzle(Entity<CMUVehicleGunOriginComponent> ent, EntityCoordinates? target, Angle view, out EntityCoordinates muzzle)
    {
        muzzle = default;
        if (!_turret.TryGetVehicle(ent.Owner, out var vehicle))
            return false;

        var (vehiclePos, vehicleRot) = _transform.GetWorldPositionRotation(vehicle);
        var pivot = vehiclePos + vehicleRot.RotateVec(DrawnPivot(ent, vehicleRot, view) ?? ent.Comp.Pivot);
        var aim = _transform.GetWorldRotation(ent.Owner).ToWorldVec();
        if (target is { } to)
        {
            var toMap = _transform.ToMapCoordinates(to);
            var towards = toMap.Position - pivot;
            if (toMap.MapId == _transform.GetMapId(vehicle) && towards.LengthSquared() > 0.0001f)
                aim = Vector2.Normalize(towards);
        }

        var tip = pivot + aim * ent.Comp.Barrel + (-view).RotateVec(new Vector2(0f, ent.Comp.MuzzleUp));
        muzzle = new EntityCoordinates(vehicle, (-vehicleRot).RotateVec(tip - vehiclePos));
        return true;
    }

    /// <summary>
    /// Where the art draws the gun's pivot on the vehicle, in the vehicle's own space: its turret's
    /// pixel offset for the frame the vehicle is drawn in on a screen turned by the view, as the
    /// client places it.
    /// </summary>
    private Vector2? DrawnPivot(Entity<CMUVehicleGunOriginComponent> ent, Angle vehicleRot, Angle view)
    {
        if (!TryComp(ent, out VehicleTurretComponent? turret))
            return null;

        _turret.TryGetAnchorTurret(ent.Owner, turret, out _, out var anchor);
        var facing = (_turret.GetOffsetFacing(anchor, anchor, vehicleRot, vehicleRot) + view).Reduced();
        var pixels = _turret.GetPixelOffset(anchor, facing) / PixelsPerMeter;
        if (pixels == Vector2.Zero)
            return null;

        return anchor.UseDirectionalOffsets
            ? VehicleTurretDirectionHelpers.GetLocalOffsetForRenderDirection(pixels, facing)
            : (-vehicleRot).RotateVec(pixels);
    }
}
