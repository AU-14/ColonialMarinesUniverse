using System.Linq;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Shared.Interaction;

public abstract partial class SharedInteractionSystem
{
    // Value state keeps nested and parallel range queries independent. Ordinary targets
    // need neither a delegate closure nor a collection of special collision exemptions.
    private readonly record struct CMURayFilter(
        EntityUid? Origin,
        EntityUid? Target,
        Ignored? Predicate,
        HashSet<EntityUid>? Exemptions)
    {
        public bool Ignore(EntityUid uid) => uid == Target || uid == Origin ||
            (Predicate?.Invoke(uid) ?? false) || (Exemptions?.Contains(uid) ?? false);
    }

    private CMURayFilter CMUCreateRayFilter(
        MapCoordinates originCoords,
        EntityUid target,
        MapCoordinates targetCoords,
        Angle targetRotation,
        CollisionGroup collisionMask,
        Ignored? predicate,
        EntityUid? ignoreOrigin)
    {
        HashSet<EntityUid>? ignored = null;
        if (_itemQuery.HasComp(target) && _physicsQuery.TryComp(target, out var physics) && physics.CanCollide)
        {
            // Preserve pickup of items embedded in walls, including between tile edges.
            var nearby = new HashSet<EntityUid>();
            _lookup.GetEntitiesInRange(target, 0.01f, nearby, LookupFlags.Static);
            foreach (var other in nearby)
            {
                if (target == other || !_physicsQuery.TryComp(other, out var body) ||
                    !body.CanCollide || ((int) collisionMask & body.CollisionLayer) == 0)
                {
                    continue;
                }

                (ignored ??= new HashSet<EntityUid>()).Add(other);
            }
        }
        else if (_wallMountQuery.TryComp(target, out var wallMount))
        {
            var ignoreAnchored = wallMount.Arc >= Math.Tau;
            if (!ignoreAnchored)
            {
                var angle = Angle.FromWorldVec(originCoords.Position - targetCoords.Position);
                var delta = (wallMount.Direction + targetRotation - angle).Reduced().FlipPositive();
                ignoreAnchored = delta < wallMount.Arc / 2 || Math.Tau - delta < wallMount.Arc / 2;
            }

            if (ignoreAnchored && _map.TryFindGridAt(targetCoords, out var gridUid, out var grid))
            {
                foreach (var anchored in _map.GetAnchoredEntities((gridUid, grid), targetCoords))
                    (ignored ??= new HashSet<EntityUid>()).Add(anchored);

                var nearby = new HashSet<EntityUid>();
                _lookup.GetEntitiesInRange(targetCoords.MapId, targetCoords.Position, 0.2f, nearby);
                foreach (var other in nearby)
                {
                    if (TryComp(other, out TransformComponent? xform) && xform.Anchored)
                        (ignored ??= new HashSet<EntityUid>()).Add(other);
                }
            }
        }

        return new CMURayFilter(ignoreOrigin, target, predicate, ignored);
    }

    private bool CMUInRangeUnobstructed(
        MapCoordinates origin,
        MapCoordinates other,
        float range,
        CollisionGroup collisionMask,
        CMURayFilter filter,
        bool checkAccess = true)
    {
        if (other.MapId != origin.MapId)
            return false;
        if (!checkAccess)
            return true;

        var dir = other.Position - origin.Position;
        var length = dir.Length();
        if (range > 0f && length > range)
            return false;
        if (MathHelper.CloseTo(length, 0))
            return true;

        if (length > MaxRaycastRange)
        {
            Log.Warning("InRangeUnobstructed check performed over extreme range. Limiting CollisionRay size.");
            length = MaxRaycastRange;
        }

        var ray = new CollisionRay(origin.Position, dir.Normalized(), (int) collisionMask);
        // This is a boolean obstruction query: no copy of the result collection or
        // additional hits are needed. The existing engine API accepts state by value.
        return !_broadphase.IntersectRayWithPredicate(origin.MapId, ray, filter,
            static (uid, state) => state.Ignore(uid), length, returnOnFirstHit: true).Any();
    }
}
