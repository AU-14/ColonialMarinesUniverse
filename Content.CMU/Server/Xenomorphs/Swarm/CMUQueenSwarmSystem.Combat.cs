using System.Numerics;
using Content.Shared._RMC14.Entrenching;
using Content.Shared.Physics;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Server.CMU14.Xenomorphs.Swarm;

public sealed partial class CMUQueenSwarmSystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;

    private EntityUid? SelectEnemy(EntityUid member, Dictionary<EntityUid, TimeSpan> sightings, Dictionary<EntityUid, int> assigned)
    {
        EntityUid? target = null;
        var bestScore = float.MaxValue;
        foreach (var hostile in sightings.Keys)
        {
            if (!Transform(member).Coordinates.TryDistance(EntityManager, _transform, Transform(hostile).Coordinates, out var distance))
                continue;
            // Each committed lesser makes another nearby enemy more attractive,
            // while distance still discourages running past a much closer threat.
            var score = distance + assigned.GetValueOrDefault(hostile) * 3f;
            if (score >= bestScore)
                continue;
            target = hostile;
            bestScore = score;
        }
        return target;
    }

    private EntityUid? BlockingBarricade(EntityUid member, EntityUid enemy)
    {
        var from = _transform.GetMapCoordinates(member);
        var to = _transform.GetMapCoordinates(enemy);
        var delta = to.Position - from.Position;
        if (from.MapId != to.MapId || delta.LengthSquared() < 0.01f)
            return null;
        var ray = new CollisionRay(from.Position, Vector2.Normalize(delta),
            (int) (CollisionGroup.Impassable | CollisionGroup.InteractImpassable | CollisionGroup.BarricadeImpassable));
        // Only breach an obstruction immediately ahead on the way to an enemy.
        // A wall in front must prevent selecting a barricade behind it.
        foreach (var hit in _physics.IntersectRayWithPredicate(from.MapId, ray, Math.Min(delta.Length(), 2f),
                     obstacle => obstacle == member || obstacle == enemy, returnOnFirstHit: true))
        {
            return HasComp<BarricadeComponent>(hit.HitEntity) && Transform(hit.HitEntity).Anchored
                ? hit.HitEntity
                : null;
        }
        return null;
    }
}
