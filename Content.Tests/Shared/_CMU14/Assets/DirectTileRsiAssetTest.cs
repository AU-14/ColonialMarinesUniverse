using System;
using System.IO;
using System.Text.Json;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Assets;

[TestFixture]
public sealed class DirectTileRsiAssetTest
{
    [Test]
    public void DirectlyLoadedTileRsiIsNotPacked()
    {
        var repositoryRoot = FindRepositoryRoot();
        var path = Path.Combine(
            repositoryRoot,
            "Resources",
            "Textures",
            "_CMU14",
            "HunterShip",
            "turf",
            "floors",
            "hybrisafloors.rsi",
            "meta.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var metadata = document.RootElement;

        Assert.That(metadata.TryGetProperty("rsic", out var rsic), Is.True);
        Assert.That(rsic.GetBoolean(), Is.False);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Resources")))
                return directory.FullName;
        }

        Assert.Fail("Repository root was not found from the test output directory.");
        return string.Empty;
    }
}
