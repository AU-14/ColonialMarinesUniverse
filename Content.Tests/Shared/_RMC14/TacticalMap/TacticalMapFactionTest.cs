using Content.Shared._RMC14.TacticalMap;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.TacticalMap;

[TestFixture]
public sealed class TacticalMapFactionTest
{
    [Test]
    public void HumanFactionNormalizationAcceptsAuSides()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedTacticalMapSystem.NormalizeHumanFaction("marine"), Is.EqualTo(SharedTacticalMapSystem.MarinesFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeHumanFaction("UNMC"), Is.EqualTo(SharedTacticalMapSystem.MarinesFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeHumanFaction("govfor"), Is.EqualTo(SharedTacticalMapSystem.GovforFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeHumanFaction("GOVFOR-RMC"), Is.EqualTo(SharedTacticalMapSystem.GovforFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeHumanFaction("opfor"), Is.EqualTo(SharedTacticalMapSystem.OpforFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeHumanFaction("CLF"), Is.EqualTo(SharedTacticalMapSystem.ClfFaction));
        });
    }

    [Test]
    public void ComputerFactionNormalizationPreservesAllFactionComputers()
    {
        Assert.Multiple(() =>
        {
            Assert.That(SharedTacticalMapSystem.NormalizeMapFaction(null), Is.Null);
            Assert.That(SharedTacticalMapSystem.NormalizeMapFaction(string.Empty), Is.Null);
            Assert.That(SharedTacticalMapSystem.NormalizeMapFaction("govfor"), Is.EqualTo(SharedTacticalMapSystem.GovforFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeMapFaction("xeno"), Is.EqualTo(SharedTacticalMapSystem.XenosFaction));
            Assert.That(SharedTacticalMapSystem.NormalizeMapFaction("thirdparty"), Is.EqualTo("THIRDPARTY"));
        });
    }
}
