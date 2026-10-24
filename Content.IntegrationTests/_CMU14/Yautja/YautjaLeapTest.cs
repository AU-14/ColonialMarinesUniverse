using Content.Server.CMU14.Yautja;
using Content.Shared.CMU14.Yautja;
using Content.Shared._RMC14.Damage.ObstacleSlamming;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.DoAfter;
using Content.Shared.Physics;
using Content.Shared.Throwing;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaLeapTest
{
    private static List<EntityUid> Warnings(IEntityManager entMan)
    {
        var list = new List<EntityUid>();
        var query = entMan.EntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out var meta))
        {
            if (meta.EntityPrototype?.ID == "CMUYautjaLeapWarning")
                list.Add(uid);
        }

        return list;
    }

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

    [Test]
    public async Task YautjaLeapShowsWindupBeforeAnimatedThrow()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid hunter = default;
        EntityUid action = default;

        try
        {
            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
                action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

                var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
                yautja.LeapThrowSpeed = 5f;
                yautja.LeapMaxRange = 7f;
                yautja.LeapWindup = TimeSpan.FromSeconds(0.2);

                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaLeapActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = map.GridCoords.Offset(new Vector2(6, 0)),
                };

                entMan.EventBus.RaiseLocalEvent(hunter, ev);

                Assert.That(ev.Handled, Is.True);
                Assert.That(entMan.HasComponent<ThrownItemComponent>(hunter), Is.False,
                    "The leap must not move the hunter before the wind-up finishes.");
                var doAfter = entMan.GetComponent<DoAfterComponent>(hunter);
                Assert.That(doAfter.DoAfters.Values.Any(active => active.Args.Event is YautjaLeapDoAfterEvent), Is.True,
                    "The wind-up do-after is running.");
            });

            await pair.RunTicksSync(pair.SecondsToTicks(0.4f));

            await server.WaitAssertion(() =>
            {
                var entMan = server.EntMan;
                var thrown = entMan.GetComponent<ThrownItemComponent>(hunter);
                Assert.That(thrown.LandTime!.Value - thrown.ThrownTime!.Value, Is.GreaterThan(TimeSpan.FromSeconds(0.5)),
                    "The animated throw lasts long enough to read as a leap.");
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                var entMan = server.EntMan;
                if (hunter != default && !entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (action != default && !entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            });

            await pair.CleanReturnAsync();
        }
    }

    [Test]
    public async Task YautjaLeapSpawnsVisibleClampedLandingWarning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hunter = entMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var action = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            try
            {
                var yautja = entMan.EnsureComponent<YautjaComponent>(hunter);
                yautja.LeapMaxRange = 7f;
                yautja.LeapWindup = TimeSpan.FromSeconds(1);
                Assert.That(yautja.LeapThrowSpeed, Is.LessThanOrEqualTo(12f));

                var actionComp = entMan.EnsureComponent<ActionComponent>(action);
                var ev = new YautjaLeapActionEvent
                {
                    Performer = hunter,
                    Action = (action, actionComp),
                    Target = map.GridCoords.Offset(new Vector2(20, 0)),
                };

                entMan.EventBus.RaiseLocalEvent(hunter, ev);

                var warnings = Warnings(entMan);
                Assert.That(warnings.Count, Is.EqualTo(1), "Exactly one landing warning is spawned.");

                var warningCoords = entMan.GetComponent<TransformComponent>(warnings[0]).Coordinates;
                Assert.That(warningCoords.Position.X, Is.EqualTo(map.GridCoords.Position.X + 7).Within(0.01f),
                    "A 20-tile request is clamped to the 7-tile maximum range.");
                Assert.That(warningCoords.Position.Y, Is.EqualTo(map.GridCoords.Position.Y).Within(0.01f));
            }
            finally
            {
                if (!entMan.Deleted(hunter))
                    entMan.DeleteEntity(hunter);
                if (!entMan.Deleted(action))
                    entMan.DeleteEntity(action);
            }
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LeapActionPrototypeHasCmss13CooldownRangeAndEvent()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var prototype = prototypes.Index<EntityPrototype>("CMUActionYautjaLeap");

            Assert.That(prototype.TryGetComponent<ActionComponent>(out var action, server.EntMan.ComponentFactory), Is.True);
            Assert.That(prototype.TryGetComponent<TargetActionComponent>(out var target, server.EntMan.ComponentFactory), Is.True);
            Assert.That(prototype.TryGetComponent<WorldTargetActionComponent>(out var world, server.EntMan.ComponentFactory), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(action!.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(15)), "15 second cooldown.");
                Assert.That(target!.Range, Is.EqualTo(7f), "7 tile target range.");
                Assert.That(target.CheckCanAccess, Is.False);
                Assert.That(world!.Event, Is.TypeOf<YautjaLeapActionEvent>(), "World-targeted leap event.");
            });

            // The action id the component grants on the role lifecycle.
            Assert.That(new YautjaComponent().LeapActionId.Id, Is.EqualTo("CMUActionYautjaLeap"));
        });

        await pair.CleanReturnAsync();
    }

    // A cross-level leap never reaches ThrowingSystem, so StopThrowEvent never fires; the leap must end by removing
    // the component, which restores the collision masks recorded before the leap. Without that the hunter keeps the
    // leap-permissive masks and can walk through impassable terrain.
    [Test]
    public async Task LeapCollisionMasksAreRestoredWhenTheLeapEnds()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var physics = entMan.System<SharedPhysicsSystem>();
            var yautja = entMan.SpawnEntity("CMUMobYautja", map.GridCoords);

            Assert.That(entMan.TryGetComponent<FixturesComponent>(yautja, out var fixtures), Is.True);
            var originals = new Dictionary<string, int>();
            foreach (var (fixtureId, fixture) in fixtures!.Fixtures)
                originals[fixtureId] = fixture.CollisionMask;
            Assert.That(originals, Is.Not.Empty, "the hunter has fixtures to relax");

            // Same steps PrepareLeapCollision performs: save the masks, then relax them for the leap.
            var leap = entMan.EnsureComponent<YautjaLeapingComponent>(yautja);
            leap.OriginalCollisionMasks.Clear();
            foreach (var (fixtureId, fixture) in fixtures.Fixtures)
            {
                leap.OriginalCollisionMasks[fixtureId] = fixture.CollisionMask;
                physics.SetCollisionMask(
                    yautja,
                    fixtureId,
                    fixture,
                    YautjaAbilitySystem.GetLeapCollisionMask(fixture.CollisionMask),
                    fixtures);
            }

            Assert.That(fixtures.Fixtures.Values.Any(f => f.CollisionMask != originals[
                    fixtures.Fixtures.First(e => e.Value == f).Key]),
                Is.True, "the leap masks differ from the originals");

            // RemoveComponent runs the same handler RemCompDeferred would.
            entMan.RemoveComponent<YautjaLeapingComponent>(yautja);

            foreach (var (fixtureId, fixture) in fixtures.Fixtures)
            {
                Assert.That(fixture.CollisionMask, Is.EqualTo(originals[fixtureId]),
                    $"fixture {fixtureId} kept its leap collision mask after the leap ended");
            }
        });

        await pair.CleanReturnAsync();
    }
}
