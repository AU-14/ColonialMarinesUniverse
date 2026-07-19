using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReactionPrototype))]
public sealed class LicoxideReactionTest
{
    private const string ReactionId = "Licoxide";

    private static readonly ProtoId<ReagentPrototype> Lead = "Lead";
    private static readonly ProtoId<ReagentPrototype> Licoxide = "Licoxide";
    private static readonly ProtoId<ReagentPrototype> Lithium = "Lithium";
    private static readonly ProtoId<ReagentPrototype> Zinc = "Zinc";

    [Test]
    public async Task UsesLithiumInsteadOfLead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var reaction = server.ResolveDependency<IPrototypeManager>()
                .Index<ReactionPrototype>(ReactionId);

            Assert.Multiple(() =>
            {
                Assert.That(reaction.Reactants, Has.Count.EqualTo(2));
                Assert.That(reaction.Reactants.ContainsKey(Lead), Is.False);
                Assert.That(reaction.Reactants.ContainsKey(Lithium), Is.True);
                Assert.That(reaction.Reactants.ContainsKey(Zinc), Is.True);
                Assert.That(
                    reaction.Reactants[Lithium].Amount,
                    Is.EqualTo(FixedPoint2.New(1)));
                Assert.That(
                    reaction.Reactants[Zinc].Amount,
                    Is.EqualTo(FixedPoint2.New(1)));
                Assert.That(
                    reaction.Products[Licoxide],
                    Is.EqualTo(FixedPoint2.New(1)));
            });
        });

        await pair.CleanReturnAsync();
    }
}
