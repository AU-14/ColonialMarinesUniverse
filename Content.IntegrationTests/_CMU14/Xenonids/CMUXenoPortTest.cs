using System.Linq;
using System.Numerics;
using Content.Shared.CMU14.Threats.Mobs.Xeno.Caste.Warlock;
using Content.Shared._RMC14.Explosion.Components;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Shared.Actions.Components;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoPortTest
{

    [Test]
    public async Task WarlockPsychicShieldFreezesProjectileDuringPhysicsContact()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid warlock = default;
        EntityUid projectile = default;
        EntityUid action = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var transform = entMan.System<SharedTransformSystem>();

                warlock = entMan.SpawnEntity("CMXenoWarlock", map.GridCoords);
                transform.SetWorldRotationNoLerp(warlock, Direction.North.ToAngle());

                var actionEnt = SpawnAction(entMan);
                action = actionEnt.Owner;
                // Click one tile north of the warlock -> shield faces north.
                var actionEv = RaiseShieldPress(entMan, warlock, actionEnt, map.GridCoords.Offset(new Vector2(0, 1)));

                Assert.That(actionEv.Handled, Is.True);

                projectile = entMan.SpawnEntity("BulletPistol", map.GridCoords.Offset(new Vector2(0, 1.25f)));
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);
                physics.SetBodyStatus(projectile, projectilePhysics, BodyStatus.InAir);
                physics.SetLinearVelocity(projectile, new Vector2(0, -5), body: projectilePhysics);
            });

            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var warlockComp = entMan.GetComponent<CMUXenoWarlockComponent>(warlock);
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUXenoFrozenProjectileComponent>(projectile), Is.True);
                    Assert.That(warlockComp.FrozenProjectiles, Does.Contain(projectile));
                    Assert.That(projectilePhysics.CanCollide, Is.False);
                    Assert.That(physics.GetMapLinearVelocity(projectile, component: projectilePhysics).LengthSquared(), Is.EqualTo(0).Within(0.001f));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(projectile))
                    entMan.DeleteEntity(projectile);

                if (entMan.EntityExists(action))
                    entMan.DeleteEntity(action);

                if (entMan.EntityExists(warlock))
                    entMan.DeleteEntity(warlock);
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task WarlockPsychicShieldCancelReleasesFrozenProjectiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid warlock = default;
        EntityUid projectile = default;
        EntityUid action = default;
        var incoming = new Vector2(0, -10);

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var transform = entMan.System<SharedTransformSystem>();

                warlock = entMan.SpawnEntity("CMXenoWarlock", map.GridCoords);
                transform.SetWorldRotationNoLerp(warlock, Direction.North.ToAngle());

                var actionEnt = SpawnAction(entMan);
                action = actionEnt.Owner;
                // Click one tile north of the warlock -> shield faces north.
                var actionEv = RaiseShieldPress(entMan, warlock, actionEnt, map.GridCoords.Offset(new Vector2(0, 1)));

                Assert.That(actionEv.Handled, Is.True);

                projectile = entMan.SpawnEntity("BulletPistol", map.GridCoords.Offset(new Vector2(0, 1)));
                var warlockComp = entMan.GetComponent<CMUXenoWarlockComponent>(warlock);
                var shield = warlockComp.PsychicShieldSegments[0];
                var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);
                var shieldPhysics = entMan.GetComponent<PhysicsComponent>(shield);
                var projectileFixture = entMan.GetComponent<FixturesComponent>(projectile).Fixtures.Values.First();
                var shieldFixture = entMan.GetComponent<FixturesComponent>(shield).Fixtures.Values.First();

                physics.SetBodyStatus(projectile, projectilePhysics, BodyStatus.InAir);
                physics.SetLinearVelocity(projectile, incoming, body: projectilePhysics);

                var ev = new PreventCollideEvent(
                    shield,
                    projectile,
                    shieldPhysics,
                    projectilePhysics,
                    shieldFixture,
                    projectileFixture);
                entMan.EventBus.RaiseLocalEvent(shield, ref ev);

                Assert.That(ev.Cancelled, Is.True);
                Assert.That(projectileComp.ProjectileSpent, Is.False);
                Assert.That(entMan.HasComponent<CMUXenoFrozenProjectileComponent>(projectile), Is.True);
            });

            await pair.RunTicksSync(10);

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var transform = entMan.System<SharedTransformSystem>();
                var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);

                transform.SetCoordinates(warlock, map.GridCoords.Offset(new Vector2(0.25f, 0)));
                var velocity = physics.GetMapLinearVelocity(projectile, component: projectilePhysics);

                Assert.Multiple(() =>
                {
                    Assert.That(entMan.HasComponent<CMUXenoFrozenProjectileComponent>(projectile), Is.False);
                    Assert.That(projectilePhysics.CanCollide, Is.True);
                    Assert.That(projectileComp.ProjectileSpent, Is.False);
                    Assert.That(velocity.X, Is.EqualTo(incoming.X).Within(0.001f));
                    Assert.That(velocity.Y, Is.EqualTo(incoming.Y).Within(0.001f));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(projectile))
                    entMan.DeleteEntity(projectile);

                if (entMan.EntityExists(action))
                    entMan.DeleteEntity(action);

                if (entMan.EntityExists(warlock))
                    entMan.DeleteEntity(warlock);
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task WarlockPsychicShieldDetonationReflectsFrozenProjectiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid warlock = default;
        EntityUid projectile = default;
        EntityUid action = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var physics = entMan.System<SharedPhysicsSystem>();
                var transform = entMan.System<SharedTransformSystem>();

                warlock = entMan.SpawnEntity("CMXenoWarlock", map.GridCoords);
                transform.SetWorldRotationNoLerp(warlock, Direction.North.ToAngle());

                var actionEnt = SpawnAction(entMan);
                action = actionEnt.Owner;
                // Click one tile north of the warlock -> shield faces north.
                var actionEv = RaiseShieldPress(entMan, warlock, actionEnt, map.GridCoords.Offset(new Vector2(0, 1)));

                Assert.That(actionEv.Handled, Is.True);

                projectile = entMan.SpawnEntity("BulletPistol", map.GridCoords.Offset(new Vector2(0, 1)));
                var warlockComp = entMan.GetComponent<CMUXenoWarlockComponent>(warlock);
                var shield = warlockComp.PsychicShieldSegments[0];
                var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);
                var shieldPhysics = entMan.GetComponent<PhysicsComponent>(shield);
                var projectileFixture = entMan.GetComponent<FixturesComponent>(projectile).Fixtures.Values.First();
                var shieldFixture = entMan.GetComponent<FixturesComponent>(shield).Fixtures.Values.First();

                physics.SetBodyStatus(projectile, projectilePhysics, BodyStatus.InAir);
                physics.SetLinearVelocity(projectile, new Vector2(0, -10), body: projectilePhysics);

                var ev = new PreventCollideEvent(
                    shield,
                    projectile,
                    shieldPhysics,
                    projectilePhysics,
                    shieldFixture,
                    projectileFixture);
                entMan.EventBus.RaiseLocalEvent(shield, ref ev);

                Assert.That(ev.Cancelled, Is.True);
                Assert.That(entMan.HasComponent<CMUXenoFrozenProjectileComponent>(projectile), Is.True);

                // Shield is up: a single press detonates.
                // Shield is up: any click detonates. Target is ignored.
                var detonateEv = RaiseShieldPress(entMan, warlock, actionEnt, map.GridCoords);

                var velocity = physics.GetMapLinearVelocity(projectile, component: projectilePhysics);
                Assert.Multiple(() =>
                {
                    Assert.That(detonateEv.Handled, Is.True);
                    Assert.That(entMan.HasComponent<CMUXenoFrozenProjectileComponent>(projectile), Is.False);
                    Assert.That(projectilePhysics.CanCollide, Is.True);
                    Assert.That(projectileComp.ProjectileSpent, Is.False);
                    Assert.That(velocity.Length(), Is.EqualTo(10).Within(0.001f));
                    Assert.That(velocity.Y, Is.GreaterThan(0));
                });
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (entMan.EntityExists(projectile))
                    entMan.DeleteEntity(projectile);

                if (entMan.EntityExists(action))
                    entMan.DeleteEntity(action);

                if (entMan.EntityExists(warlock))
                    entMan.DeleteEntity(warlock);
            });

            await pair.CleanReturnAsync();
        }
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static CMUXenoPsychicShieldActionEvent RaiseShieldPress(IEntityManager entMan, EntityUid warlock, EntityUid action, EntityCoordinates target)
    {
        var ev = new CMUXenoPsychicShieldActionEvent
        {
            Performer = warlock,
            Action = action,
            Target = target,
        };
        entMan.EventBus.RaiseLocalEvent(warlock, ev);
        return ev;
    }

}
