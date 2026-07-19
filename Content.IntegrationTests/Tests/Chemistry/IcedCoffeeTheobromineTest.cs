#nullable enable

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects;
using Content.Shared.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(AdjustReagent))]
public sealed class IcedCoffeeTheobromineTest
{
    [Test]
    public async Task IcedCoffeeProducesTheobromineDuringMetabolism()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.ResolveDependency<IEntityManager>();
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var icedCoffee = protoMan.Index<ReagentPrototype>("IcedCoffee");

            var effect = icedCoffee.Metabolisms!.Values
                .SelectMany(entry => entry.Effects)
                .OfType<AdjustReagent>()
                .Single(entry => entry.Reagent == "Theobromine");

            Assert.That(effect.Amount, Is.EqualTo(FixedPoint2.New(0.05f)));

            var source = new Solution();
            var args = new EntityEffectReagentArgs(
                EntityUid.Invalid,
                entMan,
                null,
                source,
                FixedPoint2.New(0.5f),
                icedCoffee,
                null,
                FixedPoint2.New(1));

            effect.Effect(args);

            Assert.That(
                source.GetTotalPrototypeQuantity("Theobromine"),
                Is.EqualTo(FixedPoint2.New(0.05f)));
        });

        await pair.CleanReturnAsync();
    }
}
