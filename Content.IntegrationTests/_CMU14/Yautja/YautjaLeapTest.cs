using Content.Server._CMU14.Yautja;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaLeapTest
{
    [Test]
    public void LeapPassesMobsAndObjectsButKeepsWallsBlocking()
    {
        var original = (int) (CollisionGroup.Impassable |
                              CollisionGroup.MidImpassable |
                              CollisionGroup.HighImpassable |
                              CollisionGroup.LowImpassable |
                              CollisionGroup.BulletImpassable);

        var actual = YautjaAbilitySystem.GetLeapCollisionMask(original);

        Assert.Multiple(() =>
        {
            Assert.That(actual & (int) CollisionGroup.Impassable, Is.Not.Zero,
                "Walls must remain impassable during a Yautja leap.");
            Assert.That(actual & (int) CollisionGroup.MidImpassable, Is.Zero);
            Assert.That(actual & (int) CollisionGroup.HighImpassable, Is.Zero);
            Assert.That(actual & (int) CollisionGroup.LowImpassable, Is.Zero);
            Assert.That(actual & (int) CollisionGroup.BulletImpassable, Is.Not.Zero,
                "Unrelated collision groups must not be removed by the leap.");
        });
    }

    [Test]
    public async Task IntentionalLeapGetsObstacleSlamImmunity()
    {
        var (server, _) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);
        EntityCoordinates origin = default;
        await server.WaitPost(() =>
        {
            var mapSystem = server.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            origin = new EntityCoordinates(mapSystem.CreateGridEntity(mapId), 0, 0);
        });
        EntityUid hunter = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMUMobYautja", origin);
                var yautja = entMan.GetComponent<YautjaComponent>(hunter);
                yautja.LeapWindup = TimeSpan.Zero;
                var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var leap = new YautjaLeapActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = origin.Offset(new Vector2(3, 0)),
                };
                entMan.EventBus.RaiseLocalEvent(hunter, leap);
                Assert.That(leap.Handled, Is.True);
            });

            await server.WaitRunTicks(2);

            await server.WaitAssertion(() =>
            {
                var immune = server.EntMan.GetComponent<RMCObstacleSlamImmuneComponent>(hunter);
                Assert.That(immune.ExpireIn, Is.EqualTo(TimeSpan.FromSeconds(0.5)));
                Assert.That(immune.ExpireAt, Is.GreaterThan(server.Timing.CurTime));
            });
        }
        finally
        {
            server.Dispose();
        }
    }
}
