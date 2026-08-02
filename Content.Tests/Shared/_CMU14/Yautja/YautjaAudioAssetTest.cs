using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Content.Tests.Shared._CMU14.Yautja;

[TestFixture]
public sealed class YautjaAudioAssetTest
{
    [TestCase("pred_cloakon_modern.wav")]
    [TestCase("pred_cloakoff_modern.wav")]
    public void PositionalYautjaCloakAudioIsMono(string filename)
    {
        var path = Path.Combine(FindRepositoryRoot(), "Resources", "Audio", "_CMU14", "Yautja", filename);
        Assert.That(File.Exists(path), Is.True, $"Audio asset must exist: {path}");

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(4)), Is.EqualTo("RIFF"));
        reader.ReadInt32();
        Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(4)), Is.EqualTo("WAVE"));
        Assert.That(Encoding.ASCII.GetString(reader.ReadBytes(4)), Is.EqualTo("fmt "));
        reader.ReadInt32();
        reader.ReadInt16();
        Assert.That(reader.ReadInt16(), Is.EqualTo(1), $"Positioned cloak audio must be mono: {filename}");
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
