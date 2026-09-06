using System.Numerics;
using Content.Shared._CMU14.Dropship.Integrity;
using Content.Shared._CMU14.Dropship.TacticalLand;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared.Buckle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._CMU14.Dropship.DirectFire;

/// <summary>
/// Adapts dropship attachment-point weapons to the normal gun pipeline. The
/// gun remains installed through the powerloader system, while the seated
/// pilot operates it as a remotely selected gun.
/// </summary>
public sealed partial class GunshipDirectFireSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedGunSystem _guns = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private PowerLoaderSystem _powerLoader = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<GunshipDirectFireWeaponComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<GunshipDirectFireWeaponComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<GunshipDirectFireWeaponComponent, GetAmmoCountEvent>(OnGetAmmoCount);
        SubscribeLocalEvent<GunshipDirectFireWeaponComponent, GunShotEvent>(OnGunShot);
    }

    private void OnAttemptShoot(Entity<GunshipDirectFireWeaponComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled)
            return;

        string? message = null;
        if (!TryResolveMount(ent, out var point, out var grid, out _, out _) ||
            !CanOperate(args.User, ent.Owner, grid, point.Owner, out message) ||
            args.ToCoordinates is not { } targetCoordinates)
        {
            args.Cancelled = true;
            args.ResetCooldown = true;
            args.Message = message;
            return;
        }

        var target = _transform.ToMapCoordinates(targetCoordinates);
        var gridXform = Transform(grid);
        if (target.MapId != gridXform.MapID)
        {
            args.Cancelled = true;
            args.ResetCooldown = true;
            return;
        }

        var pointPosition = _transform.GetWorldPosition(point.Owner);
        var shipRotation = _transform.GetWorldRotation(grid);
        var forward = shipRotation.RotateVec(Vector2.UnitY);
        var mountPosition = pointPosition + forward * point.Comp.ForwardOffset;
        var aimOrigin = _transform.GetWorldPosition(grid);
        if (!TryGetClampedAim(
                shipRotation,
                aimOrigin,
                target.Position,
                ent.Comp.GimbalDegrees,
                out var direction,
                out _))
        {
            args.Cancelled = true;
            args.ResetCooldown = true;
            return;
        }

        // Cursor bearing is measured around the dropship center. Measuring it
        // around the nose-mounted muzzle makes ordinary cursor positions over
        // the ship fall behind the mount, where atan2 jumps between both ends
        // of the gimbal arc. The projectile still starts at the real muzzle.
        var distance = Vector2.Distance(mountPosition, target.Position);
        var muzzleOffset = MathF.Max(0f, ent.Comp.MuzzleOffset);
        var originMap = new MapCoordinates(mountPosition + direction * muzzleOffset, gridXform.MapID);
        var targetMap = new MapCoordinates(mountPosition + direction * distance, gridXform.MapID);
        args.FromCoordinates = _transform.ToCoordinates(originMap);
        args.ToCoordinates = _transform.ToCoordinates(targetMap);
    }

    public static bool TryGetClampedAim(
        Angle shipRotation,
        Vector2 aimOrigin,
        Vector2 target,
        float gimbalDegrees,
        out Vector2 direction,
        out float aimDegrees)
    {
        direction = default;
        aimDegrees = 0f;

        var desired = target - aimOrigin;
        if (desired.LengthSquared() <= 0.0001f)
            return false;

        desired = Vector2.Normalize(desired);
        var forward = shipRotation.RotateVec(Vector2.UnitY);
        var signedRadians = MathF.Atan2(
            forward.X * desired.Y - forward.Y * desired.X,
            Vector2.Dot(forward, desired));
        var halfGimbal = MathHelper.DegreesToRadians(MathF.Max(0f, gimbalDegrees) * 0.5f);
        var clampedRadians = Math.Clamp(signedRadians, -halfGimbal, halfGimbal);
        var aimOffset = new Angle(clampedRadians);
        direction = shipRotation.RotateVec(aimOffset.RotateVec(Vector2.UnitY));
        aimDegrees = (float) aimOffset.Degrees;
        return true;
    }

    private bool CanOperate(
        EntityUid user,
        EntityUid weapon,
        EntityUid grid,
        EntityUid point,
        out string? message)
    {
        message = null;

        if (!TryComp(user, out RemoteWeaponOperatorComponent? remote) ||
            remote.Platform != grid ||
            remote.SelectedWeapon != weapon ||
            !TryComp(user, out BuckleComponent? buckle) ||
            buckle.BuckledTo is not { } seat ||
            !TryComp(seat, out GunshipPilotSeatComponent? pilotSeat) ||
            pilotSeat.Pilot != user ||
            pilotSeat.ViewOffset != 0 ||
            pilotSeat.RearView ||
            Transform(seat).GridUid != grid ||
            !TryComp(user, out GunshipPilotHudComponent? hud) ||
            hud.Dropship != grid ||
            !hud.FlightControlsAvailable ||
            !TryComp(grid, out DropshipComponent? dropship) ||
            dropship.Crashed)
        {
            return false;
        }

        if (!_net.IsClient &&
            (!TryComp(grid, out DropshipTacticalHoverComponent? hover) || hover.AltitudeTransitionAt != null))
        {
            return false;
        }

        if (TryComp(grid, out DropshipIntegrityComponent? integrity) &&
            integrity.ActiveMalfunctions.Contains(DropshipMalfunction.WeaponShort))
        {
            message = Loc.GetString("cmu-gunship-direct-fire-weapon-short");
            return false;
        }

        return Transform(point).GridUid == grid;
    }

    private void OnTakeAmmo(Entity<GunshipDirectFireWeaponComponent> ent, ref TakeAmmoEvent args)
    {
        if (!TryResolveMount(ent, out var point, out _, out var ammoUid, out var ammo) ||
            ammoUid is not { } loadedAmmo ||
            ammo == null)
        {
            args.Reason = Loc.GetString("cmu-gunship-direct-fire-no-ammunition");
            return;
        }

        var roundsPerShot = Math.Max(1, ammo.RoundsPerShot);
        for (var i = 0; i < args.Shots; i++)
        {
            if (ammo.Rounds < roundsPerShot)
                break;

            ammo.Rounds -= roundsPerShot;
            var projectile = Spawn(ent.Comp.Projectile, args.Coordinates);
            args.Ammo.Add((projectile, _guns.EnsureShootable(projectile)));
        }

        _appearance.SetData(loadedAmmo, DropshipAmmoVisuals.Fill, ammo.Rounds);
        Dirty(loadedAmmo, ammo);
        _powerLoader.SyncAppearance(point.Owner);
    }

    private void OnGetAmmoCount(Entity<GunshipDirectFireWeaponComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!TryResolveMount(ent, out _, out _, out _, out var ammo) || ammo == null)
            return;

        var roundsPerShot = Math.Max(1, ammo.RoundsPerShot);
        args.Count = ammo.Rounds / roundsPerShot;
        args.Capacity = ammo.MaxRounds / roundsPerShot;
    }

    private void OnGunShot(Entity<GunshipDirectFireWeaponComponent> ent, ref GunShotEvent args)
    {
        if (!TryResolveMount(ent, out var point, out var grid, out _, out _))
            return;

        Vector2 gunshipVelocity;
        if (TryComp(grid, out DropshipTacticalHoverComponent? hover))
            gunshipVelocity = hover.GunshipLinearVelocity;
        else if (TryComp(args.User, out GunshipPilotHudComponent? hud))
            gunshipVelocity = hud.LinearVelocity;
        else
            return;

        var inheritedAlready = _physics.GetMapLinearVelocity(point.Owner);
        var velocityCorrection = gunshipVelocity - inheritedAlready;
        if (velocityCorrection == Vector2.Zero)
            return;

        foreach (var (projectile, _) in args.Ammo)
        {
            if (projectile is not { } uid || !TryComp(uid, out PhysicsComponent? body))
                continue;

            _physics.SetLinearVelocity(uid, body.LinearVelocity + velocityCorrection, body: body);
        }
    }

    public bool TryResolveMount(
        Entity<GunshipDirectFireWeaponComponent> weapon,
        out Entity<GunshipDirectFirePointComponent> point,
        out EntityUid grid,
        out EntityUid? ammoUid,
        out DropshipAmmoComponent? ammo)
    {
        point = default;
        grid = default;
        ammoUid = null;
        ammo = null;

        if (!_containers.TryGetContainingContainer((weapon.Owner, null), out var weaponContainer) ||
            !TryComp(weaponContainer.Owner, out GunshipDirectFirePointComponent? directPoint) ||
            !TryComp(weaponContainer.Owner, out DropshipWeaponPointComponent? weaponPoint) ||
            weaponContainer.ID != weaponPoint.WeaponContainerSlotId ||
            Transform(weaponContainer.Owner).GridUid is not { } mountGrid)
        {
            return false;
        }

        point = (weaponContainer.Owner, directPoint);
        grid = mountGrid;

        if (!_containers.TryGetContainer(point.Owner, weaponPoint.AmmoContainerSlotId, out var ammoContainer))
            return true;

        foreach (var contained in ammoContainer.ContainedEntities)
        {
            if (!TryComp(contained, out DropshipAmmoComponent? foundAmmo) ||
                MetaData(contained).EntityPrototype?.ID != weapon.Comp.AmmoPrototype.Id)
            {
                continue;
            }

            ammoUid = contained;
            ammo = foundAmmo;
            break;
        }

        return true;
    }
}
