using System.Numerics;
using Content.Shared._RMC14.Xenonids.Bulwark;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class XenoBulwarkTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: CMXenoWarriorBulwark
  id: RMCTestXenoBulwarkReflecting
  components:
  - type: XenoBulwark
    reflecting: true
";

    [Test]
    public async Task PlateBashDamageIsAlwaysTwenty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("CMXenoWarriorBulwark", map.GridCoords);
            var targetUnencased = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var targetEncased = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(0, 1)));
            var action = SpawnAction(entMan);
            var encaseAction = SpawnAction(entMan);

            try
            {
                RaisePlateBash(entMan, xeno, targetUnencased, action);
                RaiseEncasedPlates(entMan, xeno, encaseAction);
                RaisePlateBash(entMan, xeno, targetEncased, action);

                Assert.Multiple(() =>
                {
                    Assert.That(TotalDamage(entMan, targetUnencased), Is.EqualTo(20));
                    Assert.That(TotalDamage(entMan, targetEncased), Is.EqualTo(20));
                });
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(targetUnencased);
                entMan.DeleteEntity(targetEncased);
                entMan.DeleteEntity(action.Owner);
                entMan.DeleteEntity(encaseAction.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlateBashUnencasedThrowsBulwarkTowardTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("CMXenoWarriorBulwark", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(3, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaisePlateBash(entMan, xeno, target, action);

                Assert.That(entMan.HasComponent<ThrownItemComponent>(xeno), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlateBashEncasedRequiresAdjacentTarget()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("CMXenoWarriorBulwark", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var action = SpawnAction(entMan);
            var encaseAction = SpawnAction(entMan);

            try
            {
                RaiseEncasedPlates(entMan, xeno, encaseAction);
                RaisePlateBash(entMan, xeno, target, action);

                Assert.That(TotalDamage(entMan, target), Is.Zero);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
                entMan.DeleteEntity(encaseAction.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TailSwingParalyzesHumansForAtLeastOneSecond()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var status = entMan.System<StatusEffectQuerySystem>();
            var xeno = entMan.SpawnEntity("CMXenoWarriorBulwark", map.GridCoords);
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaiseTailSwing(entMan, xeno, action);

                Assert.That(status.TryGetTime(target, "Stun", out var time), Is.True);
                Assert.That(time!.Value.Item2 - time.Value.Item1, Is.GreaterThanOrEqualTo(TimeSpan.FromSeconds(1)));
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TailSwingFlingsNearbyGrenadesBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("CMXenoWarriorBulwark", map.GridCoords);
            var grenade = entMan.SpawnEntity("CMGrenadeHighExplosive", map.GridCoords.Offset(new Vector2(1, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaiseTailSwing(entMan, xeno, action);

                Assert.That(entMan.HasComponent<ThrownItemComponent>(grenade), Is.True);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(grenade);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReflectiveShieldRandomizesProjectileIntoReturnHalfCircle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var xeno = entMan.SpawnEntity("RMCTestXenoBulwarkReflecting", map.GridCoords);
            var projectile = entMan.SpawnEntity("BulletPistol", map.GridCoords.Offset(new Vector2(1, 0)));

            try
            {
                var projectileComp = entMan.GetComponent<ProjectileComponent>(projectile);
                var projectilePhysics = entMan.GetComponent<PhysicsComponent>(projectile);
                var incoming = new Vector2(10, 0);
                var reflected = Vector2.Zero;
                var reflectedRotation = Angle.Zero;
                var didReflect = false;

                for (var i = 0; i < 30; i++)
                {
                    physics.SetLinearVelocity(projectile, incoming, body: projectilePhysics);

                    var ev = new ProjectileReflectAttemptEvent(projectile, projectileComp, false);
                    entMan.EventBus.RaiseLocalEvent(xeno, ref ev);
                    if (!ev.Cancelled)
                        continue;

                    reflected = physics.GetMapLinearVelocity(projectile, component: projectilePhysics);
                    reflectedRotation = transform.GetWorldRotation(projectile);
                    didReflect = true;
                    break;
                }

                Assert.That(didReflect, Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(reflected.Length(), Is.EqualTo(incoming.Length()).Within(0.01f));
                    Assert.That(Vector2.Dot(reflected, incoming), Is.LessThan(0));
                    Assert.That(Vector2.Distance(reflected, -incoming), Is.GreaterThan(0.01f));
                    Assert.That(reflectedRotation.Theta, Is.EqualTo(reflected.ToWorldAngle().Theta).Within(0.01f));
                });
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(projectile);
            }
        });

        await pair.CleanReturnAsync();
    }

    private static Entity<ActionComponent> SpawnAction(IEntityManager entMan)
    {
        var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        return (action, entMan.EnsureComponent<ActionComponent>(action));
    }

    private static void RaisePlateBash(
        IEntityManager entMan,
        EntityUid xeno,
        EntityUid target,
        Entity<ActionComponent> action)
    {
        var ev = new XenoPlateBashActionEvent
        {
            Performer = xeno,
            Action = action,
            Target = target,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static void RaiseEncasedPlates(IEntityManager entMan, EntityUid xeno, Entity<ActionComponent> action)
    {
        var ev = new XenoEncasedPlatesActionEvent
        {
            Performer = xeno,
            Action = action,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static void RaiseTailSwing(IEntityManager entMan, EntityUid xeno, Entity<ActionComponent> action)
    {
        var ev = new XenoBulwarkTailSwingActionEvent
        {
            Performer = xeno,
            Action = action,
        };

        entMan.EventBus.RaiseLocalEvent(xeno, ev);
    }

    private static float TotalDamage(IEntityManager entMan, EntityUid target)
    {
        return entMan.GetComponent<DamageableComponent>(target).Damage.GetTotal().Float();
    }
}
