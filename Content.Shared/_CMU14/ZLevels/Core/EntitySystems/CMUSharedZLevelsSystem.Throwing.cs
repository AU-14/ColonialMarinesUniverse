using System.Numerics;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Throwing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem
{
    private const float ThrowUpZVelocity = 6.5f;
    private const float ThrowDownZVelocity = -4f;
    private const float ThrowDownLocalPosition = 0.95f;
    private readonly Dictionary<EntityUid, PendingZThrow> _pendingZThrows = new();
    private readonly Dictionary<EntityUid, RecentZThrowTransition> _recentZThrowTransitions = new();

    private void InitThrowing()
    {
        SubscribeLocalEvent<CMUZLevelViewerComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<CMUZLevelShooterComponent, BeforeThrowEvent>(OnBeforeThrow);
        SubscribeLocalEvent<CMUZPhysicsComponent, ThrownEvent>(OnThrown);
        SubscribeLocalEvent<CMUZPhysicsComponent, StopThrowEvent>(OnStopThrow);
        SubscribeLocalEvent<CMUZPhysicsComponent, EntityTerminatingEvent>(OnZThrowTerminating);

        _transform.OnGlobalMoveEvent += OnThrownMove;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _transform.OnGlobalMoveEvent -= OnThrownMove;
    }

    private void OnThrown(Entity<CMUZPhysicsComponent> ent, ref ThrownEvent args)
    {
        if (_pendingZThrows.ContainsKey(ent.Owner))
            return;

        if (args.User is not { } user ||
            !TryComp<CMUZLevelViewerComponent>(user, out var viewer) ||
            !viewer.LookUp)
        {
            return;
        }

        if (ent.Comp.Velocity >= ThrowUpZVelocity)
            return;

        Entity<CMUZPhysicsComponent?> nullableEnt = (ent.Owner, ent.Comp);
        SetZVelocity(nullableEnt, ThrowUpZVelocity);
    }

    private void OnBeforeThrow<T>(Entity<T> ent, ref BeforeThrowEvent args)
        where T : IComponent
    {
        if (_pendingZThrows.ContainsKey(args.ItemUid))
            return;

        if (Transform(args.PlayerUid).MapUid is not { } sourceMap)
            return;

        var from = _transform.GetWorldPosition(args.PlayerUid);
        var to = from + args.Direction;
        var offset = GetRequestedThrowOffset(args.PlayerUid);

        if (offset == 0)
        {
            if (TryQueuePendingZThrow(args.ItemUid, sourceMap, 1, from, to) ||
                TryQueuePendingZThrow(args.ItemUid, sourceMap, -1, from, to))
            {
                return;
            }

            return;
        }

        TryQueuePendingZThrow(args.ItemUid, sourceMap, offset, from, to);
    }

    private bool TryQueuePendingZThrow(EntityUid item, EntityUid sourceMap, int offset, Vector2 from, Vector2 to)
    {
        if (!TryMapOffset((sourceMap, null), offset, out var targetMap))
            return false;

        if (!TryFindZShotOpening(sourceMap, targetMap.Value.Owner, offset, from, to, out var opening))
            return false;

        var direction = to - from;
        direction = direction.LengthSquared() > 0.001f
            ? Vector2.Normalize(direction)
            : Vector2.Zero;

        _pendingZThrows[item] = new PendingZThrow(sourceMap, offset, targetMap.Value.Owner, opening, direction);
        return true;
    }

    private void OnStopThrow(Entity<CMUZPhysicsComponent> ent, ref StopThrowEvent args)
    {
        _pendingZThrows.Remove(ent.Owner);
    }

    private void OnZThrowTerminating(Entity<CMUZPhysicsComponent> ent, ref EntityTerminatingEvent args)
    {
        _pendingZThrows.Remove(ent.Owner);
        _recentZThrowTransitions.Remove(ent.Owner);
    }

    private int GetRequestedThrowOffset(EntityUid user)
    {
        if (TryComp<CMUZLevelShooterComponent>(user, out var shooter) &&
            shooter.ShootDown)
        {
            return -1;
        }

        if (TryComp<CMUZLevelViewerComponent>(user, out var viewer) &&
            viewer.LookUp)
        {
            return 1;
        }

        return 0;
    }

    private void OnThrownMove(ref MoveEvent args)
    {
        if (!_pendingZThrows.TryGetValue(args.Sender, out var pending) ||
            !TryComp<CMUZPhysicsComponent>(args.Sender, out var zPhysics) ||
            !TryComp<MapComponent>(pending.SourceMap, out var sourceMap))
        {
            return;
        }

        var oldMap = _transform.ToMapCoordinates(args.OldPosition);
        var newMap = _transform.ToMapCoordinates(args.NewPosition);
        if (oldMap.MapId != sourceMap.MapId ||
            newMap.MapId != sourceMap.MapId)
        {
            return;
        }

        if (!TryFindZShotOpening(
                pending.SourceMap,
                pending.TargetMap,
                pending.Offset,
                oldMap.Position,
                newMap.Position,
                out var opening))
        {
            return;
        }

        _pendingZThrows.Remove(args.Sender);
        ApplyPendingZThrow((args.Sender, zPhysics), pending, GetThrowTransitionPosition(pending, opening, newMap.Position));
    }

    private static Vector2 GetThrowTransitionPosition(PendingZThrow pending, Vector2 opening, Vector2 newPosition)
    {
        if (pending.Direction == Vector2.Zero)
            return opening;

        var overshoot = Vector2.Dot(newPosition - opening, pending.Direction);
        return overshoot > 0f
            ? opening + pending.Direction * overshoot
            : opening;
    }

    private void ApplyPendingZThrow(Entity<CMUZPhysicsComponent> ent, PendingZThrow pending, Vector2 position)
    {
        Entity<CMUZPhysicsComponent?> nullableEnt = (ent.Owner, ent.Comp);

        if (!TryComp<MapComponent>(pending.TargetMap, out var targetMap))
            return;

        _recentZThrowTransitions[ent.Owner] = new RecentZThrowTransition(
            pending.SourceMap,
            pending.TargetMap,
            pending.Opening);

        _transform.SetMapCoordinates(ent.Owner, new MapCoordinates(position, targetMap.MapId));
        if (pending.Offset > 0)
        {
            SetZLocalPosition(nullableEnt, 0f);
            SetZVelocity(nullableEnt, 0f);
            return;
        }

        SetZLocalPosition(nullableEnt, ThrowDownLocalPosition);
        SetZVelocity(nullableEnt, ThrowDownZVelocity);
    }

    private readonly record struct PendingZThrow(EntityUid SourceMap, int Offset, EntityUid TargetMap, Vector2 Opening, Vector2 Direction);
    private readonly record struct RecentZThrowTransition(EntityUid SourceMap, EntityUid TargetMap, Vector2 Opening);

    public bool TryGetRecentZThrowExplosionProjection(
        EntityUid uid,
        MapId epicenterMap,
        out MapCoordinates projection)
    {
        projection = default;

        if (!_recentZThrowTransitions.TryGetValue(uid, out var recent) ||
            !TryComp<MapComponent>(recent.TargetMap, out var targetMap) ||
            targetMap.MapId != epicenterMap ||
            !TryComp<MapComponent>(recent.SourceMap, out var sourceMap))
        {
            return false;
        }

        projection = new MapCoordinates(recent.Opening, sourceMap.MapId);
        return true;
    }
}
