using Content.Shared.CMU14.Yautja;

namespace Content.IntegrationTests.CMU14.Yautja;

[TestFixture]
public sealed class YautjaRankAccessTest
{
    [TestCase(YautjaRank.Unblooded, false)]
    [TestCase(YautjaRank.YoungBlood, false)]
    [TestCase(YautjaRank.Blooded, false)]
    [TestCase(YautjaRank.Elite, false)]
    [TestCase(YautjaRank.Elder, false)]
    [TestCase(YautjaRank.Leader, true)]
    [TestCase(YautjaRank.Ancient, true)]
    public void ClanLeaderSetsFollowRank(YautjaRank rank, bool allowed)
    {
        var profile = YautjaCharacterProfile.Default
            .WithRank(rank)
            .WithUnique(YautjaUniqueSet.Plated);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaRankResolver.CanUseUnique(rank), Is.EqualTo(allowed));
            Assert.That(YautjaRankResolver.CanUseUnique(profile), Is.EqualTo(allowed));
            Assert.That(profile.Unique, Is.EqualTo(YautjaUniqueSet.Plated));
        });
    }

    [TestCase(YautjaRank.Blooded)]
    [TestCase(YautjaRank.Elite)]
    [TestCase(YautjaRank.Elder)]
    public void DroppingBelowClanLeaderClearsALeaderSet(YautjaRank rank)
    {
        var profile = YautjaCharacterProfile.Default
            .WithUnique(YautjaUniqueSet.Plated)
            .WithRank(rank);

        Assert.That(profile.Unique, Is.EqualTo(YautjaUniqueSet.None));
    }

    [Test]
    public void RankChangesKeepALegacyWhitelistSet()
    {
        var profile = YautjaCharacterProfile.Default
            .WithUnique(YautjaUniqueSet.Ronin)
            .WithRank(YautjaRank.Blooded);

        Assert.That(profile.Unique, Is.EqualTo(YautjaUniqueSet.Ronin));
    }

    // Legacy whitelist: Anubys, Cleopatra, Ronin, Praetorian, Enforcer and the three Seekers. Everything else is
    // clan leader and above. Elites and elders get no sets.
    [TestCase(YautjaRank.Elite, false, YautjaUniqueSet.Ronin, false)]
    [TestCase(YautjaRank.Blooded, true, YautjaUniqueSet.Praetorian, true)]
    [TestCase(YautjaRank.Ancient, false, YautjaUniqueSet.Praetorian, false)]
    [TestCase(YautjaRank.Blooded, true, YautjaUniqueSet.SeekerInitiate, true)]
    [TestCase(YautjaRank.Elder, true, YautjaUniqueSet.Defender, false)]
    [TestCase(YautjaRank.Leader, false, YautjaUniqueSet.Warrior, true)]
    public void UniqueSetsSanitizeToTheirLock(YautjaRank rank, bool canUseLegacy, YautjaUniqueSet unique, bool allowed)
    {
        var capabilities = new YautjaProfileCapabilities(rank, YautjaRankResolver.CanUseUnique(rank), canUseLegacy);
        var profile = YautjaCharacterProfile.Default
            .WithUnique(unique)
            .SanitizeForCapabilities(capabilities);

        Assert.Multiple(() =>
        {
            Assert.That(capabilities.CanUseUniqueSet(unique), Is.EqualTo(allowed));
            Assert.That(profile.Unique, Is.EqualTo(allowed ? unique : YautjaUniqueSet.None));
        });
    }

    [TestCase(YautjaRank.Blooded, true, YautjaLegacySet.Seeker, true)]
    [TestCase(YautjaRank.Blooded, true, YautjaLegacySet.Collector, false)]
    [TestCase(YautjaRank.Leader, false, YautjaLegacySet.Shadow, true)]
    [TestCase(YautjaRank.Leader, false, YautjaLegacySet.Enforcer, false)]
    public void LegacySetsSanitizeToTheirLock(YautjaRank rank, bool canUseLegacy, YautjaLegacySet legacy, bool allowed)
    {
        var capabilities = new YautjaProfileCapabilities(rank, YautjaRankResolver.CanUseUnique(rank), canUseLegacy);
        var profile = YautjaCharacterProfile.Default
            .WithLegacy(legacy)
            .SanitizeForCapabilities(capabilities);

        Assert.That(profile.Legacy, Is.EqualTo(allowed ? legacy : YautjaLegacySet.None));
    }

    [Test]
    public void InvalidRankFallsBackToBlooded()
    {
        var profile = YautjaCharacterProfile.Default.WithRank((YautjaRank) 99);

        Assert.Multiple(() =>
        {
            Assert.That(profile.ClanRank, Is.EqualTo(YautjaRank.Blooded));
            Assert.That(profile.OwnerRank, Is.EqualTo(YautjaBracerOwnerRank.Unblooded));
        });
    }
}
