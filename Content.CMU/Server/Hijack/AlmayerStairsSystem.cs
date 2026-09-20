using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.Hijack;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.CMU14.Hijack;

public sealed class AlmayerStairsSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _maps = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private CMUZLevelsSystem _zLevels = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly Dictionary<EntityUid, (EntityUid Stairs, EntityUid Map)> _pending = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<InputMoverComponent, MoveEvent>(OnMove);
    }

    private void OnMove(Entity<InputMoverComponent> ent, ref MoveEvent args)
    {
        // Spawning, container insertion and deletion also emit MoveEvent.
        if (TerminatingOrDeleted(ent) || Transform(ent).MapUid is not { } map ||
            !HasComp<CMUZLevelMapComponent>(map) ||
            (args.NewPosition.EntityId != map && !HasComp<CMUZLevelDeckComponent>(args.NewPosition.EntityId)) ||
            !TryComp<TransformComponent>(args.OldPosition.EntityId, out _) ||
            !TryComp<TransformComponent>(args.NewPosition.EntityId, out _))
            return;
        var oldWorld = _transform.ToMapCoordinates(args.OldPosition);
        var newWorld = _transform.ToMapCoordinates(args.NewPosition);
        if (oldWorld.MapId != newWorld.MapId)
            return;
        // Observers are parented to the map, not to the deck grid. Resolve the
        // supporting deck spatially for both observers and living movers.
        if ((!_maps.TryFindGridAt(newWorld, out var gridUid, out var grid) &&
             !_maps.TryFindGridAt(oldWorld, out gridUid, out grid)) ||
            !HasComp<CMUZLevelDeckComponent>(gridUid))
            return;
        var position = _transform.ToCoordinates(gridUid, newWorld).Position;
        var previous = _transform.ToCoordinates(gridUid, oldWorld).Position;
        var oldIndex = new Vector2i((int) MathF.Floor(previous.X), (int) MathF.Floor(previous.Y));
        var newIndex = new Vector2i((int) MathF.Floor(position.X), (int) MathF.Floor(position.Y));
        var tiles = oldIndex == newIndex ? new[] { newIndex } : new[] { oldIndex, newIndex };
        foreach (var index in tiles)
        foreach (var uid in _maps.GetAnchoredEntities(grid.Owner, grid, index))
        {
            if (!TryComp<CMUAlmayerStairsComponent>(uid, out var stairs))
                continue;

            var movement = position - previous;
            // Trigger before a wall on the OTHER side of this stair edge can
            // stop horizontal physics. CMSS13 handles this in mob/pre_move.
            if (Vector2.Dot(movement, stairs.Direction) > 0 &&
                // A human's collider stops ~0.15 tiles past the center when a
                // wall occupies the next tile. Trigger before that contact.
                Vector2.Dot(position - Transform(uid).LocalPosition, stairs.Direction) >= 0.05f)
                _pending[ent] = (uid, map);
        }
    }

    public override void Update(float frameTime)
    {
        foreach (var (user, pending) in _pending)
        {
            if (TerminatingOrDeleted(user) || TerminatingOrDeleted(pending.Stairs) ||
                Transform(user).MapUid != pending.Map ||
                !TryComp<CMUAlmayerStairsComponent>(pending.Stairs, out var stairs))
                continue;

            Traverse(user, (pending.Stairs, stairs));
        }
        _pending.Clear();
    }

    public bool Traverse(EntityUid user, Entity<CMUAlmayerStairsComponent> stairs)
    {
        var transform = Transform(stairs);
        if (Transform(user).MapUid != transform.MapUid || transform.MapUid is not { } map)
            return false;
        var target = _transform.GetWorldPosition(stairs) +
                     _transform.GetWorldRotation(transform.ParentUid).RotateVec(stairs.Comp.Direction);
        if (!_zLevels.TryProjectToZMap((map, null), stairs.Comp.Offset, target, out var destination, out _))
            return false;
        foreach (var uid in _lookup.GetEntitiesIntersecting(destination.MapId, Box2.CenteredAround(target, new Vector2(0.3f))))
        {
            if (uid == user || HasComp<MapGridComponent>(uid) ||
                !TryComp<PhysicsComponent>(uid, out var body) || !body.CanCollide ||
                (body.CollisionLayer & (int) CollisionGroup.MobMask) == 0)
                continue;

            // A crate or closed door on the landing must not be bypassed.
            _transform.SetCoordinates(user, transform.Coordinates);
            return false;
        }
        if (!_zLevels.TryMove(user, stairs.Comp.Offset, worldPosition: target))
            return false;

        if (TryComp<CMUZPhysicsComponent>(user, out var physics))
        {
            _zLevels.SetZVelocity((user, physics), 0);
            _zLevels.SetZLocalPosition((user, physics), 0.05f);
        }
        return true;
    }
}
