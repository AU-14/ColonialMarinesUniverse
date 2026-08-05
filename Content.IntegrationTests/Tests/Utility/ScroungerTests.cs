using System.IO;
using System.Linq;
using Content.IntegrationTests.Utility;
using Content.Packaging;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Utility;

[TestOf(typeof(GameDataScrounger))]
public sealed class ScroungerTests
{
    [Test]
    [Description("Assert that the standard and CMU resource roots do not contain colliding package paths.")]
    public void ResourceRootsHaveNoPackagingCollisions()
    {
        var roots = GameDataScrounger.ContentResourceRoots();
        Assert.That(roots, Has.Length.EqualTo(2));

        var contentRoot = Directory.GetParent(roots[0])?.FullName;
        Assert.That(contentRoot, Is.Not.Null);
        Assert.DoesNotThrow(() => SharedPackaging.ValidateCMUResourcePaths(contentRoot!));
    }

    [Test]
    [Description("Assert that the data scrounger finds prototypes by type successfully.")]
    public void ScroungeByType()
    {
        var scrounged = GameDataScrounger.PrototypesOfKind<EntityPrototype>();
        Assert.That(scrounged, Is.Not.Empty);
    }

    [Test]
    [Description("Assert that the data scrounger finds all files by pattern in a directory successfully.")]
    [TestCase("*.yml")]
    [TestCase("*.txt")]
    public void ScroungeByPattern(string pattern)
    {
        var files = GameDataScrounger.FilesInDirectory("/", pattern);

        Assert.That(files, Is.Not.Empty);
    }

    [Test]
    [Description("Assert that the data scrounger returns deterministic, unique, and valid VFS paths.")]
    public void ScroungeByPatternInVfs()
    {
        var files = GameDataScrounger.FilesInDirectoryInVfs("/Maps", "*.yml");
        var repeated = GameDataScrounger.FilesInDirectoryInVfs("/Maps", "*.yml");

        Assert.That(files, Is.Not.Empty);
        Assert.That(repeated, Is.EqualTo(files));
        Assert.That(files.Distinct().Count(), Is.EqualTo(files.Length));

        Assert.That(files[0].IsRooted, Is.True);
        Assert.That(files[0].ToString(), Does.StartWith("/Maps/"));
    }

    [Test]
    [Description("Assert that the data scrounger finds entities by component successfully.")]
    public void ScroungeByComponent()
    {
        var items = GameDataScrounger.EntitiesWithComponent("Item");

        Assert.That(items, Is.Not.Empty);
    }
}
