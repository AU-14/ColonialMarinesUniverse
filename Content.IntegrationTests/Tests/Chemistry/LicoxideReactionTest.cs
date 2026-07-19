using Content.Shared.Chemistry.Reaction;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReactionPrototype))]
public sealed class LicoxideReactionTest
{
    private const string ReactionId = "Licoxide";
    private static readonly ResPath SourcePath = new("/Prototypes/Recipes/Reactions/fun.yml");

    [Test]
    public async Task UsesLithiumInsteadOfLead()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var reaction = IgnoredReactionSourceTestHelper.LoadReaction(
                server.ResolveDependency<IResourceManager>(),
                server.ResolveDependency<IPrototypeManager>(),
                SourcePath,
                ReactionId);
            var reactants = reaction.GetNode<YamlMappingNode>("reactants");
            var products = reaction.GetNode<YamlMappingNode>("products");

            Assert.Multiple(() =>
            {
                Assert.That(reactants.Children, Has.Count.EqualTo(2));
                Assert.That(reactants.HasNode("Lead"), Is.False);
                Assert.That(
                    reactants.GetNode<YamlMappingNode>("Lithium").GetNode("amount").AsString(),
                    Is.EqualTo("1"));
                Assert.That(
                    reactants.GetNode<YamlMappingNode>("Zinc").GetNode("amount").AsString(),
                    Is.EqualTo("1"));
                Assert.That(
                    products.GetNode("Licoxide").AsString(),
                    Is.EqualTo("1"));
            });
        });

        await pair.CleanReturnAsync();
    }
}
