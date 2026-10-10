#pragma warning disable RA0002 // Arrange equivalent shot origins to test coordinate conversion.
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Projectiles;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class CMUProjectileGridAccuracyTest : GameTest
{
    [Test]
    public async Task MinigunAccuracyIsIndependentOfShotCoordinateParent()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetWorldPosition(map.Grid, new Vector2(1000, 1000));
            var bullet = SEntMan.SpawnEntity("AU14BulletMinigunHAZOPS9mm", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(Vector2.UnitX));
            var accuracy = SEntMan.GetComponent<RMCProjectileAccuracyComponent>(bullet);
            SEntMan.GetComponent<ProjectileComponent>(bullet).OnlyCollideWhenShot = false;
            var onGrid = map.GridCoords;
            var onMap = transform.ToCoordinates(transform.ToMapCoordinates(onGrid));
            var hits = 0;
            for (var seed = 0; seed < 32; seed++)
            {
                accuracy.GunSeed = seed;
                accuracy.ShotFrom = onGrid;
                accuracy.Dodged.Clear();
                var local = Collision();
                SEntMan.EventBus.RaiseLocalEvent(bullet, ref local);
                if (!local.Cancelled)
                    hits++;

                accuracy.ShotFrom = onMap;
                accuracy.Dodged.Clear();
                var world = Collision();
                SEntMan.EventBus.RaiseLocalEvent(bullet, ref world);
                Assert.That(world.Cancelled, Is.EqualTo(local.Cancelled),
                    "Equivalent world positions must give identical hit rolls across grid boundaries.");
            }
            Assert.That(hits, Is.GreaterThan(0));

            PreventCollideEvent Collision() => new(bullet, target,
                SEntMan.GetComponent<PhysicsComponent>(bullet), SEntMan.GetComponent<PhysicsComponent>(target), null!, null!);
        });
    }
}
