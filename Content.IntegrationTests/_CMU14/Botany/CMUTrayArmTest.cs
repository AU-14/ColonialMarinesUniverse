#pragma warning disable RA0002 // Integration regression intentionally sets restricted plant state.

using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Automation;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.CMU14.Automation;
using Content.Shared.CMU14.Botany;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Botany;

/// <summary>
/// CMU14 tray arms (M2: ARCH-03, COUPLING-02, TEST-04): the harvester buffers ripe produce for inserters, the planter plants
/// filtered seed packets, both need power and leave the automatic tray alone. Each test steps the arm system by hand.
/// </summary>
[TestFixture]
public sealed class CMUTrayArmTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    private static readonly Angle East = Angle.FromDegrees(90);
    private static readonly Angle West = Angle.FromDegrees(270);

    [Test]
    public async Task HarvesterBuffersRipeProduce()
    {
        var map = await Pair.CreateTestMap();
        EntityUid arm = default;
        EntityUid carrot = default;

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            carrot = PlantCarrot(tray, Tile(map, 1), ripe: true);

            Act();

            var buffered = Buffered(arm);
            Assert.That(buffered, Is.Not.Empty, "The harvester did not buffer the ripe plant's produce.");
            Assert.That(buffered.All(item => SEntMan.HasComponent<ProduceComponent>(item)), Is.True);
            Assert.That(SEntMan.Deleted(carrot) || SEntMan.IsQueuedForDeletion(carrot)
                    || !SComp<PlantHolderComponent>(carrot).ReadyForHarvest, Is.True,
                "The plant was left ready for harvest.");
        });
    }

    [Test]
    public async Task HarvesterIgnoresUnripe()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            var carrot = PlantCarrot(tray, Tile(map, 1), ripe: false);

            Act();

            Assert.That(Buffered(arm), Is.Empty, "The harvester took produce from an unripe plant.");
            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out var plant), Is.True);
            Assert.That(plant, Is.EqualTo(carrot), "The unripe plant was removed.");
        });
    }

    [Test]
    public async Task UnpoweredArmDoesNothing()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: false);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            var carrot = PlantCarrot(tray, Tile(map, 1), ripe: true);

            Act();

            Assert.That(Buffered(arm), Is.Empty, "An unpowered harvester harvested.");
            Assert.That(SComp<PlantHolderComponent>(carrot).ReadyForHarvest, Is.True);
        });
    }

    [Test]
    public async Task PlanterConsumesSeedAndAppliesGenes()
    {
        var map = await Pair.CreateTestMap();
        EntityUid arm = default;
        EntityUid tray = default;
        EntityUid seed = default;

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            arm = SpawnArm("CMUTrayArmPlanter", map, 0, East, powered: true);
            tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            seed = SpawnSeed("CabbagePlants", 42, arm);
            Load(arm, "seed1", seed);

            Act();

            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out var plant), Is.True,
                "The planter did not plant the tray.");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(plant!.Value).EntityPrototype?.ID, Is.EqualTo("CabbagePlants"));
                Assert.That(SComp<PlantComponent>(plant.Value).Potency, Is.EqualTo(42f), "The plant did not get the seed's genes.");
            });
        });

        await RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.EntityExists(seed), Is.False, "Planting did not use up the seed packet.");
            Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(arm, "seed1"), Is.Null);
        });
    }

    [Test]
    public async Task PlanterSkipsOccupiedTrayAndKeepsSeed()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmPlanter", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            var carrot = PlantCarrot(tray, Tile(map, 1), ripe: false);
            var seed = SpawnSeed("CabbagePlants", 42, arm);
            Load(arm, "seed1", seed);

            Act();

            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out var plant), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(plant, Is.EqualTo(carrot), "The planter replaced the plant in an occupied tray.");
                Assert.That(SEntMan.IsQueuedForDeletion(seed), Is.False, "The seed was used on an occupied tray.");
                Assert.That(SEntMan.System<ItemSlotsSystem>().GetItemOrNull(arm, "seed1"), Is.EqualTo(seed));
            });
        });
    }

    [Test]
    public async Task PlanterFilterRejectsWrongSpeciesAndLowPotency()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmPlanter", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            // Loaded before the filter is set, as when a player changes the filter of a loaded planter.
            var wrongSpecies = SpawnSeed("CabbagePlants", 77, arm);
            var weak = SpawnSeed("CarrotPlants", 10, arm);
            Load(arm, "seed1", wrongSpecies);
            Load(arm, "seed2", weak);

            SComp<CMUTrayArmComponent>(arm).Filter = new CMUSeedFilter
            {
                Species = new() { "CarrotPlants" },
                MinPotency = 50,
            };

            // From now on the planter refuses those seeds outright, so an inserter feeding it cannot jam it.
            var refused = SpawnSeed("CabbagePlants", 77, arm);
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(arm, "seed3", refused, null), Is.False,
                "The planter accepted a seed its filter refuses.");

            Act();

            var trays = SEntMan.System<PlantTraySystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            Assert.That(trays.TryGetPlant(tray, out _), Is.False, "The planter planted a seed its filter refuses.");
            Assert.Multiple(() =>
            {
                Assert.That(slots.GetItemOrNull(arm, "seed1"), Is.EqualTo(wrongSpecies));
                Assert.That(slots.GetItemOrNull(arm, "seed2"), Is.EqualTo(weak));
            });

            // A seed that passes is planted; the refused ones stay in their slots.
            var good = SpawnSeed("CarrotPlants", 60, arm);
            Load(arm, "seed3", good);
            Act();

            Assert.That(trays.TryGetPlant(tray, out var plant), Is.True, "The planter skipped the seed that passes its filter.");
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(plant!.Value).EntityPrototype?.ID, Is.EqualTo("CarrotPlants"));
                Assert.That(SComp<PlantComponent>(plant.Value).Potency, Is.EqualTo(60f));
                Assert.That(SEntMan.IsQueuedForDeletion(good), Is.True);
                Assert.That(slots.GetItemOrNull(arm, "seed1"), Is.EqualTo(wrongSpecies));
                Assert.That(slots.GetItemOrNull(arm, "seed2"), Is.EqualTo(weak));
            });
        });
    }

    [Test]
    public async Task ArmSkipsAutoFeedTray()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("RUMCAutoHydroponicsTray", Tile(map, 1));
            var carrot = PlantCarrot(tray, Tile(map, 1), ripe: true);

            Act();

            Assert.That(Buffered(arm), Is.Empty, "The harvester worked an automatic tray.");
            Assert.That(SComp<PlantHolderComponent>(carrot).ReadyForHarvest, Is.True);
        });
    }

    [Test]
    public async Task TwoPlantersOnOneTrayPlantOnce()
    {
        var map = await Pair.CreateTestMap();
        EntityUid first = default;
        EntityUid second = default;

        await Server.WaitAssertion(() =>
        {
            Floor(map, 3);
            var westArm = SpawnArm("CMUTrayArmPlanter", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            var eastArm = SpawnArm("CMUTrayArmPlanter", map, 2, West, powered: true);
            first = SpawnSeed("CabbagePlants", 42, westArm);
            second = SpawnSeed("CabbagePlants", 43, eastArm);
            Load(westArm, "seed1", first);
            Load(eastArm, "seed1", second);

            Act();

            Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out _), Is.True);
        });

        await RunTicksSync(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That((SEntMan.EntityExists(first) ? 1 : 0) + (SEntMan.EntityExists(second) ? 1 : 0), Is.EqualTo(1),
                "Two planters facing one tray planted twice (or not at all).");
        });
    }

    [Test]
    public async Task UnanchoringDropsTheBuffer()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            PlantCarrot(tray, Tile(map, 1), ripe: true);

            Act();
            var buffered = Buffered(arm);
            Assert.That(buffered, Is.Not.Empty);

            Server.System<SharedTransformSystem>().Unanchor(arm);

            Assert.That(Buffered(arm), Is.Empty, "Unanchoring kept the buffer.");
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(buffered.All(item => !containers.IsEntityInContainer(item)), Is.True);
        });
    }

    // Closed loop, last leg: an inserter takes the harvester's buffered produce onto a belt.
    [Test]
    public async Task InserterUnloadsHarvesterOutput()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 4);
            var belt = SSpawnAtPosition("RUMCConveyorBeltTile", Tile(map, 0));
            var inserter = SSpawnAtPosition("CMUInserterArm", Tile(map, 1));
            var arm = SpawnArm("CMUTrayArmHarvester", map, 2, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 3));
            PlantCarrot(tray, Tile(map, 3), ripe: true);
            Server.System<SharedTransformSystem>().SetLocalRotation(inserter, West);

            Act();
            var buffered = Buffered(arm);
            Assert.That(buffered, Is.Not.Empty);

            var inserters = Server.System<CMUInserterSystem>();
            inserters.Update(1);
            var held = SComp<CMUInserterComponent>(inserter).Claw.ContainedEntity;
            Assert.That(held, Is.Not.Null, "The inserter did not take produce out of the harvester.");
            Assert.That(buffered, Does.Contain(held!.Value));
            inserters.Update(1);
            Assert.Multiple(() =>
            {
                Assert.That(SComp<TransformComponent>(held.Value).Coordinates,
                    Is.EqualTo(SComp<TransformComponent>(belt).Coordinates), "The produce did not reach the belt.");
                Assert.That(Buffered(arm), Has.Count.EqualTo(buffered.Count - 1));
            });
        });
    }

    [Test]
    public async Task HarvesterSkipsMobProduceAndStillHarvests()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            PlantCarrot(tray, Tile(map, 1), ripe: true);

            // Killer tomatoes are "produce" too: a mob on the arm's tile is neither buffered nor in the way of a harvest.
            var mob = SSpawnAtPosition("MobTomatoKiller", Tile(map, 0));
            SEntMan.EnsureComponent<ProduceComponent>(mob);

            Act();

            var buffered = Buffered(arm);
            Assert.Multiple(() =>
            {
                Assert.That(buffered, Does.Not.Contain(mob), "The harvester buffered a mob.");
                Assert.That(buffered, Is.Not.Empty, "A mob on the arm's tile stopped the harvest.");
            });
        });
    }

    [Test]
    public async Task HarvesterWaitsWhileProduceIsLeftOnItsTile()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmHarvester", map, 0, East, powered: true);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1));
            var carrot = PlantCarrot(tray, Tile(map, 1), ripe: true);

            // A full buffer and one more carrot on the floor: the next harvest waits for an inserter instead of piling up.
            foreach (var id in SComp<CMUTrayArmComponent>(arm).ProduceSlots)
                Load(arm, id, SSpawnAtPosition("FoodCarrot", Tile(map, 0)));
            SSpawnAtPosition("FoodCarrot", Tile(map, 0));

            Act();

            Assert.That(SComp<PlantHolderComponent>(carrot).ReadyForHarvest, Is.True,
                "The harvester harvested while produce was still lying on its tile.");
        });
    }

    [Test]
    public async Task PlanterVerbsSetClearAndEmpty()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 2);
            var arm = SpawnArm("CMUTrayArmPlanter", map, 0, East, powered: true);
            var user = SSpawnAtPosition("MobHuman", Tile(map, 1));
            var verbs = SEntMan.System<SharedVerbSystem>();
            var hands = SEntMan.System<SharedHandsSystem>();
            var slots = SEntMan.System<ItemSlotsSystem>();
            var comp = SComp<CMUTrayArmComponent>(arm);

            AlternativeVerb Find(string key)
            {
                var text = Loc.GetString(key);
                var verb = verbs.GetLocalVerbs(arm, user, typeof(AlternativeVerb), force: true)
                    .OfType<AlternativeVerb>().FirstOrDefault(v => v.Text == text);
                Assert.That(verb, Is.Not.Null, $"No '{text}' verb.");
                return verb!;
            }

            // Holding a seed: "Plant this species" adds its species to the filter.
            var cabbage = SpawnSeed("CabbagePlants", 20, arm); // near the arm: SpawnSeedPacket hands the packet to `near`
            // Verbs see only the active hand's item.
            Assert.That(hands.TryPickup(user, cabbage), Is.True);
            Assert.That(hands.GetActiveItem(user), Is.EqualTo(cabbage), "The seed is not in the active hand.");
            Find("cmu-tray-arm-verb-filter-add").Act!.Invoke();
            Assert.That(comp.Filter.Species, Is.EquivalentTo(new[] { "CabbagePlants" }));

            // The filtered planter now accepts the held cabbage seed but refuses a carrot seed.
            Assert.That(hands.TryDrop(user, cabbage), Is.True);
            Load(arm, "seed1", cabbage);
            var carrotSeed = SpawnSeed("CarrotPlants", 20, arm);
            Assert.That(slots.TryInsert(arm, "seed2", carrotSeed, null), Is.False);

            // Empty hand: "Take the seed packets" empties the seed slots and "Clear the seed filter" clears the filter.
            Find("cmu-tray-arm-verb-take-seeds").Act!.Invoke();
            Assert.That(slots.GetItemOrNull(arm, "seed1"), Is.Null, "The seed packets were not taken out.");

            if (hands.IsHolding(user, cabbage))
                Assert.That(hands.TryDrop(user, cabbage), Is.True);
            Find("cmu-tray-arm-verb-filter-clear").Act!.Invoke();
            Assert.That(comp.Filter.Species, Is.Empty);
        });
    }

    // ECS-01: an arm works the 3x3 block of trays in front of it (up to three tiles ahead, one to each side), one tray an
    // act, and leaves alone the trays beside it and beyond the block.
    [Test]
    public async Task HarvesterWorksTheBlockInFrontOfIt()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 4, 2);
            SpawnArm("CMUTrayArmHarvester", map, 0, 1, East, powered: true);
            var near = Carrot(map, 1, 0);
            var farCorner = Carrot(map, 3, 2);
            var beside = Carrot(map, 0, 0);
            var beyond = Carrot(map, 4, 1);

            bool Harvested(EntityUid plant) => SEntMan.Deleted(plant) || SEntMan.IsQueuedForDeletion(plant)
                || !SComp<PlantHolderComponent>(plant).ReadyForHarvest;

            Act();
            Assert.That(Harvested(near), Is.True, "The nearest row of the block was not worked first.");
            Assert.That(Harvested(farCorner), Is.False, "One act should harvest one tray.");

            Act();
            Act();
            Assert.Multiple(() =>
            {
                Assert.That(Harvested(farCorner), Is.True, "The block's far corner, three tiles ahead, was not harvested.");
                Assert.That(Harvested(beside), Is.False, "A tray beside the arm, outside the block, was harvested.");
                Assert.That(Harvested(beyond), Is.False, "A tray four tiles ahead was harvested.");
            });
        });
    }

    [Test]
    public async Task PlanterPlantsTheBlockInFrontOfIt()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 4, 2);
            var arm = SpawnArm("CMUTrayArmPlanter", map, 0, 1, East, powered: true);
            var near = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 1, 2));
            var farCorner = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 3, 0));
            var beside = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 0, 2));
            var beyond = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 4, 1));
            Load(arm, "seed1", SpawnSeed("CabbagePlants", 20, arm));
            Load(arm, "seed2", SpawnSeed("CabbagePlants", 20, arm));
            Load(arm, "seed3", SpawnSeed("CabbagePlants", 20, arm));

            var trays = SEntMan.System<PlantTraySystem>();
            Act();
            Assert.That(trays.TryGetPlant(near, out _), Is.True, "The nearest row of the block was not worked first.");
            Assert.That(trays.TryGetPlant(farCorner, out _), Is.False, "One act should plant one tray.");

            Act();
            Act();
            Assert.Multiple(() =>
            {
                Assert.That(trays.TryGetPlant(farCorner, out _), Is.True, "The block's far corner was not planted.");
                Assert.That(trays.TryGetPlant(beside, out _), Is.False, "A tray beside the arm, outside the block, was planted.");
                Assert.That(trays.TryGetPlant(beyond, out _), Is.False, "A tray four tiles ahead was planted.");
            });
        });
    }

    // A plant can be ready with nothing to give (yield 0); the harvest does nothing, and that tray must not hold up the block.
    [Test]
    public async Task AReadyPlantWithNoYieldDoesNotStarveTheBlock()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 4, 2);
            SpawnArm("CMUTrayArmHarvester", map, 0, 1, East, powered: true);
            var barren = Carrot(map, 1, 1);
            SComp<PlantComponent>(barren).Yield = 0;
            var fruitful = Carrot(map, 3, 1);

            Act();
            Act();

            Assert.That(SEntMan.Deleted(fruitful) || SEntMan.IsQueuedForDeletion(fruitful)
                    || !SComp<PlantHolderComponent>(fruitful).ReadyForHarvest, Is.True,
                "A ready plant with no yield took every act from the rest of the block.");
        });
    }

    // ECS-03: the arm works left or right only; a north or south facing counts as west.
    [Test]
    public void WorkTilesFollowTheArmsFacing()
    {
        var tiles = new List<Vector2i>();
        var west = new[]
        {
            new Vector2i(4, 4), new Vector2i(4, 5), new Vector2i(4, 6),
            new Vector2i(3, 4), new Vector2i(3, 5), new Vector2i(3, 6),
            new Vector2i(2, 4), new Vector2i(2, 5), new Vector2i(2, 6),
        };
        CMUTrayArmSystem.GetWorkTiles(new Vector2i(5, 5), Angle.FromDegrees(270), tiles);
        Assert.That(tiles, Is.EquivalentTo(west), "Facing west the block is the three columns to the left of the arm.");

        CMUTrayArmSystem.GetWorkTiles(new Vector2i(5, 5), Angle.FromDegrees(180), tiles);
        Assert.That(tiles, Is.EquivalentTo(west), "A north facing must not work the rows above the arm.");

        // A rotation a little off a cardinal one still gives the cardinal block.
        CMUTrayArmSystem.GetWorkTiles(Vector2i.Zero, Angle.FromDegrees(80), tiles);
        Assert.That(tiles, Does.Contain(new Vector2i(3, 0)).And.Contain(new Vector2i(1, 1)).And.Not.Contain(new Vector2i(0, 1)));
    }

    // ECS-03: an arm built or wrenched down facing north or south turns to face east, a side its art can reach.
    [Test]
    public async Task ArmsFacingNorthOrSouthTurnEast()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 1);
            var transform = Server.System<SharedTransformSystem>();
            var arm = SSpawnAtPosition("CMUTrayArmPlanter", Tile(map, 0));
            Assert.That(SComp<TransformComponent>(arm).LocalRotation.GetCardinalDir(), Is.EqualTo(Direction.East),
                "A new arm (prototype facing south) did not turn east.");

            transform.Unanchor(arm);
            transform.SetLocalRotation(arm, Angle.FromDegrees(180));
            Assert.That(transform.AnchorEntity(arm), Is.True);
            Assert.That(SComp<TransformComponent>(arm).LocalRotation.GetCardinalDir(), Is.EqualTo(Direction.East),
                "Anchoring an arm that faces north did not turn it east.");

            // The rotate verbs' reset turns an anchored arm south; it must come back to east, not stay facing down.
            transform.SetLocalRotation(arm, Angle.FromDegrees(270));
            transform.SetLocalRotation(arm, Angle.Zero);
            Assert.That(SComp<TransformComponent>(arm).Anchored, Is.True);
            Assert.That(SComp<TransformComponent>(arm).LocalRotation.GetCardinalDir(), Is.EqualTo(Direction.East),
                "Resetting an anchored arm's rotation left it facing south.");
        });
    }

    // ECS-04: an act that works a tray records the reach for the client to animate; an act that works nothing does not.
    [Test]
    public async Task WorkingATrayRecordsTheReach()
    {
        var map = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Floor(map, 4, 2);
            var harvester = SpawnArm("CMUTrayArmHarvester", map, 0, 1, East, powered: true);
            var planter = SpawnArm("CMUTrayArmPlanter", map, 0, 0, East, powered: true);
            var comp = SComp<CMUTrayArmComponent>(harvester);
            var planterComp = SComp<CMUTrayArmComponent>(planter);
            var before = comp.ReachStartedAt;

            Act();
            Assert.That(comp.ReachStartedAt, Is.EqualTo(before), "An arm with nothing to work recorded a reach.");

            Carrot(map, 3, 2);
            var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, 2, 0));
            Load(planter, "seed1", SpawnSeed("CabbagePlants", 20, planter));
            Act();

            var now = Server.Timing.CurTime;
            Assert.Multiple(() =>
            {
                Assert.That(comp.ReachTarget, Is.EqualTo(new Vector2i(3, 1)), "The harvester's reach does not point at the tray it harvested.");
                Assert.That(comp.ReachStartedAt, Is.EqualTo(now));
                Assert.That(comp.ReachCarriesOut, Is.False);
                Assert.That(comp.ReachCarries, Is.Not.Null, "The harvester's claw shows no produce coming back.");
                Assert.That(planterComp.ReachTarget, Is.EqualTo(new Vector2i(2, 0)), "The planter's reach does not point at the tray it planted.");
                Assert.That(planterComp.ReachCarriesOut, Is.True);
                Assert.That(planterComp.ReachCarries, Is.Not.Null, "The planter's claw shows no seed packet going out.");
                Assert.That(SEntMan.System<PlantTraySystem>().TryGetPlant(tray, out _), Is.True);
                Assert.That(planterComp.ReachDuration, Is.LessThan((float) planterComp.Interval.TotalSeconds),
                    "The reach outlasts the time between acts.");
            });
        });
    }

    private EntityUid Carrot(TestMapData map, int x, int y)
    {
        var tray = SSpawnAtPosition("CMHydroponicsTray", Tile(map, x, y));
        return PlantCarrot(tray, Tile(map, x, y), ripe: true);
    }

    // Steps every arm once, as if its interval had elapsed.
    private void Act()
    {
        var query = SEntMan.EntityQueryEnumerator<CMUTrayArmComponent>();
        while (query.MoveNext(out _, out var arm))
            arm.NextAct = TimeSpan.Zero;

        Server.System<CMUTrayArmSystem>().Update(0.1f);
    }

    // Floor tiles from (0, 0) to (lastX, lastY), so the arms can anchor and their neighbours stand on the right tiles.
    private void Floor(TestMapData map, int lastX, int lastY = 0)
    {
        var maps = Server.System<SharedMapSystem>();
        for (var x = 0; x <= lastX; x++)
        {
            for (var y = 0; y <= lastY; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
        }
    }

    private static EntityCoordinates Tile(TestMapData map, int x, int y = 0)
    {
        return map.GridCoords.Offset(new Vector2(x + 0.5f, y + 0.5f));
    }

    private EntityUid SpawnArm(string id, TestMapData map, int x, Angle facing, bool powered)
    {
        return SpawnArm(id, map, x, 0, facing, powered);
    }

    private EntityUid SpawnArm(string id, TestMapData map, int x, int y, Angle facing, bool powered)
    {
        var arm = SSpawnAtPosition(id, Tile(map, x, y));
        var transform = Server.System<SharedTransformSystem>();
        if (!SComp<TransformComponent>(arm).Anchored)
            Assert.That(transform.AnchorEntity(arm), Is.True, "The arm did not anchor.");
        transform.SetLocalRotation(arm, facing);

        var receiver = SComp<ApcPowerReceiverComponent>(arm);
        if (powered)
            SEntMan.System<PowerReceiverSystem>().SetNeedsPower(arm, false);
        // No power net on the test map recomputes Powered, so set it as CMUAutoFeedTrayTest does.
        receiver.Powered = powered;
        SEntMan.Dirty(arm, receiver);
        return arm;
    }

    private void Load(EntityUid arm, string slot, EntityUid item)
    {
        Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(arm, slot, item, null), Is.True, $"Could not load {slot}.");
    }

    private List<EntityUid> Buffered(EntityUid arm)
    {
        var slots = SEntMan.System<ItemSlotsSystem>();
        var items = new List<EntityUid>();
        foreach (var id in SComp<CMUTrayArmComponent>(arm).ProduceSlots)
        {
            if (slots.GetItemOrNull(arm, id) is { } item)
                items.Add(item);
        }

        return items;
    }

    private EntityUid PlantCarrot(EntityUid tray, EntityCoordinates coords, bool ripe)
    {
        var carrot = SSpawnAtPosition("CarrotPlants", coords);
        SEntMan.System<PlantTraySystem>().PlantingPlantInTray(tray, carrot);
        SComp<PlantHolderComponent>(carrot).ReadyForHarvest = ripe;
        return carrot;
    }

    // A seed packet whose snapshot has the given potency, made the way the seed extractor makes packets.
    private EntityUid SpawnSeed(string plantProto, float potency, EntityUid near)
    {
        var botany = SEntMan.System<BotanySystem>();
        var plant = SEntMan.SpawnEntity(plantProto, MapCoordinates.Nullspace);
        SComp<PlantComponent>(plant).Potency = potency;
        var snapshot = botany.ClonePlantSnapshotData(plant);
        var seed = botany.SpawnSeedPacket(SComp<PlantDataComponent>(plant), plantProto, snapshot,
            SEntMan.GetComponent<TransformComponent>(near).Coordinates, near);

        Assert.That(SComp<SeedComponent>(seed).PlantData, Is.Not.Null);
        if (snapshot != null)
            SEntMan.DeleteEntity(snapshot.Value);
        SEntMan.DeleteEntity(plant);
        return seed;
    }
}

#pragma warning restore RA0002
