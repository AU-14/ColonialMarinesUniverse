using System.Collections.Generic;
using Content.Client.RoundEnd;
using Content.IntegrationTests.Fixtures;
using Content.Server.GameTicking;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests.RoundEnd;

[TestFixture]
public sealed class RoundEndSummaryWindowTest : GameTest
{
    [Test]
    public async Task UsesCMUReportAndRetainsManifestFiltering()
    {
        await Client.WaitAssertion(() =>
        {
            var stats = new RoundEndSummaryStats(
                [new RoundEndSummaryStat("round-end-summary-window-stat-bones-broken", "round-end-summary-window-stat-bones-broken-detail", 3, RoundEndSummaryStatColor.Red)],
                [new RoundEndSummaryStat("round-end-summary-window-stat-limbs-stolen", "round-end-summary-window-stat-limbs-stolen-detail", 1, RoundEndSummaryStatColor.Purple)]);
            var players = new[]
            {
                Player("marine", "Alpha Marine", false, false, true),
                Player("queen", "Hive Queen", true, false, false),
                Player("observer", "Spectator", false, true, true),
            };
            var message = new RoundEndMessageEvent(
                "Colony Fall",
                "CMU victory",
                TimeSpan.FromMinutes(42),
                48,
                players.Length,
                players,
                null,
                stats);
            var window = new RoundEndSummaryWindow(
                message.GamemodeTitle,
                message.RoundEndText,
                message.RoundDuration,
                message.RoundId,
                message.AllPlayersEndInfo,
                message.SummaryStats);

            try
            {
                var controls = Descendants(window).ToArray();
                var search = controls.OfType<LineEdit>().Single(control => control.Name == "ManifestSearch");

                Assert.Multiple(() =>
                {
                    Assert.That(message.SummaryStats.InjuryStats, Has.Length.EqualTo(1));
                    Assert.That(window.MinSize.X, Is.GreaterThanOrEqualTo(820));
                    Assert.That(controls.Count(control => control.Name == "RoundEndAfterActionReport"), Is.EqualTo(1));
                    Assert.That(controls.Count(control => control.Name == "RoundEndMetricGrid"), Is.EqualTo(1));
                    Assert.That(controls.Count(control => control.Name == "RoundEndSummaryStatCard"), Is.EqualTo(2));
                    Assert.That(controls.Count(control => control.Name?.StartsWith("ManifestSort") == true), Is.EqualTo(4));
                    Assert.That(controls.Count(control => control.Name == "RoundEndPlayerCard"), Is.EqualTo(3));
                    Assert.That(controls.OfType<Label>().Any(label => label.Text == "Bones broken"), Is.True);
                    Assert.That(PlayerCardNames(window), Is.EqualTo(new[] { "Hive Queen", "Alpha Marine", "Spectator" }));
                });

                search.SetText("Hive Queen", true);
                controls = Descendants(window).ToArray();

                Assert.Multiple(() =>
                {
                    Assert.That(controls.Count(control => control.Name == "RoundEndPlayerCard"), Is.EqualTo(1));
                    Assert.That(controls.OfType<Label>().Any(label => label.Text == "Hive Queen"), Is.True);
                });

                search.SetText(string.Empty, true);
                Assert.That(Descendants(window).Count(control => control.Name == "RoundEndPlayerCard"), Is.EqualTo(3));

                window.SortBy(RoundEndSummaryWindow.SortField.ICName);
                Assert.That(PlayerCardNames(window), Is.EqualTo(new[] { "Alpha Marine", "Hive Queen", "Spectator" }));

                window.SortBy(RoundEndSummaryWindow.SortField.ICName);
                Assert.That(PlayerCardNames(window), Is.EqualTo(new[] { "Spectator", "Hive Queen", "Alpha Marine" }));
            }
            finally
            {
                window.Dispose();
            }
        });
    }

    [Test]
    public async Task CollectsCMURoundEndTelemetry()
    {
        await Server.WaitAssertion(() =>
        {
            var stats = Server.System<GameTicker>().CollectRoundEndSummaryStats();

            Assert.Multiple(() =>
            {
                Assert.That(stats.InjuryStats.Any(stat => stat.Label == "round-end-summary-window-stat-bones-broken"), Is.True);
                Assert.That(stats.OddityStats.Any(stat => stat.Label == "round-end-summary-window-stat-bleeds-stopped"), Is.True);
            });
        });
    }

    private static RoundEndMessageEvent.RoundEndPlayerInfo Player(
        string oocName,
        string icName,
        bool antag,
        bool observer,
        bool connected)
    {
        return new RoundEndMessageEvent.RoundEndPlayerInfo
        {
            PlayerOOCName = oocName,
            PlayerICName = icName,
            Role = "game-ticker-unknown-role",
            JobPrototypes = [],
            AntagPrototypes = [],
            Antag = antag,
            Observer = observer,
            Connected = connected,
        };
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
                yield return descendant;
        }
    }

    private static string[] PlayerCardNames(Control root)
    {
        return Descendants(root)
            .Where(control => control.Name == "RoundEndPlayerCard")
            .Select(card => Descendants(card).OfType<Label>().First().Text)
            .ToArray();
    }
}
