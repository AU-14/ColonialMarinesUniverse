using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Weapons.Hitscan;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.CMU14.Weapons;

[TestFixture]
public sealed class CMUFixtureHitscanTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  id: CMUTestHitscanCircle
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      circle:
        shape:
          !type:PhysShapeCircle
          radius: 0.5
        hard: true
        layer: [Opaque]
  - type: Damageable
  - type: Injurable
""";

    [Test]
    public async Task Xm99SkipsBoundingBoxCornersAndHonorsExplicitTargets()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            // Keep the attached fixtures within the grid's actual spatial bounds.
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 5; x++)
            for (var y = -2; y <= 4; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var origin = new EntityCoordinates(map.Grid, new Vector2(0, -1.2f));
            var shooter = SEntMan.SpawnEntity(null, origin);
            var corner = SEntMan.SpawnEntity("CMUTestHitscanCircle", new EntityCoordinates(map.Grid, new Vector2(2, 0)));
            var target = SEntMan.SpawnEntity("CMUTestHitscanCircle", new EntityCoordinates(map.Grid, new Vector2(4, 2.8f)));
            var shot = SEntMan.SpawnEntity("AU14PlasmaShot", origin);
            var damage = Server.System<DamageableSystem>();
            Fire(null);
            Assert.That(damage.GetTotalDamage(corner).Float(), Is.Zero, "The near circle's AABB corner is not solid.");
            Assert.That(damage.GetTotalDamage(target).Float(), Is.GreaterThan(0));

            var protectedTarget = SEntMan.SpawnEntity("CMUTestHitscanCircle",
                new EntityCoordinates(map.Grid, new Vector2(2, .8f)));
            SEntMan.AddComponent<RequireProjectileTargetComponent>(protectedTarget);
            Fire(null);
            Assert.That(damage.GetTotalDamage(protectedTarget).Float(), Is.Zero);
            Fire(protectedTarget);
            Assert.That(damage.GetTotalDamage(protectedTarget).Float(), Is.GreaterThan(0));

            void Fire(EntityUid? aimedAt)
            {
                var ev = new HitscanTraceEvent
                {
                    FromCoordinates = origin, ShotDirection = Vector2.Normalize(Vector2.One),
                    Gun = shooter, Shooter = shooter, Target = aimedAt,
                };
                SEntMan.EventBus.RaiseLocalEvent(shot, ref ev);
            }
        });
    }

    [Test]
    public async Task RotatedGridCastUsesMapCoordinatesAndBlocksInsideOrigin()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            transform.SetWorldRotation(map.Grid, Angle.FromDegrees(90));
            var target = SEntMan.SpawnEntity("CMUTestHitscanCircle", new EntityCoordinates(map.Grid, new Vector2(2, 0)));
            var origin = transform.ToMapCoordinates(new EntityCoordinates(map.Grid, Vector2.Zero));
            var center = transform.GetWorldPosition(target);
            var direction = Vector2.Normalize(center - origin.Position);
            var ignored = SEntMan.SpawnEntity(null, new EntityCoordinates(map.Grid, Vector2.Zero));
            var cast = Server.System<CMUFixtureHitscanSystem>();
            var hits = cast.Cast(origin.MapId, new CollisionRay(origin.Position, direction, (int) CollisionGroup.Opaque), 5, ignored);
            Assert.That(hits[0].HitEntity, Is.EqualTo(target));
            Assert.That(Vector2.Distance(hits[0].HitPos, center - direction * .5f), Is.LessThan(.001f));
            var inside = cast.Cast(origin.MapId, new CollisionRay(center, direction, (int) CollisionGroup.Opaque), 5, ignored);
            Assert.That(inside[0].HitEntity, Is.EqualTo(target));
            Assert.That(inside[0].Distance, Is.Zero);
        });
    }
}
