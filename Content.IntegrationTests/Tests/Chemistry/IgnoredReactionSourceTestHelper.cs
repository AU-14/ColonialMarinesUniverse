using System.IO;
using System.Text;
using Content.Shared.Chemistry.Reaction;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests.Chemistry;

internal static class IgnoredReactionSourceTestHelper
{
    private static readonly ResPath IgnoreManifest = new("/IgnoredPrototypes/cm_ignoredPrototypes.yml");

    public static YamlMappingNode LoadReaction(
        IResourceManager resourceManager,
        IPrototypeManager prototypeManager,
        ResPath sourcePath,
        string reactionId)
    {
        var ignoreRoot = LoadRoot(resourceManager, IgnoreManifest);
        var ignoredPaths = ignoreRoot.Children
            .Cast<YamlScalarNode>()
            .Select(node => node.AsString());

        Assert.Multiple(() =>
        {
            Assert.That(
                ignoredPaths,
                Does.Contain(sourcePath.ToString()),
                $"{sourcePath} is no longer ignored; migrate this regression to the active prototype.");
            Assert.That(
                prototypeManager.TryIndex<ReactionPrototype>(reactionId, out _),
                Is.False,
                $"{reactionId} is now active; reassess the RMC chemistry policy before keeping the source-only port.");
        });

        var sourceRoot = LoadRoot(resourceManager, sourcePath);
        var reaction = sourceRoot.Children
            .Cast<YamlMappingNode>()
            .SingleOrDefault(node =>
                node.GetNode("type").AsString() == "reaction" &&
                node.GetNode("id").AsString() == reactionId);

        Assert.That(reaction, Is.Not.Null, $"Could not find reaction {reactionId} in {sourcePath}.");
        return reaction!;
    }

    private static YamlSequenceNode LoadRoot(IResourceManager resourceManager, ResPath path)
    {
        using var stream = resourceManager.ContentFileRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var yaml = new YamlStream();
        yaml.Load(reader);

        Assert.That(yaml.Documents, Has.Count.EqualTo(1), $"Expected one YAML document in {path}.");
        return (YamlSequenceNode) yaml.Documents[0].RootNode;
    }
}
