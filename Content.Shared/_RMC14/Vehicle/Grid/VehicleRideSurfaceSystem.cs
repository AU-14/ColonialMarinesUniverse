using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Stunnable;
using Content.Shared.Vehicle.Components;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared.Vehicle;

public sealed partial class VehicleRideSurfaceSystem : EntitySystem
{
    private const float TransformPositionEpsilon = 0.000001f;
    private const float TransformRotationEpsilon = 0.0001f;
    private const float ClimbDownExitOffset = 0.1f;
    private const float ClimbDownCancelDistance = 0.2f;
    private const float ClimbOntoSurfaceInset = 0.2f;
    private const float RiderLocalPositionEpsilon = 0.0001f;
    private const float RiderProjectileTargetRadius = 0.75f;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<EntityUid, RideSurfaceTransform> _lastTransforms = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _ridersByVehicle = new();
    private readonly Dictionary<EntityUid, EntityUid> _vehiclesByRider = new(); // CMU14: retain old membership across state replacement.
    private readonly HashSet<EntityUid> _movedRiders = new();
    private readonly List<EntityUid> _riderBuffer = new();

    private EntityQuery<BuckleComponent> _buckleQuery;
    private EntityQuery<MobStateComponent> _mobStateQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(GridVehicleMoverSystem));

        _buckleQuery = GetEntityQuery<BuckleComponent>();
        _mobStateQuery = GetEntityQuery<MobStateComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<VehicleRideSurfaceComponent, ComponentShutdown>(OnRideSurfaceShutdown);
        SubscribeLocalEvent<VehicleRideSurfaceComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<VehicleRideSurfaceComponent, DoAfterAttemptEvent<VehicleRideSurfaceClimbDoAfterEvent>>(OnClimbDoAfterAttempt);
        SubscribeLocalEvent<VehicleRideSurfaceComponent, VehicleRideSurfaceClimbDoAfterEvent>(OnClimbDoAfter);
        SubscribeLocalEvent<VehicleRideSurfaceComponent, DoAfterAttemptEvent<VehicleRideSurfaceClimbDownDoAfterEvent>>(OnClimbDownDoAfterAttempt);
        SubscribeLocalEvent<VehicleRideSurfaceComponent, VehicleRideSurfaceClimbDownDoAfterEvent>(OnClimbDownDoAfter);
        SubscribeLocalEvent<VehicleRideSurfaceComponent, PreventCollideEvent>(OnSurfacePreventCollide);
        SubscribeLocalEvent<VehicleRideSurfaceRiderComponent, PreventCollideEvent>(OnRiderPreventCollide);
        SubscribeLocalEvent<VehicleRideSurfaceRiderComponent, ComponentShutdown>(OnRiderShutdown);
        // CMU14: a restored or newly controlled rider need not have completed a climb on this client.
        SubscribeLocalEvent<VehicleRideSurfaceRiderComponent, ComponentStartup>(OnRiderStartup);
        SubscribeLocalEvent<VehicleRideSurfaceRiderComponent, LocalPlayerAttachedEvent>(OnRiderAttached);
        SubscribeLocalEvent<VehicleRideSurfaceRiderComponent, LocalPlayerDetachedEvent>(OnRiderDetached);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _movedRiders.Clear();

        var query = EntityQueryEnumerator<VehicleRideSurfaceComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var surface, out var xform))
        {
            var current = GetRideSurfaceTransform(xform);
            if (surface.Bounds.Count == 0 || current.MapId == MapId.Nullspace)
            {
                ClearSurface(uid);
                continue;
            }

            if (!_lastTransforms.TryGetValue(uid, out var previous) || previous.MapId != current.MapId)
            {
                _lastTransforms[uid] = current;
                continue;
            }

            if (IsSameTransform(previous, current))
                continue;

            CarryRiders(uid, surface, previous, current);
            _lastTransforms[uid] = current;
        }

        ValidateRiders();
    }

    private void OnRideSurfaceShutdown(Entity<VehicleRideSurfaceComponent> ent, ref ComponentShutdown args)
    {
        ClearSurface(ent.Owner);
    }

    private void OnRiderShutdown(Entity<VehicleRideSurfaceRiderComponent> ent, ref ComponentShutdown args)
    {
        UntrackRider(ent.Owner, ent.Comp.Vehicle);
    }

    // CMU14 Begin: reconstruct derived membership without replaying climb effects.
    private void OnRiderStartup(Entity<VehicleRideSurfaceRiderComponent> ent, ref ComponentStartup args)
    {
        RefreshRider(ent);
    }

    private void OnRiderAttached(Entity<VehicleRideSurfaceRiderComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        RefreshRider(ent);
        ResetCarryPrediction();
    }

    private void OnRiderDetached(Entity<VehicleRideSurfaceRiderComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        UntrackRider(ent.Owner, ent.Comp.Vehicle);
    }

    /// <summary>
    /// Rebuilds the rider index after authoritative state or ownership changes.
    /// Called by the client owner of the rider's state notification.
    /// </summary>
    public void RefreshRider(Entity<VehicleRideSurfaceRiderComponent> rider)
    {
        if (!rider.Comp.Vehicle.IsValid() || (_net.IsClient && rider.Owner != _player.LocalEntity))
        {
            UntrackRider(rider.Owner, rider.Comp.Vehicle);
            return;
        }

        TrackRider(rider.Comp.Vehicle, rider.Owner);
    }

    /// <summary>
    /// Starts carry deltas from the restored vehicle transforms, after all server state has applied.
    /// Authoritative movement has already been applied to the rider and must not be carried again.
    /// </summary>
    public void ResetCarryPrediction()
    {
        if (!_net.IsClient)
            return;

        foreach (var vehicle in _ridersByVehicle.Keys)
        {
            if (_transformQuery.TryComp(vehicle, out var xform))
                _lastTransforms[vehicle] = GetRideSurfaceTransform(xform);
            else
                _lastTransforms.Remove(vehicle);
        }
    }
    // CMU14 End

    private void ClearSurface(EntityUid uid)
    {
        _lastTransforms.Remove(uid);

        if (!_ridersByVehicle.TryGetValue(uid, out var riders))
            return;

        _riderBuffer.Clear();
        _riderBuffer.AddRange(riders);
        foreach (var rider in _riderBuffer)
        {
            if (TryComp(rider, out VehicleRideSurfaceRiderComponent? riderComp))
                ClearRider((rider, riderComp));
            else
                RemCompDeferred<VehicleRideSurfaceRiderComponent>(rider);
        }

        _riderBuffer.Clear();
        _ridersByVehicle.Remove(uid);
    }

    private void TrackRider(EntityUid vehicle, EntityUid rider)
    {
        // CMU14: state may replace Vehicle before the old membership can be removed.
        if (_vehiclesByRider.TryGetValue(rider, out var previous) && previous != vehicle)
            UntrackRider(rider, previous);
        _vehiclesByRider[rider] = vehicle;

        if (!_ridersByVehicle.TryGetValue(vehicle, out var riders))
            _ridersByVehicle[vehicle] = riders = new HashSet<EntityUid>();

        riders.Add(rider);
    }

    private void UntrackRider(EntityUid rider, EntityUid vehicle)
    {
        // CMU14: shutdown/state replacement may already have cleared the component's Vehicle field.
        if (_vehiclesByRider.Remove(rider, out var trackedVehicle))
            vehicle = trackedVehicle;

        if (!vehicle.IsValid() || !_ridersByVehicle.TryGetValue(vehicle, out var riders))
            return;

        riders.Remove(rider);
        if (riders.Count == 0)
            _ridersByVehicle.Remove(vehicle);
    }

    private RideSurfaceTransform GetRideSurfaceTransform(TransformComponent xform)
    {
        var (position, rotation) = _transform.GetWorldPositionRotation(xform, _transformQuery);
        return new RideSurfaceTransform(xform.MapID, position, rotation);
    }

    private static bool Contains(VehicleRideSurfaceComponent surface, Vector2 localPoint, float padding)
    {
        padding = MathF.Max(0f, padding);
        foreach (var bounds in surface.Bounds)
        {
            if (bounds.IsValid() && bounds.Enlarged(padding).Contains(localPoint))
                return true;
        }

        return false;
    }

    private static bool CanStartClimbFrom(VehicleRideSurfaceComponent surface, Vector2 localPoint)
    {
        if (surface.ClimbBounds.Count == 0)
            return Contains(surface, localPoint, MathF.Max(0f, surface.ClimbRange));

        foreach (var bounds in surface.ClimbBounds)
        {
            if (bounds.IsValid() && bounds.Contains(localPoint))
                return true;
        }

        return false;
    }

    private static bool TryGetClosestSurfaceLocal(
        VehicleRideSurfaceComponent surface,
        Vector2 localPoint,
        out Vector2 closestLocal)
    {
        return TryGetClosestSurfaceLocal(surface, localPoint, out closestLocal, out _);
    }

    private static bool TryGetClosestSurfaceLocal(
        VehicleRideSurfaceComponent surface,
        Vector2 localPoint,
        out Vector2 closestLocal,
        out Box2 closestBounds)
    {
        closestLocal = default;
        closestBounds = default;
        var closestDistance = float.MaxValue;
        var found = false;

        foreach (var bounds in surface.Bounds)
        {
            if (!bounds.IsValid())
                continue;

            var clamped = new Vector2(
                Math.Clamp(localPoint.X, bounds.Left, bounds.Right),
                Math.Clamp(localPoint.Y, bounds.Bottom, bounds.Top));
            var distance = (clamped - localPoint).LengthSquared();
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            closestLocal = clamped;
            closestBounds = bounds;
            found = true;
        }

        return found;
    }

    private static float GetDistanceToClosestSurfaceEdge(VehicleRideSurfaceComponent surface, Vector2 localPoint)
    {
        var closestDistance = float.MaxValue;

        foreach (var bounds in surface.Bounds)
        {
            if (!bounds.IsValid() || !bounds.Contains(localPoint))
                continue;

            closestDistance = Math.Min(closestDistance, MathF.Abs(localPoint.X - bounds.Left));
            closestDistance = Math.Min(closestDistance, MathF.Abs(bounds.Right - localPoint.X));
            closestDistance = Math.Min(closestDistance, MathF.Abs(localPoint.Y - bounds.Bottom));
            closestDistance = Math.Min(closestDistance, MathF.Abs(bounds.Top - localPoint.Y));
        }

        return closestDistance == float.MaxValue ? 0f : closestDistance;
    }

    private static bool TryGetClimbOntoSurfacePoint(
        VehicleRideSurfaceComponent surface,
        RideSurfaceTransform transform,
        Vector2 worldPoint,
        out Vector2 targetPosition)
    {
        targetPosition = default;
        var localPoint = WorldToLocal(worldPoint, transform);
        if (!TryGetClosestSurfaceLocal(surface, localPoint, out var closestLocal, out var closestBounds))
            return false;

        targetPosition = LocalToWorld(InsetIntoSurface(closestLocal, closestBounds), transform);
        return true;
    }

    private static Vector2 InsetIntoSurface(Vector2 localPoint, Box2 bounds)
    {
        var toCenter = bounds.Center - localPoint;
        var distance = toCenter.Length();
        if (distance <= 0.0001f)
            return localPoint;

        var target = localPoint + toCenter / distance * MathF.Min(ClimbOntoSurfaceInset, distance);
        return new Vector2(
            Math.Clamp(target.X, bounds.Left, bounds.Right),
            Math.Clamp(target.Y, bounds.Bottom, bounds.Top));
    }

    private static bool TryGetClimbDownPosition(
        VehicleRideSurfaceComponent surface,
        RideSurfaceTransform transform,
        Vector2 worldPoint,
        out Vector2 targetPosition)
    {
        targetPosition = worldPoint;
        var localPoint = WorldToLocal(worldPoint, transform);
        var closestDistance = float.MaxValue;
        var found = false;

        foreach (var bounds in surface.Bounds)
        {
            if (!bounds.IsValid())
                continue;

            var clamped = new Vector2(
                Math.Clamp(localPoint.X, bounds.Left, bounds.Right),
                Math.Clamp(localPoint.Y, bounds.Bottom, bounds.Top));

            var leftDistance = MathF.Abs(clamped.X - bounds.Left);
            var rightDistance = MathF.Abs(bounds.Right - clamped.X);
            var bottomDistance = MathF.Abs(clamped.Y - bounds.Bottom);
            var topDistance = MathF.Abs(bounds.Top - clamped.Y);

            var distance = Math.Min(Math.Min(leftDistance, rightDistance), Math.Min(bottomDistance, topDistance));
            if (distance >= closestDistance)
                continue;

            closestDistance = distance;
            found = true;

            var softBorderPadding = MathF.Max(0f, surface.SoftBorderPadding) + ClimbDownExitOffset;
            if (distance == leftDistance)
                clamped.X = bounds.Left - softBorderPadding;
            else if (distance == rightDistance)
                clamped.X = bounds.Right + softBorderPadding;
            else if (distance == bottomDistance)
                clamped.Y = bounds.Bottom - softBorderPadding;
            else
                clamped.Y = bounds.Top + softBorderPadding;

            targetPosition = LocalToWorld(clamped, transform);
        }

        return found;
    }

    private static Vector2 WorldToLocal(Vector2 world, RideSurfaceTransform transform)
    {
        return (-transform.Rotation).RotateVec(world - transform.Position);
    }

    private static Vector2 LocalToWorld(Vector2 local, RideSurfaceTransform transform)
    {
        return transform.Position + transform.Rotation.RotateVec(local);
    }

    private static bool IsSameTransform(RideSurfaceTransform previous, RideSurfaceTransform current)
    {
        return (current.Position - previous.Position).LengthSquared() <= TransformPositionEpsilon &&
               MathF.Abs((float) (current.Rotation - previous.Rotation).Theta) <= TransformRotationEpsilon;
    }

    private readonly record struct RideSurfaceTransform(MapId MapId, Vector2 Position, Angle Rotation);
}
