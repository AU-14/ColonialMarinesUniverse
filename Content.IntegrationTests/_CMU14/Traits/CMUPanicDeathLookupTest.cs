using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.Traits.PanicProne;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.IntegrationTests.CMU14.Traits;

[TestFixture]
public sealed class CMUPanicDeathLookupTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUPanicDeathObserver
          components:
          - type: Alerts
          - type: Panic
          - type: Physics
            bodyType: Dynamic
          - type: Fixtures
            fixtures:
              body:
                shape: !type:PhysShapeCircle
                  radius: 0.25
                hard: true
                layer:
                - MobLayer
                mask:
                - MobMask
        """;

    [Test]
    public async Task DifferentRadiiAndObstructionsKeepIndependentWitnessEligibility()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var target = SSpawnAtPosition("CMUPanicDeathObserver", new EntityCoordinates(map.Grid.Owner, new Vector2(0.5f)));
            var state = SEntMan.AddComponent<MobStateComponent>(target);
            var close = Observer(new Vector2(1.5f, 0.5f), 2);
            var shortRange = Observer(new Vector2(5.5f, 0.5f), 2);
            var longRange = Observer(new Vector2(5.5f, 1.5f), 8);
            var sameLongRange = Observer(new Vector2(5.5f, -1.5f), 8);

            RaiseDeath();
            Assert.That(SComp<PanicComponent>(target).Current, Is.Zero, "The dying mob is excluded.");
            Assert.That(SComp<PanicComponent>(close).Current, Is.EqualTo(SComp<PanicComponent>(close).NearbyDeathGain));
            Assert.That(SComp<PanicComponent>(shortRange).Current, Is.Zero);
            Assert.That(SComp<PanicComponent>(longRange).Current, Is.EqualTo(SComp<PanicComponent>(longRange).NearbyDeathGain));
            Assert.That(SComp<PanicComponent>(sameLongRange).Current, Is.EqualTo(SComp<PanicComponent>(sameLongRange).NearbyDeathGain));

            // A fresh death must query current topology rather than reuse the previous event's set.
            SSpawnAtPosition("WallSolid", new EntityCoordinates(map.Grid.Owner, new Vector2(3.5f, 1.5f)));
            var previous = SComp<PanicComponent>(longRange).Current;
            RaiseDeath();
            Assert.That(SComp<PanicComponent>(longRange).Current, Is.EqualTo(previous),
                "A nearby witness behind a wall must still pass its own line-of-sight check.");
            Assert.That(SComp<PanicComponent>(sameLongRange).Current,
                Is.EqualTo(2 * SComp<PanicComponent>(sameLongRange).NearbyDeathGain));

            EntityUid Observer(Vector2 position, float radius)
            {
                var observer = SSpawnAtPosition("CMUPanicDeathObserver", new EntityCoordinates(map.Grid.Owner, position));
                SComp<PanicComponent>(observer).NearbyDeathRadius = radius;
                return observer;
            }

            void RaiseDeath()
            {
                var ev = new MobStateChangedEvent(target, state, MobState.Alive, MobState.Dead);
                SEntMan.EventBus.RaiseLocalEvent(target, ev, true);
            }
        });
    }

    [Test]
    public async Task SameRadiusDeathDoesNotAllocateAResultSetForEveryTraitEntity()
    {
        await Server.WaitAssertion(() =>
        {
            var target = SSpawn(null);
            var state = SEntMan.AddComponent<MobStateComponent>(target);
            for (var i = 0; i < 500; i++)
                SEntMan.AddComponent<PanicComponent>(SSpawn(null));

            var ev = new MobStateChangedEvent(target, state, MobState.Alive, MobState.Dead);
            for (var i = 0; i < 8; i++)
                SEntMan.EventBus.RaiseLocalEvent(target, ev, true);

            // Nullspace excludes all witnesses, isolating the death lookup from panic effects.
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 32; i++)
                SEntMan.EventBus.RaiseLocalEvent(target, ev, true);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.Out.WriteLine($"CMU_PANIC_DEATH population=500 deaths=32 allocated={allocated}");
            Assert.That(allocated, Is.LessThan(128 * 1024),
                "One death must share its spatial result for observers with the same radius.");
        });
    }
}
