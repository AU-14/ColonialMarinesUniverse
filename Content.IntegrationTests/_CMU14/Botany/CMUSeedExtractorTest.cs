#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using System.Linq;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Botany;

[TestFixture]
public sealed class CMUSeedExtractorTest
{
    [TestPrototypes]
    private const string TestPrototypes = """
        - type: entity
          parent: SeedExtractor
          id: CMUTestPoweredSeedExtractor
          components:
          - type: ApcPowerReceiver
            needsPower: false

        - type: entity
          parent: BasePlant
          id: CMUTestSeedlessPlants
          components:
          - type: PlantData
            name: seeds-carrots-name
            packetPrototype: CMUTestSeedPacket
            productPrototypes: [ CMUTestSeedlessProduce ]
          - type: PlantTraitSeedless

        - type: entity
          parent: BaseItem
          id: CMUTestSeedPacket

        - type: entity
          parent: BaseItem
          id: CMUTestSeedlessProduce
          components:
          - type: Produce
            plantId: CMUTestSeedlessPlants

        - type: entity
          parent: BaseItem
          id: CMUTestNonProduce
        """;

    [Test]
    public async Task PoweredExtractorConvertsEachSeedBearingProduceExactlyOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var expectedPotencies = new Dictionary<string, float>
        {
            ["CarrotPlants"] = 37,
            ["CabbagePlants"] = 73,
        };

        EntityUid extractor = default;
        EntityUid bag = default;
        EntityUid user = default;
        EntityUid carrot = default;
        EntityUid cabbage = default;
        EntityUid seedless = default;
        EntityUid nonProduce = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var containers = entities.System<SharedContainerSystem>();
            var botany = entities.System<BotanySystem>();
            extractor = entities.SpawnEntity("CMUTestPoweredSeedExtractor", MapCoordinates.Nullspace);
            bag = entities.SpawnEntity("RMCStoragePlantBag", MapCoordinates.Nullspace);
            user = entities.SpawnEntity("MobObserver", MapCoordinates.Nullspace);
            carrot = entities.SpawnEntity("FoodCarrot", MapCoordinates.Nullspace);
            cabbage = entities.SpawnEntity("FoodCabbage", MapCoordinates.Nullspace);
            seedless = entities.SpawnEntity("CMUTestSeedlessProduce", MapCoordinates.Nullspace);
            nonProduce = entities.SpawnEntity("CMUTestNonProduce", MapCoordinates.Nullspace);
            var storage = entities.GetComponent<StorageComponent>(bag);

            AttachSnapshot(entities, botany, carrot, "CarrotPlants", expectedPotencies["CarrotPlants"]);
            AttachSnapshot(entities, botany, cabbage, "CabbagePlants", expectedPotencies["CabbagePlants"]);

            Assert.That(containers.Insert(carrot, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(cabbage, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(seedless, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(nonProduce, storage.Container, force: true), Is.True);
        });

        // PowerNetSystem updates the receiver's effective Powered state on its next update.
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var verb = GetConversionVerb(entities, extractor, bag, user);
            Assert.That(verb, Is.Not.Null, "A powered seed extractor did not expose plant-bag conversion.");
            verb!.Act!();
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var botany = entities.System<BotanySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entities.EntityExists(carrot), Is.False);
                Assert.That(entities.EntityExists(cabbage), Is.False);
                Assert.That(entities.EntityExists(seedless), Is.True);
                Assert.That(entities.EntityExists(nonProduce), Is.True);
            });

            var packets = GetSeedPackets(entities);
            Assert.That(packets.Keys, Is.EquivalentTo(expectedPotencies.Keys));
            foreach (var (plantId, expectedPotency) in expectedPotencies)
            {
                Assert.That(packets[plantId].Count, Is.InRange(1, 3),
                    $"{plantId} did not use the extractor's per-produce yield bounds.");
                foreach (var seed in packets[plantId])
                {
                    Assert.That(seed.PlantData, Is.Not.Null, $"{plantId} lost its produce snapshot.");
                    Assert.That(botany.TryGetPlantComponent<PlantComponent>(seed.PlantData, seed.PlantProtoId, out var plant),
                        Is.True);
                    Assert.That(plant!.Potency, Is.EqualTo(expectedPotency), $"{plantId} packet snapshot changed.");
                }
            }

            var storage = entities.GetComponent<StorageComponent>(bag);
            Assert.That(storage.Container.ContainedEntities, Is.EquivalentTo(new[] { seedless, nonProduce }));
        });

        Dictionary<string, List<SeedComponent>> firstPackets = null!;
        await server.WaitAssertion(() =>
        {
            firstPackets = GetSeedPackets(server.EntMan);
            var verb = GetConversionVerb(server.EntMan, extractor, bag, user);
            Assert.That(verb, Is.Not.Null, "The no-seeds path should still be available for a valid plant bag.");
            verb!.Act!();
        });

        await server.WaitRunTicks(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var afterSecondConversion = GetSeedPackets(entities);
            Assert.That(afterSecondConversion.ToDictionary(pair => pair.Key, pair => pair.Value.Count),
                Is.EqualTo(firstPackets.ToDictionary(pair => pair.Key, pair => pair.Value.Count)),
                "A second conversion duplicated packets from already-consumed produce.");
            Assert.That(entities.EntityExists(seedless), Is.True, "The no-seeds path consumed seedless produce.");
            Assert.That(entities.EntityExists(nonProduce), Is.True, "The no-seeds path consumed an unrelated item.");
        });

        await pair.CleanReturnAsync();
    }

    private static void AttachSnapshot(
        IEntityManager entities,
        BotanySystem botany,
        EntityUid produce,
        string plantPrototype,
        float potency)
    {
        var plant = entities.SpawnEntity(plantPrototype, MapCoordinates.Nullspace);
        entities.GetComponent<PlantComponent>(plant).Potency = potency;
        var produceComponent = entities.GetComponent<ProduceComponent>(produce);
        produceComponent.PlantData = botany.ClonePlantSnapshotData(plant, parent: produce);
        Assert.That(produceComponent.PlantData, Is.Not.Null);
        entities.DeleteEntity(plant);
    }

    private static AlternativeVerb? GetConversionVerb(
        IEntityManager entities,
        EntityUid extractor,
        EntityUid bag,
        EntityUid user)
    {
        var verbs = new GetVerbsEvent<AlternativeVerb>(
            user,
            extractor,
            bag,
            hands: null,
            canInteract: true,
            canComplexInteract: true,
            canAccess: true,
            extraCategories: new List<VerbCategory>());
        entities.EventBus.RaiseLocalEvent(extractor, verbs);
        return verbs.Verbs.SingleOrDefault(verb => verb.Text == "Convert plant bag into seeds");
    }

    private static Dictionary<string, List<SeedComponent>> GetSeedPackets(IEntityManager entities)
    {
        var packets = new Dictionary<string, List<SeedComponent>>();
        var query = entities.EntityQueryEnumerator<SeedComponent>();
        while (query.MoveNext(out _, out var seed))
        {
            var plantId = seed.PlantProtoId.Id;
            if (!packets.TryGetValue(plantId, out var seeds))
                packets[plantId] = seeds = new List<SeedComponent>();
            seeds.Add(seed);
        }

        return packets;
    }
}

#pragma warning restore RA0002
