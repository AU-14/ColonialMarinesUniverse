using Content.Shared._CMU14.Yautja;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaRankAccessTest
{
    [TestCase(YautjaRank.Unblooded, false)]
    [TestCase(YautjaRank.YoungBlood, false)]
    [TestCase(YautjaRank.Blooded, false)]
    [TestCase(YautjaRank.Elite, true)]
    [TestCase(YautjaRank.Elder, true)]
    [TestCase(YautjaRank.Leader, true)]
    [TestCase(YautjaRank.Ancient, true)]
    public void UniqueSetsFollowRank(YautjaRank rank, bool allowed)
    {
        var profile = YautjaCharacterProfile.Default
            .WithRank(rank)
            .WithUnique(YautjaUniqueSet.Ronin);

        Assert.Multiple(() =>
        {
            Assert.That(YautjaRankResolver.CanUseUnique(rank), Is.EqualTo(allowed));
            Assert.That(YautjaRankResolver.CanUseUnique(profile), Is.EqualTo(allowed));
            Assert.That(profile.Unique, Is.EqualTo(YautjaUniqueSet.Ronin));
        });
    }

    [TestCase(YautjaRank.Unblooded)]
    [TestCase(YautjaRank.YoungBlood)]
    [TestCase(YautjaRank.Blooded)]
    public void ApplyingBelowEliteRankClearsUniqueSet(YautjaRank rank)
    {
        var profile = YautjaCharacterProfile.Default
            .WithUnique(YautjaUniqueSet.Ronin)
            .WithRank(rank);

        Assert.That(profile.Unique, Is.EqualTo(YautjaUniqueSet.None));
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
