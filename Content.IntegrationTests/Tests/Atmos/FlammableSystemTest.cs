using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Atmos;

[TestOf(typeof(FlammableSystem))]
public sealed class FlammableSystemTest : GameTest
{
    private const string TestEntity = "FlammableUpdateTest";
    private const string TestRMCEntity = "RMCFlammableUpdateTest";

    [TestPrototypes]
    private const string Prototypes = $"""
        - type: entity
          id: {TestEntity}
          components:
          - type: Appearance
          - type: Flammable
            damage:
              types:
                Heat: 0

        - type: entity
          parent: {TestEntity}
          id: {TestRMCEntity}
          components:
          - type: RMCFireColor
        """;

    [SidedDependency(Side.Server)] private readonly FlammableSystem _flammable = null!;
    [SidedDependency(Side.Server)] private readonly MapSystem _map = null!;

    [Test]
    public async Task ActivatingIdleFlammableSchedulesFirstUpdateOneSecondLater()
    {
        var map = await Pair.CreateTestMap();
        EntityUid entity = default;
        FlammableComponent flammable = default!;

        await Server.WaitAssertion(() =>
        {
            entity = SSpawnAtPosition(TestEntity, map.GridCoords);
            flammable = SComp<FlammableComponent>(entity);
        });

        await Pair.RunSeconds(3f);

        await Server.WaitAssertion(() =>
        {
            _flammable.SetFireStacks(entity, 2f, flammable, ignite: true);
            Assert.That(flammable.OnFire, Is.True);
        });

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(flammable.OnFire, Is.True));

        // Fire duration is stack-driven and does not depend on the atmospheric simulation.
        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(flammable.OnFire, Is.True);
            Assert.That(flammable.FireStacks, Is.EqualTo(1.9f));
        });
    }

    [Test]
    public async Task RMCFlammableFadesAtRMCStackRate()
    {
        var map = await Pair.CreateTestMap();
        EntityUid entity = default;
        FlammableComponent flammable = default!;

        await Server.WaitAssertion(() =>
        {
            entity = SSpawnAtPosition(TestRMCEntity, map.GridCoords);
            flammable = SComp<FlammableComponent>(entity);
            _flammable.SetFireStacks(entity, 2f, flammable, ignite: true);
        });

        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(flammable.OnFire, Is.True);
            Assert.That(flammable.FireStacks, Is.EqualTo(1.75f));
        });
    }

    [Test]
    public async Task WetFlammableDriesOncePerSecondAndStopsWhilePaused()
    {
        var map = await Pair.CreateTestMap();
        EntityUid entity = default;
        FlammableComponent flammable = default!;

        await Server.WaitAssertion(() =>
        {
            entity = SSpawnAtPosition(TestEntity, map.GridCoords);
            flammable = SComp<FlammableComponent>(entity);
            _flammable.SetFireStacks(entity, -3f, flammable);
        });

        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() => Assert.That(flammable.FireStacks, Is.EqualTo(-2f)));

        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(flammable.FireStacks, Is.EqualTo(-2f)));

        await Pair.RunSeconds(0.6f);
        await Server.WaitAssertion(() => Assert.That(flammable.FireStacks, Is.EqualTo(-1f)));

        await Server.WaitAssertion(() => _map.SetPaused(map.MapId, true));
        await Pair.RunSeconds(2f);
        await Server.WaitAssertion(() => Assert.That(flammable.FireStacks, Is.EqualTo(-1f)));

        await Server.WaitAssertion(() => _map.SetPaused(map.MapId, false));
        await Pair.RunSeconds(0.5f);
        await Server.WaitAssertion(() => Assert.That(flammable.FireStacks, Is.EqualTo(-1f)));

        await Pair.RunSeconds(1.1f);
        await Server.WaitAssertion(() => Assert.That(flammable.FireStacks, Is.Zero));

        await Server.WaitAssertion(() => SDeleteNow(entity));
        await Pair.RunTicksSync(2);
    }
}
