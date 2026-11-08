#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Shared._RMC14.Actions;
using Content.Shared._RMC14.Aura;
using Content.Shared._RMC14.Xenonids.Bulwark;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Spawners;

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

- type: entity
  parent: CMXenoWarriorBulwark
  id: RMCTestXenoBulwarkLongTailSwing
  components:
  - type: XenoBulwark
    tailSwingRange: 3
";

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
    public async Task TailSwingDoesNotHitThroughWalls()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var xeno = entMan.SpawnEntity("RMCTestXenoBulwarkLongTailSwing", map.GridCoords);
            var wall = entMan.SpawnEntity("CMWallMetal", map.GridCoords.Offset(new Vector2(1, 0)));
            var target = entMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(2, 0)));
            var action = SpawnAction(entMan);

            try
            {
                RaiseTailSwing(entMan, xeno, action);

                Assert.That(TotalDamage(entMan, target), Is.Zero);
            }
            finally
            {
                entMan.DeleteEntity(xeno);
                entMan.DeleteEntity(wall);
                entMan.DeleteEntity(target);
                entMan.DeleteEntity(action.Owner);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ReflectiveShieldEarlyCancellationUsesMinimumCooldown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var actions = entMan.System<SharedActionsSystem>();
            var rmcActions = entMan.System<SharedRMCActionsSystem>();
            var xeno = entMan.SpawnEntity("CMXenoWarriorBulwark", map.GridCoords);

            try
            {
                var encaseAction = rmcActions.GetActionsWithEvent<XenoEncasedPlatesActionEvent>(xeno).Single();
                var reflectAction = rmcActions.GetActionsWithEvent<XenoReflectiveShieldActionEvent>(xeno).Single();

                actions.PerformAction(xeno, encaseAction);
                actions.PerformAction(xeno, reflectAction);
                actions.ClearCooldown(reflectAction.AsNullable());
                actions.PerformAction(xeno, reflectAction);

                var bulwark = entMan.GetComponent<XenoBulwarkComponent>(xeno);
                var cooldown = reflectAction.Comp.Cooldown;

                Assert.Multiple(() =>
                {
                    Assert.That(bulwark.Reflecting, Is.False);
                    Assert.That(cooldown, Is.Not.Null);
                    Assert.That(cooldown!.Value.End - cooldown.Value.Start, Is.EqualTo(bulwark.ReflectMinCooldown));
                });
            }
            finally
            {
                entMan.DeleteEntity(xeno);
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
                    Assert.That(Vector2.Distance(reflected, -incoming), Is.GreaterThan(0.0001f));
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

#pragma warning restore RA0002
