#nullable enable

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReagentPrototype))]
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
            var protoMan = server.ResolveDependency<IPrototypeManager>();
            var icedCoffee = protoMan.Index(IcedCoffee);
            var digestion = icedCoffee.Metabolisms!.Metabolisms["Digestion"];

            Assert.That(digestion.Metabolites, Is.Not.Null);
            Assert.That(digestion.Metabolites!.TryGetValue("Theobromine", out var ratio), Is.True);
            Assert.That(digestion.MetabolismRate * ratio, Is.EqualTo(FixedPoint2.New(0.05f)));
        });

        await pair.CleanReturnAsync();
    }
}
