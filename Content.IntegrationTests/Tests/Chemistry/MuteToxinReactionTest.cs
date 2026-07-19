using Content.Shared.Chemistry.Reaction;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests.Chemistry;

[TestFixture]
[TestOf(typeof(ReactionPrototype))]
public sealed class MuteToxinReactionTest
{
    private const string ReactionId = "MuteToxin";
    private static readonly ResPath SourcePath = new("/Prototypes/Recipes/Reactions/chemicals.yml");

    [Test]
    public async Task DoesNotRequireUranium()
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
                Assert.That(reactants.HasNode("Uranium"), Is.False);
                Assert.That(
                    reactants.GetNode<YamlMappingNode>("Vestine").GetNode("amount").AsString(),
                    Is.EqualTo("2"));
                Assert.That(
                    reactants.GetNode<YamlMappingNode>("SpaceGlue").GetNode("amount").AsString(),
                    Is.EqualTo("2"));
                Assert.That(
                    products.GetNode("MuteToxin").AsString(),
                    Is.EqualTo("2"));
            });
        });

        await pair.CleanReturnAsync();
    }
}
