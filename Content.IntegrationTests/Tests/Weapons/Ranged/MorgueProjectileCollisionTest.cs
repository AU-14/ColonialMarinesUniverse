using Content.Server.Projectiles;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.IntegrationTests.Tests.Weapons.Ranged;

[TestFixture]
[TestOf(typeof(RequireProjectileTargetSystem))]
public sealed class MorgueProjectileCollisionTest
{
    private const string TestProjectile = "BaseBulletPractice";

    [TestCase("Morgue")]
    [TestCase("Crematorium")]
    [TestCase("CMMorgue")]
    [TestCase("CMCrematorium")]
    public async Task StoredShooterCollidesWithMorgue(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        var containers = server.System<SharedContainerSystem>();
        var entMan = server.EntMan;
        var projectiles = server.System<ProjectileSystem>();
        var storage = server.System<EntityStorageSystem>();

        await server.WaitAssertion(() =>
        {
            var target = entMan.SpawnEntity(prototype, map.GridCoords);
            var shooter = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var projectile = entMan.SpawnEntity(TestProjectile, MapCoordinates.Nullspace);

            Assert.That(
                entMan.HasComponent<RequireProjectileTargetComponent>(target),
                Is.True,
                $"{prototype} must require explicit projectile targeting.");

            var requiredLayer = (int) (CollisionGroup.MachineLayer | CollisionGroup.BulletImpassable);
            var targetFixtures = entMan.GetComponent<FixturesComponent>(target);

            Assert.That(
                targetFixtures.Fixtures.Values.Any(fixture =>
                    (fixture.CollisionLayer & requiredLayer) == requiredLayer),
                Is.True,
                $"{prototype} must use MachineLayer and explicitly block bullets.");

            var targetFixture = targetFixtures.Fixtures.Values.First(fixture =>
                (fixture.CollisionLayer & requiredLayer) == requiredLayer);
            var targetBody = entMan.GetComponent<PhysicsComponent>(target);

            var projectileFixtures = entMan.GetComponent<FixturesComponent>(projectile);
            var projectileFixture = projectileFixtures.Fixtures["projectile"];
            var projectileBody = entMan.GetComponent<PhysicsComponent>(projectile);
            var projectileComponent = entMan.GetComponent<ProjectileComponent>(projectile);

            Assert.That(
                projectileFixture.CollisionMask & targetFixture.CollisionLayer,
                Is.Not.EqualTo(0),
                $"{TestProjectile} must physically collide with {prototype}.");

            projectiles.SetShooter(projectile, projectileComponent, shooter);

            Assert.That(containers.IsEntityOrParentInContainer(shooter), Is.False);

            var outsideEvent = new PreventCollideEvent(
                target,
                projectile,
                targetBody,
                projectileBody,
                targetFixture,
                projectileFixture);

            entMan.EventBus.RaiseLocalEvent(target, ref outsideEvent);

            Assert.That(
                outsideEvent.Cancelled,
                Is.True,
                $"Untargeted outside shots should pass over {prototype}.");

            Assert.That(
                storage.Insert(shooter, target),
                Is.True,
                $"Failed to insert the shooter into {prototype}.");
            Assert.That(containers.IsEntityOrParentInContainer(shooter), Is.True);

            var insideEvent = new PreventCollideEvent(
                target,
                projectile,
                targetBody,
                projectileBody,
                targetFixture,
                projectileFixture);

            entMan.EventBus.RaiseLocalEvent(target, ref insideEvent);

            Assert.That(
                insideEvent.Cancelled,
                Is.False,
                $"Shots fired from inside {prototype} must collide with it.");
        });

        await pair.CleanReturnAsync();
    }
}
