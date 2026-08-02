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

    [Test]
    public async Task AllYautjaAudioFilesAreMono()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var audioRoot = new ResPath("/Audio/_CMU14/Yautja");
            var nonMono = new List<string>();
            var unreadable = new List<string>();

            foreach (var file in resources.ContentFindFiles(audioRoot)
                         .Where(path => path.Extension is "wav" or "ogg"))
            {
                using var stream = resources.ContentFileRead(file);
                var channels = ReadChannelCount(stream, file.Extension);
                if (channels is null)
                {
                    unreadable.Add(file.ToString());
                }
                else if (channels != 1)
                {
                    nonMono.Add($"{file} ({channels} channels)");
                }
            }

            Assert.That(unreadable, Is.Empty, string.Join(Environment.NewLine, unreadable));
            Assert.That(nonMono, Is.Empty, string.Join(Environment.NewLine, nonMono));
        });

        await pair.CleanReturnAsync();
    }

    private static int? ReadChannelCount(Stream stream, string extension)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        var data = buffer.ToArray();

        if (extension == "wav")
        {
            for (var i = 0; i + 12 <= data.Length; i++)
            {
                if (data[i] != 'f' || data[i + 1] != 'm' || data[i + 2] != 't' || data[i + 3] != ' ')
                    continue;

                return data[i + 10] | data[i + 11] << 8;
            }
        }
        else if (extension == "ogg")
        {
            for (var i = 1; i + 10 < data.Length; i++)
            {
                if (data[i - 1] != 1 || data[i] != 'v' || data[i + 1] != 'o' || data[i + 2] != 'r' ||
                    data[i + 3] != 'b' || data[i + 4] != 'i' || data[i + 5] != 's')
                    continue;

                return data[i + 10];
            }
        }

        return null;
    }
}
