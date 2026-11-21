using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Gibbing;
using Content.Shared.Gibbing;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Gibbing;

[TestFixture]
[TestOf(typeof(RMCGibSystem))]
public sealed class RMCGibSuccessorMergeRegressionTest : GameTest
{
    private static readonly EntProtoId SpawnSource = "RMCGibSuccessorSpawnSource";
    private static readonly EntProtoId SpawnResult = "RMCGibSuccessorSpawnResult";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: RMCGibSuccessorSpawnSource
  components:
  - type: RMCSpawnEntitiesOnGib
    entities:
    - RMCGibSuccessorSpawnResult

- type: entity
  id: RMCGibSuccessorSpawnResult
";

    [Test]
    public async Task CertainDeathGibAndBeingGibbedSpawnRemainReachable()
    {
        var server = Pair.Server;
        var map = await Pair.CreateTestMap();
        EntityUid doomed = default;

        await server.WaitPost(() =>
        {
            _ = server.System<RMCGibSystem>();
            var entities = server.EntMan;
            doomed = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            var gib = entities.GetComponent<RMCGibOnDeathComponent>(doomed);
            gib.GibChance = 1;
            gib.DamageGibMultiplier = 0;
            entities.Dirty(doomed, gib);

            var source = entities.SpawnEntity(SpawnSource, map.GridCoords);
            Assert.That(CountPrototype(entities, SpawnResult), Is.Zero);
            var beingGibbed = new BeingGibbedEvent([]);
            entities.EventBus.RaiseLocalEvent(source, ref beingGibbed);
            Assert.That(CountPrototype(entities, SpawnResult), Is.EqualTo(1),
                "the RMC BeingGibbed subscriber must still spawn its configured payload once");

            server.System<MobStateSystem>().ChangeMobState(doomed, MobState.Dead);
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.Deleted(doomed), Is.True,
                "GibChance 1 must keep the RMC death-to-gib path deterministic and reachable");
        });
    }

    private static int CountPrototype(IEntityManager entities, EntProtoId prototype)
    {
        return entities.EntityQuery<MetaDataComponent>()
            .Count(metadata => metadata.EntityPrototype?.ID == prototype);
    }
}
