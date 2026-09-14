using System.Collections.Generic;
using System.IO;
using System.Linq;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaPrototypeLoadingTest
{
    [Test]
    public async Task AllAuthoredYautjaEntitiesLoadOnBothSides()
    {
        await using var pair = await PoolManager.GetServerClient();
        var resources = pair.Server.ResolveDependency<IResourceManager>();
        var ids = new HashSet<string>();

        foreach (var path in resources.ContentFindFiles(new ResPath("/Prototypes/CMU14")))
        {
            if (path.Extension != "yml" ||
                !(path.ToString().Contains("/Yautja/") ||
                  path.Filename.StartsWith("huntership", StringComparison.Ordinal)))
                continue;

            using var stream = resources.ContentFileRead(path);
            using var reader = new StreamReader(stream);
            var yaml = new YamlStream();
            yaml.Load(reader);
            foreach (var document in yaml.Documents)
            {
                foreach (var node in ((YamlSequenceNode) document.RootNode).Cast<YamlMappingNode>())
                {
                    if (node.GetNode("type").AsString() == "entity" &&
                        !(node.TryGetNode<YamlScalarNode>("abstract", out var abstractNode) && abstractNode.AsString() == "true"))
                        ids.Add(node.GetNode("id").AsString());
                }
            }
        }

        Assert.That(ids.Count, Is.GreaterThan(1000), "The test must include the HunterShip resource root.");
        foreach (var instance in new Robust.UnitTesting.RobustIntegrationTest.IntegrationInstance[] { pair.Server, pair.Client })
        {
            await instance.WaitAssertion(() =>
            {
                var prototypes = instance.ResolveDependency<IPrototypeManager>();
                var missing = ids.Where(id => !prototypes.HasIndex<EntityPrototype>(id)).Order().ToArray();
                Assert.That(missing, Is.Empty,
                    "Authored entities must survive deserialization, including required fields in nested component registries.");
            });
        }

        await pair.CleanReturnAsync();
    }
}
