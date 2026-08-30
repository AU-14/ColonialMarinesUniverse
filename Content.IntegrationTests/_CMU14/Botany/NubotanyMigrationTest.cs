using System.Linq;
using System.Collections.Generic;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Random;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.IntegrationTests.CMU14.Botany;

[TestFixture]
public sealed class NubotanyMigrationTest
{
    private static readonly PlantContract[] Contracts =
    {
        new("CarrotPlants", "CarrotSeeds", "FoodCarrot", ["CMUConiine", "CMUPhenol"]),
        new("CabbagePlants", "CabbageSeeds", "FoodCabbage", ["CMUPsoralen"]),
        new("NettlePlants", "NettleSeeds", "Nettle", ["CMUUrishiol"]),
        new("DeathNettlePlants", "DeathNettleSeeds", "DeathNettle", ["CMUUrishiol"]),
        new("PoppyPlants", "PoppySeeds", "FoodPoppy", ["CMUAtropine"]),
        new("LingzhiPlants", "LingzhiSeeds", "FoodLingzhi", ["CMUZygacine"]),
        new("MTearPlants", "MTearSeeds", "FoodMTear", ["CMUDigoxin"]),
        new("DeathberryPlants", "DeathberrySeeds", "FoodDeathberry", ["CMUThymol"]),
        new("PoisonberryPlants", "PoisonberrySeeds", "FoodPoisonberry", ["CMUThymol"]),
        new("RMCMangoPlants", "RMCMangoSeeds", "RMCFoodMango", []),
    };

    private static readonly PlantConsumerContract[] MigratedConsumerContracts =
    {
        new("MTearPlants", "MTearSeeds", "FoodMTear"),
        new("DeathberryPlants", "DeathberrySeeds", "FoodDeathberry"),
        new("PoisonberryPlants", "PoisonberrySeeds", "FoodPoisonberry"),
        new("PoisonApplePlants", "PoisonAppleSeeds", "FoodPoisonApple"),
        new("PlumpPlants", "PlumpSeeds", "FoodMushroomPlump"),
        new("GrassPlants", "GrassSeeds", "FoodGrass"),
        new("WhiteBeetPlants", "WhiteBeetSeeds", "FoodWhiteBeet"),
        new("PeanutPlants", "PeanutSeeds", "FoodPeanut"),
        new("SunflowerPlants", "SunflowerSeeds", "FoodSunflower"),
        new("RMCMangoPlants", "RMCMangoSeeds", "RMCFoodMango"),
    };

    [Test]
    public async Task SpeciesPoolsAndTwentyPlantConsumersMatchMigrationContract()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var factory = server.EntMan.ComponentFactory;

            foreach (var contract in Contracts)
            {
                var plant = prototypes.Index<EntityPrototype>(contract.PlantId);
                Assert.That(plant.TryComp<PlantSpecialChemicalsComponent>(out var special, factory), Is.True,
                    contract.PlantId.Id);
                Assert.That(special!.Chemicals.Select(id => id.Id),
                    Is.EquivalentTo(contract.SpecialChemicals), contract.PlantId.Id);

                var packet = prototypes.Index<EntityPrototype>(contract.PacketId);
                Assert.That(packet.TryComp<SeedComponent>(out var seed, factory), Is.True, contract.PacketId.Id);
                Assert.That(seed!.PlantProtoId.Id, Is.EqualTo(contract.PlantId.Id), contract.PacketId.Id);

                var produce = prototypes.Index<EntityPrototype>(contract.ProduceId);
                Assert.That(produce.TryComp<ProduceComponent>(out var produceComponent, factory), Is.True,
                    contract.ProduceId.Id);
                Assert.That(produceComponent!.PlantProtoId?.Id, Is.EqualTo(contract.PlantId.Id),
                    contract.ProduceId.Id);
            }

            foreach (var contract in MigratedConsumerContracts)
            {
                var packet = prototypes.Index<EntityPrototype>(contract.PacketId);
                Assert.That(packet.TryComp<SeedComponent>(out var seed, factory), Is.True, contract.PacketId.Id);
                Assert.That(seed!.PlantProtoId.Id, Is.EqualTo(contract.PlantId.Id), contract.PacketId.Id);

                var produce = prototypes.Index<EntityPrototype>(contract.ProduceId);
                Assert.That(produce.TryComp<ProduceComponent>(out var produceComponent, factory), Is.True,
                    contract.ProduceId.Id);
                Assert.That(produceComponent!.PlantProtoId?.Id, Is.EqualTo(contract.PlantId.Id),
                    contract.ProduceId.Id);
            }

            var migratedPlantIds = MigratedConsumerContracts
                .Select(contract => contract.PlantId.Id)
                .ToHashSet();
            var migratedPackets = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.TryComp<SeedComponent>(out var seed, factory) &&
                                migratedPlantIds.Contains(seed.PlantProtoId.Id))
                .Select(proto => proto.ID);
            var migratedProduce = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.TryComp<ProduceComponent>(out var produce, factory) &&
                                produce.PlantProtoId is { } plantId &&
                                migratedPlantIds.Contains(plantId.Id))
                .Select(proto => proto.ID);

            Assert.Multiple(() =>
            {
                Assert.That(migratedPackets,
                    Is.EquivalentTo(MigratedConsumerContracts.Select(contract => contract.PacketId.Id)),
                    "The ten fork plant migrations must have exactly one packet consumer each.");
                Assert.That(migratedProduce,
                    Is.EquivalentTo(MigratedConsumerContracts.Select(contract => contract.ProduceId.Id)),
                    "The ten fork plant migrations must have exactly one produce consumer each.");
            });

            var plantsWithSpecialPools = prototypes.EnumeratePrototypes<EntityPrototype>()
                .Where(proto => proto.TryComp<PlantSpecialChemicalsComponent>(out _, factory))
                .Select(proto => proto.ID);
            Assert.That(plantsWithSpecialPools,
                Is.EquivalentTo(Contracts.Select(contract => contract.PlantId.Id)),
                "PlantSpecialChemicals must remain species-bound to the exact ten migrated plants.");

            var mangoPacket = server.EntMan.SpawnEntity("RMCMangoSeeds", MapCoordinates.Nullspace);
            var solutions = server.EntMan.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(mangoPacket, "food", out var solution), Is.True);
            Assert.That(solution!.Value.Comp.Solution.GetTotalPrototypeQuantity("CMUUrishiol"),
                Is.EqualTo((FixedPoint2) 3),
                "The mango packet's deliberate urishiol payload was lost during the plantId migration.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpecialAndFallbackMutationsPreserveInherentChemicalsAndUseLegacyRanges()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var randomChemicals = prototypes.Index<WeightedRandomFillSolutionPrototype>("RandomPickBotanyReagent");
            var system = entities.System<PlantChemicalsSystem>();

            var carrot = entities.SpawnEntity("CarrotPlants", MapCoordinates.Nullspace);
            var carrotChemicals = entities.GetComponent<PlantChemicalsComponent>(carrot);
            var inherent = carrotChemicals.Chemicals.ToDictionary(pair => pair.Key, pair => pair.Value);
            system.MutateRandomChemical(carrot, randomChemicals, SeededRandom(FindProbabilitySeed(true)));

            AssertInherentChemicalsUnchanged(inherent, carrotChemicals.Chemicals);
            var special = carrotChemicals.Chemicals.Single(pair => !inherent.ContainsKey(pair.Key));
            Assert.Multiple(() =>
            {
                Assert.That(special.Key.Id, Is.AnyOf("CMUConiine", "CMUPhenol"));
                Assert.That(special.Value.Min, Is.EqualTo((FixedPoint2) 7));
                Assert.That(special.Value.Max, Is.InRange((FixedPoint2) 12, (FixedPoint2) 15));
                Assert.That(special.Value.PotencyDivisor, Is.EqualTo(1));
                Assert.That(special.Value.Inherent, Is.False);
            });

            var mango = entities.SpawnEntity("RMCMangoPlants", MapCoordinates.Nullspace);
            var mangoChemicals = entities.GetComponent<PlantChemicalsComponent>(mango);
            var mangoInherent = mangoChemicals.Chemicals.ToDictionary(pair => pair.Key, pair => pair.Value);
            KeyValuePair<ProtoId<ReagentPrototype>, PlantChemQuantity>? fallback = null;
            for (var seed = 0; seed < 1024 && fallback == null; seed++)
            {
                system.MutateRandomChemical(mango, randomChemicals, SeededRandom(seed));
                var added = mangoChemicals.Chemicals
                    .Where(pair => !mangoInherent.ContainsKey(pair.Key))
                    .ToArray();
                if (added.Length > 0)
                    fallback = added[0];
            }

            Assert.That(fallback, Is.Not.Null, "No generated-class fallback chemical was added to mango.");
            AssertInherentChemicalsUnchanged(mangoInherent, mangoChemicals.Chemicals);
            Assert.Multiple(() =>
            {
                Assert.That(fallback!.Value.Value.Min, Is.EqualTo((FixedPoint2) 1));
                Assert.That(fallback.Value.Value.Max, Is.InRange((FixedPoint2) 1, (FixedPoint2) 2));
                Assert.That(fallback.Value.Value.PotencyDivisor, Is.EqualTo(1));
                Assert.That(fallback.Value.Value.Inherent, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SnapshotFallsBackToPrototypeAndCrossKeepsTargetPool()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var botany = entities.System<BotanySystem>();
            var target = entities.SpawnEntity("CarrotPlants", MapCoordinates.Nullspace);
            var pollen = entities.SpawnEntity("CabbagePlants", MapCoordinates.Nullspace);

            var snapshot = botany.ClonePlantSnapshotData(target);
            Assert.That(snapshot, Is.Not.Null);
            Assert.That(entities.HasComponent<PlantSpecialChemicalsComponent>(snapshot!.Value), Is.False,
                "PlantClone must deliberately omit species-bound special pools.");
            Assert.That(botany.TryGetPlantComponent<PlantSpecialChemicalsComponent>(
                snapshot, "CarrotPlants", out var snapshotPool), Is.True);
            Assert.That(snapshotPool!.Chemicals.Select(id => id.Id),
                Is.EquivalentTo(new[] { "CMUConiine", "CMUPhenol" }));

            var cross = new PlantCrossPollinateEvent(pollen, "CabbagePlants");
            entities.EventBus.RaiseLocalEvent(target, ref cross);
            Assert.That(entities.GetComponent<PlantSpecialChemicalsComponent>(target).Chemicals.Select(id => id.Id),
                Is.EquivalentTo(new[] { "CMUConiine", "CMUPhenol" }),
                "Cross-pollination replaced the target species' pool with pollen species data.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SpeciesChangeUsesReplacementSpeciesPool()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var tray = entities.SpawnEntity("hydroponicsTray", MapCoordinates.Nullspace);
            var carrot = entities.SpawnEntity("CarrotPlants", MapCoordinates.Nullspace);
            var trayComponent = entities.GetComponent<PlantTrayComponent>(tray);
            entities.System<PlantTraySystem>().PlantingPlantInTray((tray, trayComponent), carrot);

            var plantData = entities.GetComponent<PlantDataComponent>(carrot);
            plantData.MutationPrototypes = ["CabbagePlants"];
            entities.System<PlantMutationSystem>().SpeciesChange((carrot, plantData), "CabbagePlants");

            Assert.That(trayComponent.PlantEntity, Is.Not.Null.And.Not.EqualTo(carrot));
            var replacement = trayComponent.PlantEntity!.Value;
            Assert.That(entities.GetComponent<PlantSpecialChemicalsComponent>(replacement).Chemicals.Select(id => id.Id),
                Is.EquivalentTo(new[] { "CMUPsoralen" }),
                "SpeciesChange retained the old species' special pool.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task InjectedSeedProducesSameSpecialMutationOnClientAndServer()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var map = await pair.CreateTestMap();
        var player = pair.Player!;
        var originalAttached = player.AttachedEntity;
        var seed = FindProbabilitySeed(true);
        EntityUid serverCarrot = default;
        NetEntity carrotNet = default;
        MutationResult? serverResult = null;
        MutationResult? clientResult = null;

        try
        {
            await pair.Server.WaitPost(() =>
            {
                pair.Server.PlayerMan.SetAttachedEntity(player, map.Grid.Owner);
                serverCarrot = pair.Server.EntMan.SpawnEntity("CarrotPlants", map.GridCoords);
                carrotNet = pair.Server.EntMan.GetNetEntity(serverCarrot);
            });
            await pair.RunUntilSynced();

            EntityUid clientCarrot = default;
            await pair.Client.WaitAssertion(() =>
            {
                clientCarrot = pair.Client.EntMan.GetEntity(carrotNet);
                Assert.That(pair.Client.EntMan.EntityExists(clientCarrot), Is.True);
            });

            await pair.Server.WaitAssertion(() => serverResult = MutateCarrot(
                serverCarrot,
                pair.Server.EntMan,
                pair.Server.ResolveDependency<IPrototypeManager>(),
                seed));
            await pair.Client.WaitAssertion(() => clientResult = MutateCarrot(
                clientCarrot,
                pair.Client.EntMan,
                pair.Client.ResolveDependency<IPrototypeManager>(),
                seed));

            Assert.That(clientResult, Is.EqualTo(serverResult));
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                if (pair.Server.EntMan.EntityExists(serverCarrot))
                    pair.Server.EntMan.DeleteEntity(serverCarrot);
            });
            await pair.RunUntilSynced();
            await pair.Server.WaitPost(() => pair.Server.PlayerMan.SetAttachedEntity(player, originalAttached));
            await pair.CleanReturnAsync();
        }
    }

    private static MutationResult MutateCarrot(
        EntityUid plant,
        IEntityManager entities,
        IPrototypeManager prototypes,
        int seed)
    {
        var chemicals = entities.GetComponent<PlantChemicalsComponent>(plant);
        var original = chemicals.Chemicals.Keys.ToHashSet();
        entities.System<PlantChemicalsSystem>().MutateRandomChemical(
            plant,
            prototypes.Index<WeightedRandomFillSolutionPrototype>("RandomPickBotanyReagent"),
            SeededRandom(seed));
        var added = chemicals.Chemicals.Single(pair => !original.Contains(pair.Key));
        return new MutationResult(added.Key.Id, added.Value.Min, added.Value.Max,
            added.Value.PotencyDivisor, added.Value.Inherent);
    }

    private static void AssertInherentChemicalsUnchanged(
        IReadOnlyDictionary<ProtoId<ReagentPrototype>, PlantChemQuantity> expected,
        IReadOnlyDictionary<ProtoId<ReagentPrototype>, PlantChemQuantity> actual)
    {
        foreach (var (id, quantity) in expected)
        {
            Assert.That(actual, Contains.Key(id));
            var after = actual[id];
            Assert.Multiple(() =>
            {
                Assert.That(after.Min, Is.EqualTo(quantity.Min), id.Id);
                Assert.That(after.Max, Is.EqualTo(quantity.Max), id.Id);
                Assert.That(after.PotencyDivisor, Is.EqualTo(quantity.PotencyDivisor), id.Id);
                Assert.That(after.Inherent, Is.EqualTo(quantity.Inherent), id.Id);
            });
        }
    }

    private static int FindProbabilitySeed(bool expected)
    {
        for (var seed = 0; seed < 1024; seed++)
        {
            var random = SeededRandom(seed);
            if (random.Prob(0.4f) == expected)
                return seed;
        }

        Assert.Fail($"Could not find a deterministic seed for special-branch result {expected}.");
        return -1;
    }

    private static IRobustRandom SeededRandom(int seed)
    {
        var random = new RobustRandom();
        random.SetSeed(seed);
        return random;
    }

    private sealed record PlantContract(
        EntProtoId PlantId,
        EntProtoId PacketId,
        EntProtoId ProduceId,
        string[] SpecialChemicals);

    private sealed record PlantConsumerContract(
        EntProtoId PlantId,
        EntProtoId PacketId,
        EntProtoId ProduceId);

    private sealed record MutationResult(
        string Chemical,
        FixedPoint2 Min,
        FixedPoint2 Max,
        float PotencyDivisor,
        bool Inherent);
}
