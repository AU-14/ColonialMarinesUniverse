using Content.Shared.Damage;
using Content.Shared._CMU14.Yautja;
using NUnit.Framework;

namespace Content.Tests.Shared.Medical.CrewMonitoring;

[TestFixture]
public sealed class YautjaCrewMonitoringMetadataTest
{
    [TestCase(YautjaRank.Ancient, false, "cmu-yautja-rank-ancient")]
    [TestCase(YautjaRank.Leader, false, "cmu-yautja-rank-leader")]
    [TestCase(YautjaRank.Elder, false, "cmu-yautja-rank-elder")]
    [TestCase(YautjaRank.Elite, false, "cmu-yautja-rank-elite")]
    [TestCase(YautjaRank.Blooded, false, "cmu-yautja-rank-blooded")]
    [TestCase(YautjaRank.YoungBlood, false, "cmu-yautja-rank-youngblood")]
    [TestCase(YautjaRank.Unblooded, false, "cmu-yautja-rank-unblooded")]
    [TestCase(YautjaRank.Blooded, true, "cmu-yautja-rank-badblood")]
    public void AssignmentUsesAuthoritativeRankOrBadBloodMarker(YautjaRank rank, bool isBadBlood, string expected)
    {
        Assert.That(YautjaCrewMonitoringMetadata.GetAssignment(rank, isBadBlood).ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void SumDamageGroupAddsOnlyTheRequestedTypes()
    {
        var damage = new DamageSpecifier
        {
            DamageDict = new()
            {
                ["Asphyxiation"] = 3,
                ["Bloodloss"] = 2,
                ["Poison"] = 7,
                ["Radiation"] = 1,
                ["Heat"] = 11,
                ["Shock"] = 2,
                ["Cold"] = 4,
                ["Caustic"] = 3,
                ["Blunt"] = 13,
                ["Slash"] = 5,
                ["Piercing"] = 2,
            }
        };

        Assert.Multiple(() =>
        {
            Assert.That(YautjaCrewMonitoringMetadata.SumDamageGroup(damage, ["Asphyxiation", "Bloodloss"]), Is.EqualTo(5));
            Assert.That(YautjaCrewMonitoringMetadata.SumDamageGroup(damage, ["Poison", "Radiation"]), Is.EqualTo(8));
            Assert.That(YautjaCrewMonitoringMetadata.SumDamageGroup(damage, ["Heat", "Shock", "Cold", "Caustic"]), Is.EqualTo(20));
            Assert.That(YautjaCrewMonitoringMetadata.SumDamageGroup(damage, ["Blunt", "Slash", "Piercing"]), Is.EqualTo(20));
        });
    }
}
