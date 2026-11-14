#pragma warning disable RA0002 // Integration regression intentionally sets restricted plant state.

using Content.IntegrationTests.Fixtures;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.CMU14.Electric;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Botany;

/// <summary>
/// CMU14 seed-consuming auto-tray (N8): with FreeReplant off, a replant uses up the slotted seed and plants its genes;
/// with no seed, nothing is replanted.
/// </summary>
[TestFixture]
public sealed class CMUAutoFeedTrayTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private const float SeedPotency = 77;

    [Test]
    public async Task ReplantConsumesTheSlottedSeedAndPlantsItsGenes()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tray = default;
        EntityUid carrot = default;
        EntityUid seed = default;

        await Server.WaitAssertion(() =>
        {
            tray = SpawnPoweredTray(map.GridCoords);
            Assert.That(SComp<AutoFeedTrayComponent>(tray).FreeReplant, Is.False);
            carrot = PlantReadyCarrot(tray, map.GridCoords);
            seed = SpawnCabbageSeed(tray);
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(tray, SComp<AutoFeedTrayComponent>(tray).SeedSlot, seed, null),
                Is.True);
        });

        // One feed tick harvests, the next one replants.
        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            var trays = SEntMan.System<PlantTraySystem>();
            Assert.That(SEntMan.EntityExists(carrot), Is.False, "The ripe plant was not harvested.");
            Assert.That(SEntMan.EntityExists(seed), Is.False, "The replant did not use up the slotted seed.");
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(tray, SComp<AutoFeedTrayComponent>(tray).SeedSlot),
                Is.Null);

            Assert.That(trays.TryGetPlant(tray, out var plant), Is.True, "The tray did not replant from its seed.");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(plant!.Value).EntityPrototype?.ID, Is.EqualTo("CabbagePlants"),
                    "The replant used the remembered plant instead of the seed.");
                Assert.That(SComp<PlantComponent>(plant.Value).Potency, Is.EqualTo(SeedPotency),
                    "The replanted plant did not get the seed's genes.");
            });
        });
    }

    // A seedless plant is still harvested, and with FreeReplant off the replant uses the slotted seed, not the seedless genes.
    [Test]
    public async Task SeedlessHarvestStillWorksAndReplantsFromTheSlot()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tray = default;
        EntityUid carrot = default;
        EntityUid seed = default;
        var produceBefore = 0;

        await Server.WaitAssertion(() =>
        {
            tray = SpawnPoweredTray(map.GridCoords);
            carrot = PlantReadyCarrot(tray, map.GridCoords);
            SEntMan.EnsureComponent<PlantTraitSeedlessComponent>(carrot);
            seed = SpawnCabbageSeed(tray);
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(tray, SComp<AutoFeedTrayComponent>(tray).SeedSlot, seed, null),
                Is.True);
            produceBefore = CountProduce();
        });

        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(carrot), Is.False, "The ripe seedless plant was not harvested.");
                Assert.That(CountProduce(), Is.GreaterThan(produceBefore), "Harvesting the seedless plant made no produce.");
                Assert.That(SEntMan.EntityExists(seed), Is.False, "The replant did not use up the slotted seed.");
            });

            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out var plant), Is.True,
                "The tray did not replant from its seed after a seedless harvest.");
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(plant!.Value).EntityPrototype?.ID, Is.EqualTo("CabbagePlants"));
            Assert.That(SComp<PlantComponent>(plant.Value).Potency, Is.EqualTo(SeedPotency));
        });
    }

    [Test]
    public async Task EmptySeedSlotMeansNoReplant()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tray = default;
        EntityUid carrot = default;

        await Server.WaitAssertion(() =>
        {
            tray = SpawnPoweredTray(map.GridCoords);
            carrot = PlantReadyCarrot(tray, map.GridCoords);
        });

        await RunSeconds(4);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(carrot), Is.False, "The ripe plant was not harvested.");
            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out _), Is.False,
                "A tray with an empty seed slot replanted for free.");
        });
    }

    private int CountProduce()
    {
        var count = 0;
        var query = SEntMan.EntityQueryEnumerator<ProduceComponent>();
        while (query.MoveNext(out _, out _))
            count++;

        return count;
    }

    private EntityUid SpawnPoweredTray(EntityCoordinates coords)
    {
        var tray = SSpawnAtPosition("RUMCAutoHydroponicsTray", coords);
        SEntMan.System<PowerReceiverSystem>().SetNeedsPower(tray, false);
        // No power net on the test map recomputes Powered, so set it as CMUReactionGatesTest does.
        var receiver = SComp<ApcPowerReceiverComponent>(tray);
        receiver.Powered = true;
        SEntMan.Dirty(tray, receiver);
        return tray;
    }

    private EntityUid PlantReadyCarrot(EntityUid tray, EntityCoordinates coords)
    {
        var carrot = SSpawnAtPosition("CarrotPlants", coords);
        SEntMan.System<PlantTraySystem>().PlantingPlantInTray(tray, carrot);
        SComp<PlantHolderComponent>(carrot).ReadyForHarvest = true;
        return carrot;
    }

    // A cabbage packet whose snapshot has a distinctive potency, made the way the seed extractor makes packets.
    private EntityUid SpawnCabbageSeed(EntityUid tray)
    {
        var botany = SEntMan.System<BotanySystem>();
        var cabbage = SEntMan.SpawnEntity("CabbagePlants", MapCoordinates.Nullspace);
        SComp<PlantComponent>(cabbage).Potency = SeedPotency;
        var snapshot = botany.ClonePlantSnapshotData(cabbage);
        var seed = botany.SpawnSeedPacket(SComp<PlantDataComponent>(cabbage), "CabbagePlants", snapshot,
            SEntMan.GetComponent<TransformComponent>(tray).Coordinates, tray);

        Assert.That(SComp<SeedComponent>(seed).PlantData, Is.Not.Null);
        if (snapshot != null)
            SEntMan.DeleteEntity(snapshot.Value);
        SEntMan.DeleteEntity(cabbage);
        return seed;
    }
}

#pragma warning restore RA0002
