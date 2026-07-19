using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReactionPrototype))]
public sealed class MuteToxinReactionTest
{
    private const string ReactionId = "MuteToxin";

    private static readonly ProtoId<ReagentPrototype> MuteToxin = "MuteToxin";
    private static readonly ProtoId<ReagentPrototype> SpaceGlue = "SpaceGlue";
    private static readonly ProtoId<ReagentPrototype> Uranium = "Uranium";
    private static readonly ProtoId<ReagentPrototype> Vestine = "Vestine";

    [Test]
    public async Task DoesNotRequireUranium()
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
                Assert.That(reaction.Reactants.ContainsKey(Uranium), Is.False);
                Assert.That(
                    reaction.Reactants[Vestine].Amount,
                    Is.EqualTo(FixedPoint2.New(2)));
                Assert.That(
                    reaction.Reactants[SpaceGlue].Amount,
                    Is.EqualTo(FixedPoint2.New(2)));
                Assert.That(
                    reaction.Products[MuteToxin],
                    Is.EqualTo(FixedPoint2.New(2)));
            });
        });

        await pair.CleanReturnAsync();
    }
}
