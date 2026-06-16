using Content.Shared.GameTicking;
using NUnit.Framework;

namespace Content.Tests.Shared.GameTicking;

[TestFixture]
public sealed class RoundEndSummaryStatsEventTest
{
    [Test]
    public void AddStatsKeepsZeroValuesAndPreservesCategoryOrder()
    {
        var ev = new RoundEndSummaryStatsEvent();

        ev.AddInjuryStat(
            "round-end-summary-window-stat-bones-broken",
            "round-end-summary-window-stat-bones-broken-detail",
            0,
            RoundEndSummaryStatColor.Red);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-defibs",
            "round-end-summary-window-stat-defibs-detail",
            0,
            RoundEndSummaryStatColor.Green);
        ev.AddInjuryStat(
            "round-end-summary-window-stat-surgeries",
            "round-end-summary-window-stat-surgeries-detail",
            4,
            RoundEndSummaryStatColor.Cyan);
        ev.AddOddityStat(
            "round-end-summary-window-stat-shrapnel-embedded",
            "round-end-summary-window-stat-shrapnel-embedded-detail",
            -1,
            RoundEndSummaryStatColor.Gold);
        ev.AddOddityStat(
            "round-end-summary-window-stat-limbs-stolen",
            "round-end-summary-window-stat-limbs-stolen-detail",
            2,
            RoundEndSummaryStatColor.Purple);

        var stats = ev.ToSummaryStats();

        Assert.Multiple(() =>
        {
            Assert.That(stats.InjuryStats, Has.Length.EqualTo(3));
            Assert.That(stats.InjuryStats[0].Label, Is.EqualTo("round-end-summary-window-stat-bones-broken"));
            Assert.That(stats.InjuryStats[0].Value, Is.Zero);
            Assert.That(stats.InjuryStats[0].Color, Is.EqualTo(RoundEndSummaryStatColor.Red));
            Assert.That(stats.InjuryStats[1].Label, Is.EqualTo("round-end-summary-window-stat-defibs"));
            Assert.That(stats.InjuryStats[1].Value, Is.Zero);
            Assert.That(stats.InjuryStats[1].Color, Is.EqualTo(RoundEndSummaryStatColor.Green));
            Assert.That(stats.InjuryStats[2].Label, Is.EqualTo("round-end-summary-window-stat-surgeries"));
            Assert.That(stats.InjuryStats[2].Detail, Is.EqualTo("round-end-summary-window-stat-surgeries-detail"));
            Assert.That(stats.InjuryStats[2].Value, Is.EqualTo(4));
            Assert.That(stats.InjuryStats[2].Color, Is.EqualTo(RoundEndSummaryStatColor.Cyan));

            Assert.That(stats.OddityStats, Has.Length.EqualTo(1));
            Assert.That(stats.OddityStats[0].Label, Is.EqualTo("round-end-summary-window-stat-limbs-stolen"));
            Assert.That(stats.OddityStats[0].Value, Is.EqualTo(2));
            Assert.That(stats.OddityStats[0].Color, Is.EqualTo(RoundEndSummaryStatColor.Purple));
        });
    }
}
