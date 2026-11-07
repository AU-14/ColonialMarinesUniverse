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
