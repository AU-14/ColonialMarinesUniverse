using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.CMU14.Weapons.Hitscan;

public sealed partial class CMUFixtureHitscanSystem : EntitySystem
{
    [Dependency] private FixtureSystem _fixtures = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private RayCastSystem _rays = default!;

    public List<RayCastResults> Cast(MapId map, CollisionRay ray, float distance, EntityUid ignored)
    {
        var results = new List<RayCastResults>();
        if (distance <= 0 || ray.Direction.LengthSquared() == 0)
            return results;

        var translation = Vector2.Normalize(ray.Direction) * distance;
        var hits = _rays.CastRay(map, ray.Position, translation, new QueryFilter
        {
            MaskBits = ray.CollisionMask,
            IsIgnored = entity => entity == ignored,
        });
        foreach (var hit in hits.Results)
            results.Add(new RayCastResults(hit.Fraction * distance, hit.Point, hit.Entity));

        // The new caster omits shapes containing the ray origin and unsupported edge/chain
        // shapes. Preserve blocking in those cases rather than letting shots escape solid cover.
        foreach (var candidate in _physics.IntersectRay(map, ray, distance, ignored, false))
        {
            if (!TryComp(candidate.HitEntity, out FixturesComponent? fixtures))
                continue;
            var transform = _physics.GetPhysicsTransform(candidate.HitEntity);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard || (fixture.CollisionLayer & ray.CollisionMask) == 0)
                    continue;
                if (fixture.Shape.ShapeType is not (ShapeType.Circle or ShapeType.Polygon))
                {
                    results.Add(candidate);
                    break;
                }
                if (_fixtures.TestPoint(fixture.Shape, transform, ray.Position))
                {
                    results.Add(new RayCastResults(0, ray.Position, candidate.HitEntity));
                    break;
                }
            }
        }

        results.Sort((left, right) => left.Distance.CompareTo(right.Distance));
        return results;
    }
}
