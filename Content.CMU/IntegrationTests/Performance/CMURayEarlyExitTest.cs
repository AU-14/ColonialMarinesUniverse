using Content.IntegrationTests.Fixtures;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Performance;

[TestFixture]
public sealed class CMURayEarlyExitTest : GameTest
{
    private const int Mask = 33554432;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestRayStatic
  components:
  - type: Physics
    bodyType: Static
    canCollide: true
  - type: Fixtures
    fixtures:
      body:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.4,-0.4,0.4,0.4'
        hard: true
        layer: 33554432
        mask: 0

- type: entity
  id: CMUTestRayDynamic
  parent: CMUTestRayStatic
  components:
  - type: Physics
    bodyType: Dynamic
    canCollide: true

- type: entity
  id: CMUTestRaySoft
  parent: CMUTestRayStatic
  components:
  - type: Fixtures
    fixtures:
      body:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.4,-0.4,0.4,0.4'
        hard: false
        layer: 33554432
        mask: 0
";

    [Test]
    public async Task FirstHitAndAllHitsRespectFiltersDistanceAndRotatedGrids()
    {
        var map = await Pair.CreateTestMap();
        EntityUid ignored = default;
        EntityUid wall = default;
        EntityUid dynamic = default;
        await Server.WaitPost(() =>
        {
            var transforms = Server.System<SharedTransformSystem>();
            transforms.SetLocalPosition(map.Grid, new Vector2(10, 20));
            transforms.SetLocalRotation(map.Grid, Angle.FromDegrees(90));
            SEntMan.SpawnEntity("CMUTestRaySoft", new EntityCoordinates(map.Grid, 1, 0.5f));
            ignored = SEntMan.SpawnEntity("CMUTestRayStatic", new EntityCoordinates(map.Grid, 3, 0.5f));
            wall = SEntMan.SpawnEntity("CMUTestRayStatic", new EntityCoordinates(map.Grid, 5, 0.5f));
            dynamic = SEntMan.SpawnEntity("CMUTestRayDynamic", new EntityCoordinates(map.Grid, 12, 0.5f));
        });
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var physics = Server.System<SharedPhysicsSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var matrix = transform.GetWorldMatrix(map.Grid);
            var origin = Vector2.Transform(new Vector2(0, 0.5f), matrix);
            var direction = Vector2.Normalize(Vector2.Transform(new Vector2(1, 0.5f), matrix) - origin);
            var ray = new CollisionRay(origin, direction, Mask);
            var mapId = SComp<TransformComponent>(map.Grid).MapID;
            var first = physics.IntersectRayWithPredicate(mapId, ray, ignored,
                static (uid, skip) => uid == skip, 20).ToArray();
            var all = physics.IntersectRayWithPredicate(mapId, ray, ignored,
                static (uid, skip) => uid == skip, 20, returnOnFirstHit: false).ToArray();

            Assert.That(first.Select(hit => hit.HitEntity), Is.EqualTo(new[] { wall }));
            Assert.That(all.Select(hit => hit.HitEntity), Is.EqualTo(new[] { wall, dynamic }));
            // Fixture AABBs include the shape's collision skin.
            var distance = 5f - 0.4f - PhysicsConstants.PolygonRadius;
            Assert.That(first[0].Distance, Is.EqualTo(distance).Within(0.001f));
            Assert.That(Vector2.Distance(first[0].HitPos, origin + direction * distance), Is.LessThan(0.001f));
            Assert.That(first[0].Distance, Is.EqualTo(all[0].Distance));
            Assert.That(first[0].HitPos, Is.EqualTo(all[0].HitPos));
            Assert.That(physics.IntersectRayWithPredicate(mapId, ray, ignored,
                static (uid, skip) => uid == skip, 2), Is.Empty);
            Assert.That(physics.IntersectRayWithPredicate(mapId, new CollisionRay(origin, direction, 0),
                20), Is.Empty);
            var dynamicOnly = physics.IntersectRayWithPredicate(mapId, ray, (ignored, wall),
                static (uid, skip) => uid == skip.ignored || uid == skip.wall, 20).ToArray();
            Assert.That(dynamicOnly.Select(hit => hit.HitEntity), Is.EqualTo(new[] { dynamic }));
        });
    }
}
