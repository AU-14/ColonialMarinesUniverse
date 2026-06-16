using System.Linq;
using System.Numerics;
using Content.Shared._CMU14.Xenonids.Warlock;
using Content.Shared._RMC14.Explosion.Components;
using Content.Shared._RMC14.Weapons.Ranged.Prediction;
using Content.Shared.Actions.Components;
using Content.Shared.Projectiles;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._CMU14.Xenonids;

[TestFixture]
public sealed class CMUXenoPortTest
{
    [Test]
    public async Task BullAndWarlockSpawnWithActionsAndEvolutionSources()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var runner = entMan.SpawnEntity("CMXenoRunner", map.GridCoords);
            var warrior = entMan.SpawnEntity("CMXenoWarrior", map.GridCoords);
            var bull = entMan.SpawnEntity("CMXenoBull", map.GridCoords);
            var warlock = entMan.SpawnEntity("CMXenoWarlock", map.GridCoords);

            try
            {
                Assert.That(HasAction(entMan, bull, "ActionXenoToggleCharging"), Is.True);
                Assert.That(HasAction(entMan, bull, "CMUActionXenoBullPlow"), Is.True);
                Assert.That(HasAction(entMan, bull, "CMUActionXenoBullHeadbutt"), Is.True);
                Assert.That(HasAction(entMan, bull, "CMUActionXenoBullGore"), Is.True);

                Assert.That(HasAction(entMan, warlock, "CMUActionXenoPsychicCrush"), Is.True);
                Assert.That(HasAction(entMan, warlock, "CMUActionXenoPsychicBlast"), Is.True);
                Assert.That(HasAction(entMan, warlock, "CMUActionXenoPsychicShield"), Is.True);
                Assert.That(HasAction(entMan, warlock, "ActionXenoTransferPlasmaHivelord"), Is.True);
                Assert.That(HasAction(entMan, warlock, "ActionXenoPsychicWhisper"), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(runner);
                entMan.DeleteEntity(warrior);
                entMan.DeleteEntity(bull);
                entMan.DeleteEntity(warlock);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WarlockVisualEffectPrototypesSpawn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var blastShockwave = entMan.SpawnEntity("CMUXenoPsychicBlastShockwave", map.GridCoords);
            var crushSmooth = entMan.SpawnEntity("CMUXenoPsychicCrushSmooth", map.GridCoords);
            var crushOrb = entMan.SpawnEntity("CMUXenoPsychicCrushOrb", map.GridCoords);
            var crushChannel = entMan.SpawnEntity("CMUXenoWarlockCrushChannelEffect", map.GridCoords);
            var blastChannel = entMan.SpawnEntity("CMUXenoWarlockBlastChannelEffect", map.GridCoords);
            var crushParticle = entMan.SpawnEntity("CMUXenoWarlockCrushParticles", map.GridCoords);
            var blastParticle = entMan.SpawnEntity("CMUXenoWarlockBlastParticles", map.GridCoords);
            var lanceParticle = entMan.SpawnEntity("CMUXenoWarlockLanceParticles", map.GridCoords);

            try
            {
                Assert.That(entMan.HasComponent<RMCExplosionShockWaveComponent>(blastShockwave), Is.True);
                Assert.That(entMan.Deleted(crushSmooth), Is.False);
                Assert.That(entMan.Deleted(crushOrb), Is.False);
                Assert.That(entMan.Deleted(crushChannel), Is.False);
                Assert.That(entMan.Deleted(blastChannel), Is.False);
                Assert.That(entMan.HasComponent<CMUXenoWarlockParticleEmitterComponent>(crushParticle), Is.True);
                Assert.That(entMan.HasComponent<CMUXenoWarlockParticleEmitterComponent>(blastParticle), Is.True);
                Assert.That(entMan.HasComponent<CMUXenoWarlockParticleEmitterComponent>(lanceParticle), Is.True);
                Assert.That(entMan.Deleted(crushParticle), Is.False);
                Assert.That(entMan.Deleted(blastParticle), Is.False);
                Assert.That(entMan.Deleted(lanceParticle), Is.False);
            }
            finally
            {
                entMan.DeleteEntity(blastShockwave);
                entMan.DeleteEntity(crushSmooth);
                entMan.DeleteEntity(crushOrb);
                entMan.DeleteEntity(crushChannel);
                entMan.DeleteEntity(blastChannel);
                entMan.DeleteEntity(crushParticle);
                entMan.DeleteEntity(blastParticle);
                entMan.DeleteEntity(lanceParticle);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WarlockPsychicShieldFreezesIncomingProjectiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var warlock = entMan.SpawnEntity("CMXenoWarlock", map.GridCoords);
            var action = SpawnAction(entMan);
            var projectile = entMan.SpawnEntity("BulletPistol", map.GridCoords.Offset(new Vector2(0, 1)));
            EntityUid? shield = null;

            try
            {
                var actionEv = new CMUXenoPsychicShieldActionEvent
                {
                    Performer = warlock,
                    Action = action,
                };
                entMan.EventBus.RaiseLocalEvent(warlock, actionEv);

                var warlockComp = entMan.GetComponent<CMUXenoWarlockComponent>(warlock);
                Assert.That(actionEv.Handled, Is.True);
                Assert.That(warlockComp.PsychicShieldSegments, Has.Count.EqualTo(1));

                shield = warlockComp.PsychicShieldSegments[0];
                var segment = entMan.GetComponent<CMUXenoPsychicShieldSegmentComponent>(shield.Value);
                var incoming = segment.Direction.GetOpposite().ToVec() * 10;

                var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);
                var shieldPhysics = entMan.GetComponent<PhysicsComponent>(shield.Value);
                var projectileFixture = entMan.GetComponent<FixturesComponent>(projectile).Fixtures.Values.First();
                var shieldFixture = entMan.GetComponent<FixturesComponent>(shield.Value).Fixtures.Values.First();

                Assert.That(shieldFixture.Hard, Is.True);
                Assert.That(entMan.HasComponent<IgnorePredictionHitComponent>(shield.Value), Is.True);

                physics.SetLinearVelocity(projectile, incoming, body: projectilePhysics);

                var ev = new PreventCollideEvent(
                    shield.Value,
                    projectile,
                    shieldPhysics,
                    projectilePhysics,
                    shieldFixture,
                    projectileFixture);
                entMan.EventBus.RaiseLocalEvent(shield.Value, ref ev);

                var frozen = entMan.HasComponent<CMUXenoFrozenProjectileComponent>(projectile);
                var velocity = physics.GetMapLinearVelocity(projectile, component: projectilePhysics);

                Assert.Multiple(() =>
                {
                    Assert.That(ev.Cancelled, Is.True);
                    Assert.That(frozen, Is.True);
                    Assert.That(projectilePhysics.CanCollide, Is.False);
                    Assert.That(velocity.LengthSquared(), Is.EqualTo(0).Within(0.001f));
                    Assert.That(warlockComp.FrozenProjectiles, Does.Contain(projectile));
                    Assert.That(projectileComp.DeleteOnCollide, Is.False);
                    Assert.That(projectileComp.ProjectileSpent, Is.False);
                });
            }
            finally
            {
                entMan.DeleteEntity(projectile);
                if (shield is { } shieldUid)
                    entMan.DeleteEntity(shieldUid);

                entMan.DeleteEntity(action.Owner);
                entMan.DeleteEntity(warlock);
            }
        });

        await pair.CleanReturnAsync();
    }

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
                var actionEv = new CMUXenoPsychicShieldActionEvent
                {
                    Performer = warlock,
                    Action = actionEnt,
                };
                entMan.EventBus.RaiseLocalEvent(warlock, actionEv);

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
                var actionEv = new CMUXenoPsychicShieldActionEvent
                {
                    Performer = warlock,
                    Action = actionEnt,
                };
                entMan.EventBus.RaiseLocalEvent(warlock, actionEv);

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
                var actionEv = new CMUXenoPsychicShieldActionEvent
                {
                    Performer = warlock,
                    Action = actionEnt,
                };
                entMan.EventBus.RaiseLocalEvent(warlock, actionEv);

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

                var detonateEv = new CMUXenoPsychicShieldActionEvent
                {
                    Performer = warlock,
                    Action = actionEnt,
                };
                entMan.EventBus.RaiseLocalEvent(warlock, detonateEv);

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

    private static bool HasAction(IEntityManager entMan, EntityUid user, string prototype)
    {
        if (!entMan.TryGetComponent<ActionsComponent>(user, out var actions))
            return false;

        foreach (var action in actions.Actions)
        {
            if (entMan.GetComponent<MetaDataComponent>(action).EntityPrototype?.ID == prototype)
                return true;
        }

        return false;
    }
}
