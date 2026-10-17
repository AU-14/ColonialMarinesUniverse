using Content.Client.Guidebook;
using Content.Client.Guidebook.Richtext;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Guidebook;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipCasGuidebookTest : GameTest
{
    [Test]
    public async Task CasIsReachableFromCmuRootAndBothEntriesParse()
    {
        await Client.WaitAssertion(() =>
        {
            var prototypes = Client.ResolveDependency<IPrototypeManager>();
            var resources = Client.ResolveDependency<IResourceManager>();
            var parser = Client.ResolveDependency<DocumentParsingManager>();
            var root = prototypes.Index<GuideEntryPrototype>("CMUGuidebook");
            Assert.That(root.Children.Select(child => child.Id), Does.Contain("CMUGuideCAS"));
            var cmu = prototypes.Index<GuideEntryPrototype>("CMUGuideCAS");
            var legacy = prototypes.Index<GuideEntryPrototype>("RMCGuideRoleCAS");
            Assert.That(cmu.Text, Is.EqualTo(legacy.Text), "Equipment help must open the same maintained handbook.");
            Assert.That(cmu.Text.ToString(), Is.EqualTo("/ServerInfo/Guidebook/CMU14/Guides/CMUCAS.xml"));
            foreach (var entry in new[] { root, cmu, legacy })
            {
                using var reader = resources.ContentFileReadText(entry.Text);
                Assert.That(parser.TryAddMarkup(new Document(), reader.ReadToEnd()), Is.True, entry.ID);
            }
        });
    }
}
