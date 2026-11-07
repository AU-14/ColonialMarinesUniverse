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

    private static readonly ProtoId<WeightedRandomFillSolutionPrototype> RandomBotanyReagent = "RandomPickBotanyReagent";

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
            prototypes.Index(RandomBotanyReagent),
            SeededRandom(seed));
        var added = chemicals.Chemicals.Single(pair => !original.Contains(pair.Key));
        return new MutationResult(added.Key.Id, added.Value.Min, added.Value.Max,
            added.Value.PotencyDivisor, added.Value.Inherent);
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

    private sealed record MutationResult(
        string Chemical,
        FixedPoint2 Min,
        FixedPoint2 Max,
        float PotencyDivisor,
        bool Inherent);
}
