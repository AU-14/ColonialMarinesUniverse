using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Database;
using Content.Shared.CMU14.RoundStatistics;
using NUnit.Framework;

namespace Content.Tests.CMU14.RoundStatistics;

[TestFixture]
public sealed class CMURoundStatisticsTest
{
    [Test]
    public void PlanetScopeAndArchiveSearchDoNotChangeGlobalHistory()
    {
        var records = new List<CMURoundOutcomeRecord>
        {
            Record(11, CMURoundStatisticsWinner.Xeno),
            Record(12, CMURoundStatisticsWinner.Govfor),
            Record(13, CMURoundStatisticsWinner.Unknown) with { PlanetId = "OtherPlanet" },
            Record(14, CMURoundStatisticsWinner.Govfor) with { Preset = CMURoundStatisticsPreset.Insurgency },
        };
        var dashboard = ServerDbBase.BuildCMURoundStatisticsDashboard(records,
            new CMURoundStatisticsQuery(planetId: "Planet", search: "12", page: 99));

        Assert.Multiple(() =>
        {
            Assert.That(dashboard.TotalRecords, Is.EqualTo(4));
            Assert.That(dashboard.LatestRoundId, Is.EqualTo(14));
            Assert.That(dashboard.Modes.Single(mode => mode.Preset == CMURoundStatisticsPreset.DistressSignal).Total, Is.EqualTo(3));
            Assert.That(dashboard.SelectedMode!.Total, Is.EqualTo(2));
            Assert.That(dashboard.SelectedMode.DecidedTotal, Is.EqualTo(2));
            Assert.That(dashboard.ArchiveTotal, Is.EqualTo(1));
            Assert.That(dashboard.RecentRounds.Select(record => record.RoundId), Is.EqualTo(new[] { 12 }));
            Assert.That(dashboard.Query.Page, Is.Zero);
            Assert.That(dashboard.Matchups.Single(pair => pair.AgainstThreat).Statistics.Total, Is.EqualTo(2));
        });
    }

    [Test]
    public void MissingPairingMetadataDoesNotBecomeAnOpponentOrChangeDenominators()
    {
        var records = new List<CMURoundOutcomeRecord>
        {
            Record(1, CMURoundStatisticsWinner.Xeno),
            Record(2, CMURoundStatisticsWinner.Govfor),
            Record(3, CMURoundStatisticsWinner.Draw),
            Record(4, CMURoundStatisticsWinner.Unknown),
            Record(5, CMURoundStatisticsWinner.Xeno) with { GovforPlatoonId = null },
            Record(6, CMURoundStatisticsWinner.Govfor) with { SelectedThreatId = null, OpforPlatoonId = "Opponent" },
        };
        var dashboard = ServerDbBase.BuildCMURoundStatisticsDashboard(records, new CMURoundStatisticsQuery());
        var threat = dashboard.Matchups.Single(pair => pair.AgainstThreat).Statistics;
        var platoon = dashboard.Matchups.Single(pair => !pair.AgainstThreat).Statistics;
        Assert.Multiple(() =>
        {
            Assert.That(dashboard.SelectedMode!.Total, Is.EqualTo(6));
            Assert.That(threat.Total, Is.EqualTo(4));
            Assert.That(threat.DecidedTotal, Is.EqualTo(2));
            Assert.That(threat.SideAWins, Is.EqualTo(1));
            Assert.That(threat.SideBWins, Is.EqualTo(1));
            Assert.That(threat.Draws, Is.EqualTo(1));
            Assert.That(threat.Unknown, Is.EqualTo(1));
            Assert.That(platoon.Total, Is.EqualTo(1));
            Assert.That(platoon.SideBWins, Is.EqualTo(1));
        });
    }

    [Test]
    public void ArchivePagesAreStableAndClampedWithoutLosingOlderRounds()
    {
        var records = Enumerable.Range(1, 29).Select(id => Record(id, CMURoundStatisticsWinner.Xeno)).ToList();
        var first = ServerDbBase.BuildCMURoundStatisticsDashboard(records, new CMURoundStatisticsQuery(page: -4));
        var second = ServerDbBase.BuildCMURoundStatisticsDashboard(records, new CMURoundStatisticsQuery(page: 1));
        var last = ServerDbBase.BuildCMURoundStatisticsDashboard(records, new CMURoundStatisticsQuery(page: 999));
        Assert.Multiple(() =>
        {
            Assert.That(first.RecentRounds.Select(record => record.RoundId), Is.EqualTo(Enumerable.Range(18, 12).Reverse()));
            Assert.That(second.RecentRounds.Select(record => record.RoundId), Is.EqualTo(Enumerable.Range(6, 12).Reverse()));
            Assert.That(last.RecentRounds.Select(record => record.RoundId), Is.EqualTo(Enumerable.Range(1, 5).Reverse()));
            Assert.That(last.Query.Page, Is.EqualTo(2));
            Assert.That(last.SelectedMode!.Total, Is.EqualTo(29));
        });
    }

    private static CMURoundOutcomeRecord Record(int id, CMURoundStatisticsWinner winner) => new(
        id, CMURoundStatisticsPreset.DistressSignal, winner, CMURoundStatisticsOutcome.Unknown,
        "Recorded outcome", "XenoThreat", "Planet", "Platoon", null, 180, 3600,
        new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc));

    [TestCase("XenoThreat", null, new[] { 6, 5, 4, 2, 1 })]
    [TestCase(null, "100-149", new[] { 3, 2 })]
    [TestCase("XenoThreat", "300+", new[] { 6, 5 })]
    public void DetailScopesKeepArchiveAndBreakdownsTogether(string? threat, string? band, int[] expected)
    {
        var records = new List<CMURoundOutcomeRecord>
        {
            Record(1, CMURoundStatisticsWinner.Xeno) with { PlayerCount = 99 },
            Record(2, CMURoundStatisticsWinner.Govfor) with { PlayerCount = 100 },
            Record(3, CMURoundStatisticsWinner.Unknown) with { PlayerCount = 149, SelectedThreatId = "OtherThreat" },
            Record(4, CMURoundStatisticsWinner.Draw) with { PlayerCount = 150 },
            Record(5, CMURoundStatisticsWinner.Govfor) with { PlayerCount = 300 },
            Record(6, CMURoundStatisticsWinner.Xeno) with { PlayerCount = 420 },
        };
        var dashboard = ServerDbBase.BuildCMURoundStatisticsDashboard(records,
            new CMURoundStatisticsQuery(threatId: threat, populationBand: band));
        Assert.Multiple(() =>
        {
            Assert.That(dashboard.RecentRounds.Select(record => record.RoundId), Is.EqualTo(expected));
            Assert.That(dashboard.SelectedMode!.Total, Is.EqualTo(expected.Length));
            Assert.That(dashboard.SelectedMode.Planets.Sum(planet => planet.Total), Is.EqualTo(expected.Length));
            Assert.That(dashboard.Matchups.Where(pair => pair.AgainstThreat).Sum(pair => pair.Statistics.Total), Is.EqualTo(expected.Length));
            Assert.That(dashboard.TotalRecords, Is.EqualTo(6));
            Assert.That(dashboard.Modes.Single(mode => mode.Preset == CMURoundStatisticsPreset.DistressSignal).Total, Is.EqualTo(6));
        });
    }

    [Test]
    public void ForceOnForceSeparatesOpforFromInsurgencyAcrossBreakdowns()
    {
        var records = new List<CMURoundOutcomeRecord>();
        foreach (var winner in new[]
                 {
                     CMURoundStatisticsWinner.Govfor, CMURoundStatisticsWinner.Opfor,
                     CMURoundStatisticsWinner.Opfor, CMURoundStatisticsWinner.Draw, CMURoundStatisticsWinner.Unknown,
                 })
        {
            records.Add(Record(records.Count + 1, winner) with
            {
                Preset = CMURoundStatisticsPreset.ForceOnForce,
                SelectedThreatId = null,
                OpforPlatoonId = "Opponent",
            });
        }
        records.Add(Record(6, CMURoundStatisticsWinner.Clf) with { Preset = CMURoundStatisticsPreset.Insurgency });
        var dashboard = ServerDbBase.BuildCMURoundStatisticsDashboard(records,
            new CMURoundStatisticsQuery(CMURoundStatisticsPreset.ForceOnForce));
        var mode = dashboard.SelectedMode!;
        Assert.Multiple(() =>
        {
            Assert.That(mode.Total, Is.EqualTo(5));
            Assert.That(mode.SideAWins, Is.EqualTo(1));
            Assert.That(mode.SideBWins, Is.EqualTo(2));
            Assert.That(mode.DecidedTotal, Is.EqualTo(3));
            Assert.That(mode.Planets.Single().SideBWins, Is.EqualTo(2));
            Assert.That(mode.PlayerCountBands.Single().SideBWins, Is.EqualTo(2));
            Assert.That(dashboard.Matchups.Single().Statistics.SideBWins, Is.EqualTo(2));
            Assert.That(dashboard.Matchups.Single().AgainstThreat, Is.False);
            Assert.That(dashboard.RecentRounds.Select(record => record.RoundId), Is.EqualTo(new[] { 5, 4, 3, 2, 1 }));
            Assert.That(dashboard.Modes.Single(other => other.Preset == CMURoundStatisticsPreset.Insurgency).SideBWins, Is.EqualTo(1));
        });
    }
}
