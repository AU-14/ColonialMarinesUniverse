using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Administration.Logs;
using Content.Shared.CMU14.Weapons.Gauss;
using Content.Shared.CMU14.Weapons.Hitscan;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Hitscan.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Weapons.Gauss;

/// <summary>
///     Traces every gauss shot, bouncing charged beams off walls, and handles the overcharge's chance to ignite.
///     Raises the same hitscan events as the basic raycast so damage and hit effects work unchanged.
/// </summary>
public sealed class CMUGaussRaycastSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private CMUFixtureHitscanSystem _fixtureHitscan = default!;
    [Dependency] private ISharedAdminLogManager _log = default!;
    [Dependency] private SharedRMCFlammableSystem _flammable = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private HitscanBasicRaycastSystem _raycast = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // Nudge off the wall face so the next leg doesn't start inside it.
    private const float BounceOffset = 0.05f;

    /// <summary>How close, in tiles, the crosshair has to be to someone lying down for the beam to hit them.</summary>
    private const float AimTolerance = 0.6f;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGaussRaycastComponent, HitscanTraceEvent>(OnTrace);
        SubscribeLocalEvent<CMUGaussIgniteComponent, HitscanRaycastFiredEvent>(OnIgniteHit);
    }

    private void OnTrace(Entity<CMUGaussRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        var map = _transform.ToMapCoordinates(args.FromCoordinates);
        var origin = map.Position;
        var direction = args.ShotDirection.Normalized();
        var ignored = shooter;
        var target = args.Target;
        var aim = CompOrNull<CMUGaussGunComponent>(args.Gun)?.ShotAim;
        var inContainer = _container.IsEntityOrParentInContainer(shooter);
        var fixtureAccurate = HasComp<CMUFixtureHitscanComponent>(ent);

        for (var bounce = 0; bounce <= ent.Comp.MaxBounces; bounce++)
        {
            var ray = new CollisionRay(origin, direction, (int) ent.Comp.CollisionMask);
            IEnumerable<RayCastResults> results = fixtureAccurate
                ? _fixtureHitscan.Cast(map.MapId, ray, ent.Comp.MaxDistance, ignored)
                : _physics.IntersectRay(map.MapId, ray, ent.Comp.MaxDistance, ignored, false);
            var result = results.FirstOrNull(hit => inContainer || CanHit(hit.HitEntity, target, aim));

            var distance = result?.Distance ?? ent.Comp.MaxDistance;
            _raycast.FireEffects(_transform.ToCoordinates(new MapCoordinates(origin, map.MapId)), distance, direction.ToAngle(), ent, muzzleFlash: bounce == 0);

            if (result is not { } hit)
                return;

            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(hit.HitEntity):target} using {ToPrettyString(args.Gun):entity} (gauss beam, bounce {bounce}).");

            var data = new HitscanRaycastFiredData
            {
                ShotDirection = direction,
                Gun = args.Gun,
                Hitscan = ent.Owner,
                Shooter = args.Shooter,
                HitEntity = hit.HitEntity,
            };

            var attempt = new AttemptHitscanRaycastFiredEvent { Data = data };
            RaiseLocalEvent(ent, ref attempt);
            if (attempt.Cancelled)
                return;

            var fired = new HitscanRaycastFiredEvent { Data = data };
            RaiseLocalEvent(ent, ref fired);

            var strike = new HitscanRaycastStrikeEvent { Data = data };
            RaiseLocalEvent(hit.HitEntity, ref strike);

            if (bounce >= ent.Comp.MaxBounces || !CanBounceOff(hit.HitEntity))
                return;

            var normal = GetWallNormal(hit.HitEntity, hit.HitPos);
            direction = Vector2.Reflect(direction, normal);
            origin = hit.HitPos + normal * BounceOffset;
            ignored = hit.HitEntity;
            target = null;
        }
    }

    /// <summary>
    ///     Like other hitscans, the beam passes over anyone lying down unless they're what was shot at. For the
    ///     gauss gun that's the clicked target, or a downed, prone or crit mob the crosshair was on.
    /// </summary>
    private bool CanHit(EntityUid uid, EntityUid? target, MapCoordinates? aim)
    {
        if (uid == target || CompOrNull<RequireProjectileTargetComponent>(uid)?.Active != true)
            return true;

        if (aim is not { } aimAt || !HasComp<MobStateComponent>(uid))
            return false;

        var position = _transform.GetMapCoordinates(uid);
        return position.MapId == aimAt.MapId &&
               (position.Position - aimAt.Position).LengthSquared() <= AimTolerance * AimTolerance;
    }

    private bool CanBounceOff(EntityUid uid)
    {
        if (Deleted(uid) || HasComp<MobStateComponent>(uid))
            return false;

        return TryComp(uid, out PhysicsComponent? physics) && physics.BodyType == BodyType.Static;
    }

    /// <summary>
    ///     Walls are grid aligned boxes, so the face that was hit is whichever axis of the wall's local frame
    ///     the hit point sits furthest out along.
    /// </summary>
    private Vector2 GetWallNormal(EntityUid wall, Vector2 hitPos)
    {
        var xform = Transform(wall);
        var (center, rotation) = _transform.GetWorldPositionRotation(xform);
        var local = (-rotation).RotateVec(hitPos - center);
        var localNormal = Math.Abs(local.X) >= Math.Abs(local.Y)
            ? new Vector2(Math.Sign(local.X), 0)
            : new Vector2(0, Math.Sign(local.Y));

        if (localNormal == Vector2.Zero)
            localNormal = new Vector2(0, 1);

        return rotation.RotateVec(localNormal);
    }

    private void OnIgniteHit(Entity<CMUGaussIgniteComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity is not { } target || !HasComp<MobStateComponent>(target))
            return;

        if (!_random.Prob(ent.Comp.Chance))
            return;

        _flammable.Ignite((target, null), ent.Comp.Intensity, ent.Comp.Duration, null);
    }
}
