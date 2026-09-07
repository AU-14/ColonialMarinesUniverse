using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Prototypes;

[TestFixture]
[TestOf(typeof(TagPrototype))]
public sealed class CompatibilityTagTest : GameTest
{
    private static readonly ProtoId<TagPrototype>[] CompatibilityTags =
    [
        "Briefcase",
        "DrinkCup",
        "DrinkGlass",
        "GasTank",
        "Ingredient",
        "Machete",
        "RodMetal1",
        "Shovel",
        "Wall",
        "Wringer",
    ];

    [Test]
    public async Task ActiveCompatibilityTagsLoad()
    {
        await Server.WaitAssertion(() =>
        {
            Assert.That(CompatibilityTags, Has.Length.EqualTo(10));
            var ids = CompatibilityTags.Select(tag => tag.Id).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(ids, Is.Ordered);
                Assert.That(ids, Is.Unique);
            });

            foreach (var tag in CompatibilityTags)
                Assert.That(SProtoMan.TryIndex(tag, out _), Is.True, tag.Id);
        });
    }
}
