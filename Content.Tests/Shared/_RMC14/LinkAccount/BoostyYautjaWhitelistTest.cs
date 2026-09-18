using Content.Shared._RMC14.LinkAccount;
using NUnit.Framework;

namespace Content.Tests.Shared._RMC14.LinkAccount;

[TestFixture]
public sealed class BoostyYautjaWhitelistTest
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void HunterIsAllowedForPrioritiesOneThroughFour(int priority)
    {
        Assert.That(BoostyYautjaWhitelist.IsAllowed("CMUYautjaHunter", priority), Is.True);
    }

    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void HunterIsRejectedForPrioritiesFiveThroughSeven(int priority)
    {
        Assert.That(BoostyYautjaWhitelist.IsAllowed("CMUYautjaHunter", priority), Is.False);
    }

    [Test]
    public void OtherJobsAndMissingPriorityAreRejected()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BoostyYautjaWhitelist.IsAllowed("CMUYautjaYoungblood", 1), Is.False);
            Assert.That(BoostyYautjaWhitelist.IsAllowed("CMUYautjaBadBlood", 1), Is.False);
            Assert.That(BoostyYautjaWhitelist.IsAllowed("CMUYautjaHunter", null), Is.False);
        });
    }
}
