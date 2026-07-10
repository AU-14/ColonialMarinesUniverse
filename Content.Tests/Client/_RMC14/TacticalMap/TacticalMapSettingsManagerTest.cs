using System.IO;
using System.Linq;
using Content.Client._RMC14.TacticalMap;
using NUnit.Framework;
using Robust.Shared.Serialization.Markdown;

namespace Content.Tests.Client._RMC14.TacticalMap;

[TestFixture]
public sealed class TacticalMapSettingsManagerTest
{
    [Test]
    public void WriterEmitsEmptySequencesExplicitly()
    {
        using var writer = new StringWriter();

        TacticalMapSettingsManager.WriteSettingsYaml(writer, [], []);
        var yaml = writer.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(yaml, Does.Contain("settings: []"));
            Assert.That(yaml, Does.Contain("unsetSettings: []"));
            Assert.That(
                () => DataNodeParser.ParseYamlStream(new StringReader(yaml)).Single(),
                Throws.Nothing);
        });
    }

    [Test]
    public void WriterEscapesQuotedPlanetNames()
    {
        using var writer = new StringWriter();
        var settings = new[]
        {
            new TacticalMapSettingRegistration
            {
                Key = "ZoomFactor",
                Value = 1.0f,
                PlanetId = "JE-1758 \"Ascanius\"",
            },
        };

        TacticalMapSettingsManager.WriteSettingsYaml(writer, settings, []);
        var yaml = writer.ToString();

        Assert.Multiple(() =>
        {
            Assert.That(yaml, Does.Contain("Value: 1.000000"));
            Assert.That(yaml, Does.Contain("PlanetId: \"JE-1758 \\\"Ascanius\\\"\""));
            Assert.That(
                () => DataNodeParser.ParseYamlStream(new StringReader(yaml)).Single(),
                Throws.Nothing);
        });
    }
}
