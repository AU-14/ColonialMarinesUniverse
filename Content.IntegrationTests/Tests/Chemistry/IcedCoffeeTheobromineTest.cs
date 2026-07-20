#nullable enable

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.Solution;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(AdjustReagent))]
public sealed class IcedCoffeeTheobromineTest
{
    private static readonly ProtoId<ReagentPrototype> IcedCoffee = "IcedCoffee";

    [Test]
    public async Task IcedCoffeeProducesTheobromineDuringMetabolism()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var effects = entMan.System<SharedEntityEffectsSystem>();
            var icedCoffee = protoMan.Index(IcedCoffee);

            var effect = icedCoffee.Metabolisms!.Metabolisms.Values
                .SelectMany(entry => entry.Effects)
                .OfType<AdjustReagent>()
                .Single(entry => entry.Reagent == "Theobromine");

            Assert.That(effect.Amount, Is.EqualTo(FixedPoint2.New(0.05f)));

            var solutionEntity = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var solution = entMan.AddComponent<SolutionComponent>(solutionEntity);
            effects.ApplyEffect(solutionEntity, effect);

            Assert.That(
                solution.Solution.GetTotalPrototypeQuantity("Theobromine"),
                Is.EqualTo(FixedPoint2.New(0.05f)));
        });

        await pair.CleanReturnAsync();
    }
}
