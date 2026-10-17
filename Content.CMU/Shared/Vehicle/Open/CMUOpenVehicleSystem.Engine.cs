using System.Numerics;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;

namespace Content.Shared.CMU14.Vehicle.Open;

public sealed partial class CMUOpenVehicleSystem
{
    [Dependency] private HardpointSystem _hardpoints = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private (EntityUid Jeep, MapCoordinates Epicenter)? _blast;

    private void InitializeEngine()
    {
        SubscribeLocalEvent<CMUOpenVehicleComponent, ExplosionDamagePreparingEvent>(OnExplosionPreparing);
    }

    private void OnExplosionPreparing(Entity<CMUOpenVehicleComponent> ent, ref ExplosionDamagePreparingEvent args)
    {
        _blast = (ent, args.Epicenter);
    }

    /// <summary>
    /// The engine soaks up its share of a hit, all of a direct one, while it has integrity left; the
    /// rest goes on to the hull. Anti vehicle weapons hit it as hard as they hit the hull.
    /// </summary>
    private void AbsorbIntoEngine(Entity<CMUOpenVehicleComponent> ent, ref DamageModifyEvent args, float total, Vector2? hitPoint)
    {
        var jeep = ent.Comp;
        var share = hitPoint is { } at && jeep.EngineBay.Contains(at) ? jeep.EngineDirectHitShare : jeep.EngineDamageShare;
        var hit = total * _hardpoints.CMUIncomingDamageMultiplier(args.Origin, args.Tool);
        var absorbed = MathF.Min(jeep.EngineIntegrity, hit * share);
        if (absorbed <= 0f)
            return;

        jeep.EngineIntegrity -= absorbed;
        args.Damage *= 1f - absorbed / hit;
        if (jeep.EngineIntegrity <= 0f && TryComp(ent, out VehicleComponent? vehicle) && vehicle.Operator is { } driver)
            _popup.PopupEntity(Loc.GetString("cmu-open-vehicle-engine-dead"), ent, driver, PopupType.MediumCaution);
    }

    /// <summary>
    /// Where a hit lands on the hull, in the vehicle's own space: a shot where its line enters the
    /// hull, a blast or an attacker at the point of the hull nearest to it. A hit from nowhere in
    /// particular lands nowhere.
    /// </summary>
    private Vector2? HitPoint(Entity<CMUOpenVehicleComponent> ent, DamageModifyEvent args)
    {
        var blast = _blast;
        _blast = null;

        var jeep = ent.Comp;
        var xform = Transform(ent);
        if (xform.MapUid == null)
            return null;

        var (position, rotation) = _transform.GetWorldPositionRotation(xform);
        Vector2 ToLocal(Vector2 world) => (-rotation).RotateVec(world - position);

        Vector2 hit;
        if (args.Tool is { } tool && HasComp<ProjectileComponent>(tool) && TryComp(tool, out TransformComponent? toolXform))
        {
            var at = ToLocal(_transform.GetWorldPosition(toolXform));
            var velocity = TryComp(tool, out PhysicsComponent? body) ? body.LinearVelocity : Vector2.Zero;
            if (velocity.LengthSquared() < 0.0001f && args.Origin is { } shooter && TryComp(shooter, out TransformComponent? shooterXform))
                velocity = _transform.GetWorldPosition(xform) - _transform.GetWorldPosition(shooterXform);

            var direction = (-rotation).RotateVec(velocity);
            hit = direction.LengthSquared() > 0.0001f && EntryPoint(jeep.HullBounds, at, Vector2.Normalize(direction)) is { } entry
                ? entry
                : jeep.HullBounds.ClosestPoint(at);
        }
        else if (args.Impact.Delivery == DamageImpactDelivery.Explosion && blast is { } b && b.Jeep == ent.Owner && b.Epicenter.MapId == xform.MapID)
        {
            hit = jeep.HullBounds.ClosestPoint(ToLocal(b.Epicenter.Position));
        }
        else if (args.Origin is { } origin && origin != ent.Owner && TryComp(origin, out TransformComponent? originXform) && originXform.MapID == xform.MapID)
        {
            hit = jeep.HullBounds.ClosestPoint(ToLocal(_transform.GetWorldPosition(originXform)));
        }
        else
        {
            return null;
        }

        return hit;
    }

    /// <summary>
    /// Where a line through <paramref name="at"/> along <paramref name="direction"/> enters the box,
    /// coming from behind.
    /// </summary>
    private static Vector2? EntryPoint(Box2 box, Vector2 at, Vector2 direction)
    {
        var from = at - direction * 10f;
        var enter = 0f;
        var leave = float.MaxValue;
        for (var axis = 0; axis < 2; axis++)
        {
            var start = axis == 0 ? from.X : from.Y;
            var step = axis == 0 ? direction.X : direction.Y;
            var min = axis == 0 ? box.Left : box.Bottom;
            var max = axis == 0 ? box.Right : box.Top;
            if (MathF.Abs(step) < 0.0001f)
            {
                if (start < min || start > max)
                    return null;

                continue;
            }

            var t1 = (min - start) / step;
            var t2 = (max - start) / step;
            enter = MathF.Max(enter, MathF.Min(t1, t2));
            leave = MathF.Min(leave, MathF.Max(t1, t2));
            if (enter > leave)
                return null;
        }

        return from + direction * enter;
    }
}
