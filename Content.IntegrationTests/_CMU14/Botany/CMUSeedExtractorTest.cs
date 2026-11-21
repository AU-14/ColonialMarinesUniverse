#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Automation;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.CMU14.Automation;
using Content.Shared.CMU14.Botany;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Botany;

/// <summary>
/// CMU14 batch seed extractor (N7): the plant-bag verb, the batch tick, seedless refusal and pausing on power loss.
/// </summary>
[TestFixture]
public sealed class CMUSeedExtractorTest : GameTest
{
    // Nullspace entities created here must be flushed before another test borrows this pair.
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

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
        var initialPackets = new HashSet<EntityUid>();
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

        await Server.WaitAssertion(() =>
        {
            var entities = SEntMan;
            CollectPackets(entities, initialPackets);

            var containers = entities.System<SharedContainerSystem>();
            var botany = entities.System<BotanySystem>();
            extractor = SSpawn("CMUTestPoweredSeedExtractor");
            bag = SSpawn("RMCStoragePlantBag");
            user = SSpawn("MobObserver");
            carrot = SSpawn("FoodCarrot");
            cabbage = SSpawn("FoodCabbage");
            seedless = SSpawn("CMUTestSeedlessProduce");
            nonProduce = SSpawn("CMUTestNonProduce");
            var storage = entities.GetComponent<StorageComponent>(bag);

            AttachSnapshot(entities, botany, carrot, "CarrotPlants", expectedPotencies["CarrotPlants"]);
            AttachSnapshot(entities, botany, cabbage, "CabbagePlants", expectedPotencies["CabbagePlants"]);

            Assert.That(containers.Insert(carrot, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(cabbage, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(seedless, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(nonProduce, storage.Container, force: true), Is.True);
        });

        // PowerNetSystem updates the receiver's effective Powered state on its next update.
        await RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            var verb = GetConversionVerb(SEntMan, extractor, bag, user);
            Assert.That(verb, Is.Not.Null, "A powered seed extractor did not expose plant-bag conversion.");
            verb!.Act!();
        });

        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var entities = SEntMan;
            var botany = entities.System<BotanySystem>();
            Assert.Multiple(() =>
            {
                Assert.That(entities.EntityExists(carrot), Is.False);
                Assert.That(entities.EntityExists(cabbage), Is.False);
                Assert.That(entities.EntityExists(seedless), Is.True);
                Assert.That(entities.EntityExists(nonProduce), Is.True);
            });

            var packets = GetSeedPackets(entities, initialPackets);
            Assert.That(packets.Keys, Is.EquivalentTo(expectedPotencies.Keys));
            foreach (var (plantId, expectedPotency) in expectedPotencies)
            {
                Assert.That(packets[plantId].Count, Is.InRange(1, 3),
                    $"{plantId} did not use the extractor's per-produce yield bounds.");
                foreach (var seed in packets[plantId])
                    AssertPotency(botany, seed, expectedPotency, plantId);
            }

            var storage = entities.GetComponent<StorageComponent>(bag);
            Assert.That(storage.Container.ContainedEntities, Is.EquivalentTo(new[] { seedless, nonProduce }));
        });

        Dictionary<string, List<SeedComponent>> firstPackets = null!;
        await Server.WaitAssertion(() =>
        {
            firstPackets = GetSeedPackets(SEntMan, initialPackets);
            var verb = GetConversionVerb(SEntMan, extractor, bag, user);
            Assert.That(verb, Is.Not.Null, "The no-seeds path should still be available for a valid plant bag.");
            verb!.Act!();
        });

        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var entities = SEntMan;
            var afterSecondConversion = GetSeedPackets(entities, initialPackets);
            Assert.That(afterSecondConversion.ToDictionary(pair => pair.Key, pair => pair.Value.Count),
                Is.EqualTo(firstPackets.ToDictionary(pair => pair.Key, pair => pair.Value.Count)),
                "A second conversion duplicated packets from already-consumed produce.");
            Assert.That(entities.EntityExists(seedless), Is.True, "The no-seeds path consumed seedless produce.");
            Assert.That(entities.EntityExists(nonProduce), Is.True, "The no-seeds path consumed an unrelated item.");
        });
    }

    // (d) The bag verb reads its bounds from the batch component, through the same CMUSeedYield helper.
    [Test]
    public async Task PlantBagVerbUsesTheBatchExtractorBounds()
    {
        var initialPackets = new HashSet<EntityUid>();
        EntityUid extractor = default;
        EntityUid bag = default;
        EntityUid user = default;

        await Server.WaitAssertion(() =>
        {
            var entities = SEntMan;
            CollectPackets(entities, initialPackets);
            var containers = entities.System<SharedContainerSystem>();
            extractor = SSpawn("CMUTestPoweredSeedExtractor");
            bag = SSpawn("RMCStoragePlantBag");
            user = SSpawn("MobObserver");
            entities.GetComponent<CMUBatchSeedExtractorComponent>(extractor).BaseSeeds = new(2, 2);

            var storage = entities.GetComponent<StorageComponent>(bag);
            for (var i = 0; i < 3; i++)
                Assert.That(containers.Insert(SSpawn("FoodCarrot"), storage.Container, force: true), Is.True);
        });

        await RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            var verb = GetConversionVerb(SEntMan, extractor, bag, user);
            Assert.That(verb, Is.Not.Null);
            verb!.Act!();
        });

        await RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var packets = GetSeedPackets(SEntMan, initialPackets);
            Assert.That(packets.Keys, Is.EquivalentTo(new[] { "CarrotPlants" }));
            Assert.That(packets["CarrotPlants"].Count, Is.EqualTo(6),
                "Three produce at a fixed 2-2 yield must give exactly six packets.");
        });
    }

    // (a) Three produce become packets in the output slots within [3*min, 3*max], keep their snapshots and are consumed.
    [Test]
    public async Task BatchTurnsLoadedProduceIntoSlottedSeedPackets()
    {
        var map = await Pair.CreateTestMap();
        EntityUid extractor = default;
        var produce = new List<EntityUid>();
        const float potency = 41;

        await Server.WaitAssertion(() =>
        {
            extractor = SSpawnAtPosition("SeedExtractor", map.GridCoords);
            var slots = SEntMan.System<ItemSlotsSystem>();
            var botany = SEntMan.System<BotanySystem>();
            for (var i = 0; i < 3; i++)
            {
                var carrot = SSpawnAtPosition("FoodCarrot", map.GridCoords);
                AttachSnapshot(SEntMan, botany, carrot, "CarrotPlants", potency);
                Assert.That(slots.TryInsert(extractor, $"produce{i + 1}", carrot, null), Is.True);
                produce.Add(carrot);
            }
        });

        CMUBatchSeedExtractorComponent comp = null!;
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
            Assert.That(comp.Active, Is.True, "A powered extractor with produce and room did not start a batch.");
            Assert.That(GetOutputPackets(extractor), Is.Empty, "Packets appeared before the work time elapsed.");
        });

        await RunSeconds((float) comp.WorkTime.TotalSeconds + 1f);
        await Server.WaitAssertion(() =>
        {
            var botany = SEntMan.System<BotanySystem>();
            var packets = GetOutputPackets(extractor);
            var min = (int) comp.BaseSeeds.Min;
            var max = (int) comp.BaseSeeds.Max;
            Assert.Multiple(() =>
            {
                Assert.That(packets.Count, Is.InRange(3 * min, 3 * max));
                Assert.That(produce.All(p => !SEntMan.EntityExists(p)), Is.True, "Extracted produce was not consumed.");
                Assert.That(comp.Active, Is.False);
            });

            foreach (var packet in packets)
            {
                var seed = SComp<SeedComponent>(packet);
                Assert.That(seed.PlantProtoId.Id, Is.EqualTo("CarrotPlants"));
                AssertPotency(botany, seed, potency, "CarrotPlants");
            }
        });
    }

    // FLOW-03 / ECS-06: a plant bag dumped into the extractor fills its 8 input slots with extractable produce; seedless
    // produce, non-produce and the produce that does not fit stay in the bag, and a full extractor offers no dump.
    [Test]
    public async Task PlantBagDumpsProduceIntoTheEightInputSlots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            var extractor = SSpawnAtPosition("SeedExtractor", map.GridCoords);
            var user = SSpawnAtPosition("MobObserver", map.GridCoords);
            var bag = SSpawnAtPosition("RMCStoragePlantBag", map.GridCoords);
            var storage = SEntMan.GetComponent<StorageComponent>(bag);
            var comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
            Assert.That(comp.InputSlotCount, Is.EqualTo(8));
            Assert.That(comp.OutputSlotCount, Is.EqualTo(16));

            var carrots = new List<EntityUid>();
            for (var i = 0; i < 9; i++)
            {
                var carrot = SSpawnAtPosition("FoodCarrot", map.GridCoords);
                carrots.Add(carrot);
                Assert.That(containers.Insert(carrot, storage.Container, force: true), Is.True);
            }
            var seedless = SSpawnAtPosition("CMUTestSeedlessProduce", map.GridCoords);
            var nonProduce = SSpawnAtPosition("CMUTestNonProduce", map.GridCoords);
            Assert.That(containers.Insert(seedless, storage.Container, force: true), Is.True);
            Assert.That(containers.Insert(nonProduce, storage.Container, force: true), Is.True);

            var verb = new Content.Shared.Storage.Components.GetDumpableVerbEvent(user, null);
            SEntMan.EventBus.RaiseLocalEvent(extractor, ref verb);
            Assert.That(verb.Verb, Is.Not.Null, "An empty extractor offers no dump.");

            // Seedless and non-produce first, so they are turned away before any carrot.
            var queue = new Queue<EntityUid>(new[] { seedless, nonProduce }.Concat(carrots));
            var dump = new Content.Shared.Storage.Components.DumpEvent(queue, user, false, false);
            SEntMan.EventBus.RaiseLocalEvent(extractor, ref dump);

            var loaded = Enumerable.Range(1, 8).Select(i => slots.GetItemOrNull(extractor, $"produce{i}")).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(dump.Handled, Is.True);
                Assert.That(loaded, Is.EquivalentTo(carrots.Take(8)), "The first 8 carrots did not fill the 8 input slots.");
                Assert.That(storage.Container.ContainedEntities, Is.EquivalentTo(new[] { seedless, nonProduce, carrots[8] }),
                    "What the extractor cannot take must stay in the bag.");
            });

            var full = new Content.Shared.Storage.Components.GetDumpableVerbEvent(user, null);
            SEntMan.EventBus.RaiseLocalEvent(extractor, ref full);
            Assert.That(full.Verb, Is.Null, "A full extractor still offers a dump.");
        });
    }

    // (b) Seedless produce is refused by hand (with no slot taken) and by a direct slot insert (inserters use that path).
    [Test]
    public async Task SeedlessProduceIsRefused()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var extractor = SSpawnAtPosition("SeedExtractor", map.GridCoords);
            var user = SSpawnAtPosition("MobObserver", map.GridCoords);
            var seedless = SSpawnAtPosition("CMUTestSeedlessProduce", map.GridCoords);
            var carrot = SSpawnAtPosition("FoodCarrot", map.GridCoords);
            var slots = SEntMan.System<ItemSlotsSystem>();

            var refused = new InteractUsingEvent(user, seedless, extractor, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(extractor, refused);
            Assert.That(refused.Handled, Is.True, "The extractor should claim produce clicks.");
            Assert.That(slots.TryInsert(extractor, "produce1", seedless, null), Is.False,
                "Seedless produce must not enter an input slot.");

            // Hand use: a seed-bearing produce clicked on the machine goes into the first free input slot.
            var accepted = new InteractUsingEvent(user, carrot, extractor, map.GridCoords);
            SEntMan.EventBus.RaiseLocalEvent(extractor, accepted);

            Assert.Multiple(() =>
            {
                Assert.That(accepted.Handled, Is.True);
                Assert.That(slots.GetItemOrNull(extractor, "produce1"), Is.EqualTo(carrot));
                Assert.That(SEntMan.EntityExists(seedless), Is.True);
                Assert.That(Enumerable.Range(1, 6).All(i => slots.GetItemOrNull(extractor, $"produce{i}") != seedless), Is.True);
            });
        });
    }

    // (c) Losing power mid-batch pauses it: no packets while unpowered, and exactly one batch's worth after power returns.
    [Test]
    public async Task PowerLossPausesTheBatchWithoutDuplication()
    {
        var map = await Pair.CreateTestMap();
        EntityUid extractor = default;
        EntityUid carrot = default;
        var workSeconds = 0f;

        await Server.WaitAssertion(() =>
        {
            extractor = SSpawnAtPosition("SeedExtractor", map.GridCoords);
            carrot = SSpawnAtPosition("FoodCarrot", map.GridCoords);
            workSeconds = (float) SComp<CMUBatchSeedExtractorComponent>(extractor).WorkTime.TotalSeconds;
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(extractor, "produce1", carrot, null), Is.True);
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUBatchSeedExtractorComponent>(extractor).EndTime, Is.Not.Null, "The batch did not start.");
            SEntMan.System<PowerReceiverSystem>().SetPowerDisabled(extractor, true);
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
            Assert.Multiple(() =>
            {
                Assert.That(comp.EndTime, Is.Null, "An unpowered batch kept running.");
                Assert.That(comp.Remaining, Is.Not.Null, "The paused batch lost its remaining time.");
                Assert.That(comp.Active, Is.True);
            });
        });

        await RunSeconds(workSeconds + 2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(GetOutputPackets(extractor), Is.Empty, "Packets were made without power.");
            Assert.That(SEntMan.EntityExists(carrot), Is.True, "Produce was consumed without power.");
            SEntMan.System<PowerReceiverSystem>().SetPowerDisabled(extractor, false);
        });

        await RunSeconds(workSeconds + 1f);
        var firstCount = 0;
        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
            firstCount = GetOutputPackets(extractor).Count;
            Assert.Multiple(() =>
            {
                Assert.That(firstCount, Is.InRange((int) comp.BaseSeeds.Min, (int) comp.BaseSeeds.Max));
                Assert.That(SEntMan.EntityExists(carrot), Is.False, "The resumed batch did not consume the produce.");
                Assert.That(comp.Active, Is.False);
            });
        });

        await RunSeconds(workSeconds + 1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(GetOutputPackets(extractor).Count, Is.EqualTo(firstCount), "The resumed batch ran twice.");
        });
    }

    // Full outputs block the batch: with less free room than one produce's maximum yield, nothing starts and nothing is used up.
    [Test]
    public async Task FullOutputsBlockTheBatch()
    {
        var map = await Pair.CreateTestMap();
        EntityUid extractor = default;
        EntityUid carrot = default;
        var filler = new List<EntityUid>();
        var workSeconds = 0f;

        await Server.WaitAssertion(() =>
        {
            extractor = SSpawnAtPosition("SeedExtractor", map.GridCoords);
            var comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
            workSeconds = (float) comp.WorkTime.TotalSeconds;
            var slots = SEntMan.System<ItemSlotsSystem>();

            // Leave one fewer free output slot than the most packets one produce can give.
            var keepFree = CMUSeedYield.MaxPacketCount(comp.BaseSeeds) - 1;
            for (var i = 0; i < comp.OutputSlotCount - keepFree; i++)
            {
                var junk = SSpawnAtPosition("CMUTestNonProduce", map.GridCoords);
                Assert.That(slots.TryInsert(extractor, comp.OutputSlotId(i), junk, null), Is.True);
                filler.Add(junk);
            }

            carrot = SSpawnAtPosition("FoodCarrot", map.GridCoords);
            Assert.That(slots.TryInsert(extractor, "produce1", carrot, null), Is.True);
        });

        await RunSeconds(workSeconds + 1f);
        await Server.WaitAssertion(() =>
        {
            var comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(comp.Active, Is.False, "A batch started without room for its packets.");
                Assert.That(comp.EndTime, Is.Null);
                Assert.That(SEntMan.EntityExists(carrot), Is.True, "Produce was consumed while the outputs were full.");
                Assert.That(slots.GetItemOrNull(extractor, "produce1"), Is.EqualTo(carrot));
                Assert.That(GetOutputPackets(extractor), Is.Empty);
                Assert.That(filler.All(junk => SEntMan.EntityExists(junk)), Is.True);
            });
        });
    }

    // Inserter round trip: an arm loads a loose produce into a produce slot, and after extraction a second arm takes a seed
    // packet out of an output slot onto a belt.
    [Test]
    public async Task InsertersLoadProduceAndUnloadSeedPackets()
    {
        var map = await Pair.CreateTestMap();
        EntityUid extractor = default;
        EntityUid loader = default;
        EntityUid unloader = default;
        EntityUid belt = default;
        EntityUid carrot = default;
        var workSeconds = 0f;

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            for (var x = 0; x <= 4; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            carrot = SSpawnAtPosition("FoodCarrot", map.GridCoords);
            loader = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(1, 0)));
            extractor = SSpawnAtPosition("SeedExtractor", map.GridCoords.Offset(new Vector2(2, 0)));
            unloader = SSpawnAtPosition("CMUInserterArm", map.GridCoords.Offset(new Vector2(3, 0)));
            belt = SSpawnAtPosition("RUMCConveyorBeltTile", map.GridCoords.Offset(new Vector2(4, 0)));
            workSeconds = (float) SComp<CMUBatchSeedExtractorComponent>(extractor).WorkTime.TotalSeconds;

            var transform = Server.System<SharedTransformSystem>();
            Assert.That(transform.AnchorEntity(extractor), Is.True, "Inserters only work with anchored window machines.");
            transform.SetLocalRotation(loader, Angle.FromDegrees(90));
            transform.SetLocalRotation(unloader, Angle.FromDegrees(90));
            SComp<CMUInserterComponent>(unloader).Enabled = false;

            var arms = Server.System<CMUInserterSystem>();
            arms.Update(1);
            Assert.That(SComp<CMUInserterComponent>(loader).Claw.ContainedEntity, Is.EqualTo(carrot));
            arms.Update(1);
            Assert.That(Server.System<ItemSlotsSystem>().GetItemOrNull(extractor, "produce1"), Is.EqualTo(carrot),
                "The loader did not put the produce into the extractor's produce slot.");

            // Keep both arms still while the batch runs, so only the extractor acts.
            SComp<CMUInserterComponent>(loader).Enabled = false;
        });

        await RunSeconds(workSeconds + 1f);
        await Server.WaitAssertion(() =>
        {
            var packets = GetOutputPackets(extractor);
            Assert.That(packets, Is.Not.Empty, "The extractor made no packets from the inserted produce.");
            Assert.That(SEntMan.EntityExists(carrot), Is.False);

            SComp<CMUInserterComponent>(unloader).Enabled = true;
            var arms = Server.System<CMUInserterSystem>();
            arms.Update(1);
            var held = SComp<CMUInserterComponent>(unloader).Claw.ContainedEntity;
            Assert.That(held, Is.Not.Null, "The unloader did not take a packet out of an output slot.");
            Assert.That(packets, Does.Contain(held!.Value));
            arms.Update(1);
            Assert.That(SComp<TransformComponent>(held.Value).Coordinates, Is.EqualTo(SComp<TransformComponent>(belt).Coordinates),
                "The unloaded seed packet did not reach the belt.");
            Assert.That(GetOutputPackets(extractor).Count, Is.EqualTo(packets.Count - 1));
        });
    }

    private List<EntityUid> GetOutputPackets(EntityUid extractor)
    {
        var slots = SEntMan.System<ItemSlotsSystem>();
        var comp = SComp<CMUBatchSeedExtractorComponent>(extractor);
        var packets = new List<EntityUid>();
        for (var i = 0; i < comp.OutputSlotCount; i++)
        {
            if (slots.GetItemOrNull(extractor, comp.OutputSlotId(i)) is { } item && SEntMan.HasComponent<SeedComponent>(item))
                packets.Add(item);
        }

        return packets;
    }

    private static void AssertPotency(BotanySystem botany, SeedComponent seed, float expectedPotency, string plantId)
    {
        Assert.That(seed.PlantData, Is.Not.Null, $"{plantId} lost its produce snapshot.");
        Assert.That(botany.TryGetPlantComponent<PlantComponent>(seed.PlantData, seed.PlantProtoId, out var plant), Is.True);
        Assert.That(plant!.Potency, Is.EqualTo(expectedPotency), $"{plantId} packet snapshot changed.");
    }

    private static void CollectPackets(IEntityManager entities, HashSet<EntityUid> packets)
    {
        var existingPackets = entities.EntityQueryEnumerator<SeedComponent>();
        while (existingPackets.MoveNext(out var packet, out _))
            packets.Add(packet);
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

    private static Dictionary<string, List<SeedComponent>> GetSeedPackets(
        IEntityManager entities,
        HashSet<EntityUid> initialPackets)
    {
        var packets = new Dictionary<string, List<SeedComponent>>();
        var query = entities.EntityQueryEnumerator<SeedComponent>();
        while (query.MoveNext(out var uid, out var seed))
        {
            if (initialPackets.Contains(uid))
                continue;

            var plantId = seed.PlantProtoId.Id;
            if (!packets.TryGetValue(plantId, out var seeds))
                packets[plantId] = seeds = new List<SeedComponent>();
            seeds.Add(seed);
        }

        return packets;
    }
}

#pragma warning restore RA0002
