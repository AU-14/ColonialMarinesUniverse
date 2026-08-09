using Content.Server._RMC14.Rules.DistressSignal;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Rules.DistressSignal;

[TestFixture]
[TestOf(typeof(CMDistressSignalRuleSystem))]
public sealed class CMDistressSignalDropshipInitializationTest
{
    [Test]
    public void PlatoonDropshipsSuppressLegacyDropshipInitialization()
    {
        Assert.That(CMDistressSignalRuleSystem.ShouldInitializeLegacyDropships(true), Is.False);
    }

    [Test]
    public void LegacyDropshipsRemainForRoundsWithoutPlatoonDropships()
    {
        Assert.That(CMDistressSignalRuleSystem.ShouldInitializeLegacyDropships(false), Is.True);
    }
}
