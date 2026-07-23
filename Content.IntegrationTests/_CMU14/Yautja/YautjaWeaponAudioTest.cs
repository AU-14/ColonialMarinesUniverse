using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Robust.Server.Audio;
using Robust.Shared.ContentPack;
using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaWeaponAudioTest
{
    private static readonly Regex AudioPathRegex = new(@"^\s*path:\s*(/Audio/_CMU14/Yautja/\S+)");

    [Test]
    public async Task YautjaWeaponAudioPathsPointToExistingFiles()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var audio = server.EntMan.System<AudioSystem>();
            var missing = new List<string>();
            var equipmentPath = new ResPath("/Prototypes/_CMU14/Threats/Yautja/Equipment");

            foreach (var prototypePath in resources.ContentFindFiles(equipmentPath)
                         .Where(path => path.Extension == "yml" && !path.Filename.StartsWith('.')))
            {
                using var stream = resources.ContentFileRead(prototypePath);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = AudioPathRegex.Match(line);
                    if (!match.Success)
                        continue;

                    var audioPath = new ResPath(match.Groups[1].Value);
                    if (!resources.ContentFileExists(audioPath))
                    {
                        missing.Add($"{prototypePath}:{audioPath}");
                        continue;
                    }

                    Assert.DoesNotThrow(
                        () => audio.GetAudioLength(new ResolvedPathSpecifier(audioPath)),
                        $"Audio metadata must be readable for {prototypePath}:{audioPath}");
                }
            }

            Assert.That(missing, Is.Empty, string.Join(Environment.NewLine, missing));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task AllYautjaAudioFilesHaveReadableMetadata()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var audio = server.EntMan.System<AudioSystem>();
            var audioRoot = new ResPath("/Audio/_CMU14/Yautja");
            var files = resources.ContentFindFiles(audioRoot)
                .Where(path => path.Extension is "wav" or "ogg")
                .ToList();

            Assert.That(files, Is.Not.Empty, "Yautja audio directory must contain audio assets.");
            foreach (var file in files)
            {
                Assert.DoesNotThrow(
                    () => audio.GetAudioLength(new ResolvedPathSpecifier(file)),
                    $"Audio metadata must be readable for {file}");
            }
        });

        await pair.CleanReturnAsync();
    }
}
