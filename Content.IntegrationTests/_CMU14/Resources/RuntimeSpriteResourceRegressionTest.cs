using System.IO;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Resources;

[TestFixture]
public sealed class RuntimeSpriteResourceRegressionTest : GameTest
{
    [Test]
    public async Task DeathGaspCollectionsReferencePackagedAudio()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var resources = Pair.Client.ResolveDependency<IResourceManager>();

            foreach (var collectionId in new[] { "MaleDeathGasp", "FemaleDeathGasp" })
            {
                var collection = CProtoMan.Index<SoundCollectionPrototype>(collectionId);
                Assert.That(collection.PickFiles, Has.All.Matches<ResPath>(resources.ContentFileExists),
                    $"{collectionId} contains an audio path that is missing from packaged resources.");
            }
        });
    }

    [Test]
    public async Task VehicleResourceReferencesMatchPackagedPathCasing()
    {
        var badReferences = new List<string>();

        await Pair.Client.WaitAssertion(() =>
        {
            var resources = Pair.Client.ResolveDependency<IResourceManager>();
            foreach (var path in resources.ContentFindFiles("/Prototypes/"))
            {
                if (path.Extension != "yml")
                    continue;

                using var stream = resources.ContentFileRead(path);
                using var reader = new StreamReader(stream);
                var lineNumber = 0;
                while (reader.ReadLine() is { } line)
                {
                    lineNumber++;
                    if (line.Contains("CMU14/Structures/Vehicles/", StringComparison.Ordinal))
                        badReferences.Add($"{path}:{lineNumber}");
                }
            }
        });

        Assert.That(badReferences, Is.Empty,
            "Packaged resource paths are case-sensitive; use CMU14/Structures/vehicles/.");
    }

    [Test]
    public async Task StampInhandLayersDeclareTheStampRsi()
    {
        await Pair.Client.WaitAssertion(() =>
        {
            var resources = Pair.Client.ResolveDependency<IResourceManager>();
            var path = new ResPath("/Prototypes/Entities/Objects/Misc/rubber_stamp.yml");
            using var stream = resources.ContentFileRead(path);
            using var reader = new StreamReader(stream);
            var yaml = reader.ReadToEnd();

            Assert.That(yaml.Split("sprite: Objects/Misc/stamps.rsi").Length - 1, Is.EqualTo(5),
                "The world sprite and all four in-hand layers must declare the stamp RSI explicitly.");
        });
    }
}
