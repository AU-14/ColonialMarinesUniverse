using System.Linq;
using Content.Shared.Botany.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects.Effects.Botany;
using Content.Shared.EntityEffects.Effects.Botany.PlantAttributes;
using Content.Shared.Random;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Botany;

/// <summary>
/// Prototype-only checks for the CMU14 plant mutation list overrides.
/// </summary>
[TestFixture]
public sealed class CMUMutationListTest
{
    private const string ReagentPool = "CMURandomPickBotanyReagent";

    private static readonly string[] BannedReagents =
    [
        "Omnizine", "Honk", "BuzzochloricBees", "Napalm", "ChlorineTrifluoride",
    ];

    [Test]
    public async Task MutationListHasNoKudzu()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            var list = protos.Index<RandomPlantMutationListPrototype>("RandomPlantMutations");
            Assert.That(list.Mutations.Any(m => m.Name == "Kudzufication"), Is.False);
            Assert.That(list.Mutations.Any(m => m.Effect is PlantChangeTraits { Trait: "PlantTraitKudzu" }), Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HeatToleranceRangesAreKelvin()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            var list = protos.Index<RandomPlantMutationListPrototype>("RandomPlantMutations");

            // Defaults of PlantAtmosphericComponent: Low 283 K, High 303 K.
            var low = list.Mutations.Single(m => m.Name == "ChangeLowHeatTolerance");
            var lowStat = (PlantChangeStat) low.Effect;
            Assert.That(lowStat.ApplyRange.Min, Is.LessThan(283f));
            Assert.That(lowStat.ApplyRange.Max, Is.GreaterThan(283f));

            var high = list.Mutations.Single(m => m.Name == "ChangeHighHeatTolerance");
            var highStat = (PlantChangeStat) high.Effect;
            Assert.That(highStat.ApplyRange.Min, Is.LessThan(303f));
            Assert.That(highStat.ApplyRange.Max, Is.GreaterThan(303f));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ChemicalMutationUsesCMUPool()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            var pool = protos.Index<WeightedRandomFillSolutionPrototype>(ReagentPool);
            var reagents = pool.Fills.SelectMany(f => f.Reagents).Select(r => r.Id).ToList();
            Assert.That(reagents, Is.Not.Empty);

            foreach (var reagent in reagents)
            {
                Assert.That(protos.HasIndex<ReagentPrototype>(reagent), Is.True, $"Unknown reagent {reagent}");
            }

            foreach (var banned in BannedReagents)
            {
                Assert.That(reagents, Does.Not.Contain(banned));
            }

            var list = protos.Index<RandomPlantMutationListPrototype>("RandomPlantMutations");
            var chemicals = list.Mutations.Single(m => m.Name == "ChangeChemicals");
            Assert.That(chemicals.Effect, Is.TypeOf<PlantMutateChemicals>());
            Assert.That(((PlantMutateChemicals) chemicals.Effect).RandomPickBotanyReagent.Id, Is.EqualTo(ReagentPool));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PoisonChainsResolve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protos = pair.Server.ProtoMan;

        await pair.Server.WaitAssertion(() =>
        {
            AssertMutatesInto(protos, "ApplePlants", "GoldenApplePlants", "PoisonApplePlants");
            AssertMutatesInto(protos, "BerryPlants", "DeathberryPlants", "PoisonberryPlants");
        });

        await pair.CleanReturnAsync();
    }

    private static void AssertMutatesInto(IPrototypeManager protos, string plant, params string[] targets)
    {
        var data = GetPlantData(protos, plant);
        foreach (var target in targets)
        {
            Assert.That(data.MutationPrototypes.Select(p => p.Id), Does.Contain(target),
                $"{plant} does not mutate into {target}");
            // The target must itself be a plant.
            GetPlantData(protos, target);
        }
    }

    private static PlantDataComponent GetPlantData(IPrototypeManager protos, string plant)
    {
        var proto = protos.Index<EntityPrototype>(plant);
        Assert.That(proto.Components.TryGetValue("PlantData", out var registry), Is.True,
            $"{plant} has no PlantData component");
        return (PlantDataComponent) registry!.Component;
    }
}
