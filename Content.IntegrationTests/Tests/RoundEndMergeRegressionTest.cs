#nullable enable
using System.Collections;
using System.Reflection;
using Content.Client.RoundEnd;
using Content.IntegrationTests.Fixtures;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using RoundEndPlayerInfo = Content.Shared.GameTicking.RoundEndMessageEvent.RoundEndPlayerInfo;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(RoundEndSummaryWindow))]
public sealed class RoundEndMergeRegressionTest : GameTest
{
    [SidedDependency(Side.Client)] private readonly IUserInterfaceManager _uiManager = null!;

    [Test]
    [RunOnSide(Side.Client)]
    public async Task SummaryWindowDeduplicatesRoundAndPreservesStatsSearchAndSortContracts()
    {
        var players = new[]
        {
            Player("antag-zulu", "Zulu Antag", "job-name-captain", antag: true, connected: true),
            Player("antag-bravo", "Bravo Antag", "job-name-clown", antag: true, connected: false),
            Player("crew-alpha", "Alpha Crew", "job-name-engineer", connected: true),
            Player("crew-delta", "Delta Crew", "job-name-doctor", connected: false),
            Player("observer-charlie", "Charlie Observer", "job-name-chaplain", observer: true, connected: true),
        };
        var stats = new RoundEndSummaryStats(
            [new RoundEndSummaryStat("job-name-botanist", "job-name-borg", 17, RoundEndSummaryStatColor.Red)],
            [new RoundEndSummaryStat("job-name-cadet", "job-name-chef", 4, RoundEndSummaryStatColor.Purple)]);
        var firstMessage = Message(4401, players, stats);
        var nextMessage = Message(4402, players, RoundEndSummaryStats.Empty);

        await Client.WaitAssertion(() =>
        {
            var controller = _uiManager.GetUIController<RoundEndSummaryUIController>();
            var windowField = typeof(RoundEndSummaryUIController)
                .GetField("_window", BindingFlags.Instance | BindingFlags.NonPublic)!;

            RoundEndSummaryWindow? first = null;
            RoundEndSummaryWindow? second = null;
            try
            {
                controller.OpenRoundEndSummaryWindow(firstMessage);
                first = (RoundEndSummaryWindow) windowField.GetValue(controller)!;
                controller.OpenRoundEndSummaryWindow(firstMessage);
                Assert.That(windowField.GetValue(controller), Is.SameAs(first),
                    "replayed messages for one RoundId must not create another window");

                var labels = Descendants(first).OfType<Label>().Select(label => label.Text).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(first.RoundId, Is.EqualTo(4401));
                    Assert.That(labels, Does.Contain(Loc.GetString("job-name-botanist")));
                    Assert.That(labels, Does.Contain(Loc.GetString("job-name-borg")));
                    Assert.That(labels, Does.Contain("17"));
                    Assert.That(labels, Does.Contain(Loc.GetString("job-name-cadet")));
                    Assert.That(labels, Does.Contain(Loc.GetString("job-name-chef")));
                    Assert.That(labels, Does.Contain("4"));
                });

                AssertPlayerOrder(first,
                    "Bravo Antag",
                    "Zulu Antag",
                    "Alpha Crew",
                    "Delta Crew",
                    "Charlie Observer");

                AssertSearch(first, "alpha", "Alpha Crew");
                AssertSearch(first, "observer-charlie", "Charlie Observer");
                AssertSearch(first, "job-name-engineer", "Alpha Crew");
                AssertSearch(first, Loc.GetString("job-name-engineer"), "Alpha Crew");
                AssertSearch(first,
                    Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-antag"),
                    "Bravo Antag",
                    "Zulu Antag");
                SetPrivate(first, "_searchText", string.Empty);

                foreach (var field in new[] { "ICName", "Role", "PlayerType", "OOCName" })
                {
                    InvokeSort(first, field);
                    Assert.That(GetPrivate<bool>(first, "_sortDescending"), Is.False,
                        $"selecting the new {field} field begins ascending");
                    InvokeSort(first, field);
                    Assert.That(GetPrivate<bool>(first, "_sortDescending"), Is.True,
                        $"pressing the active {field} field toggles descending");
                }

                controller.OpenRoundEndSummaryWindow(nextMessage);
                second = (RoundEndSummaryWindow) windowField.GetValue(controller)!;
                Assert.Multiple(() =>
                {
                    Assert.That(second, Is.Not.SameAs(first));
                    Assert.That(second.RoundId, Is.EqualTo(4402));
                });
            }
            finally
            {
                if (first?.IsOpen == true)
                    first.Close();
                if (second?.IsOpen == true)
                    second.Close();
                windowField.SetValue(controller, null);
            }
        });
    }

    private static RoundEndPlayerInfo Player(
        string ooc,
        string ic,
        string role,
        bool antag = false,
        bool observer = false,
        bool connected = false)
    {
        return new RoundEndPlayerInfo
        {
            PlayerOOCName = ooc,
            PlayerICName = ic,
            Role = role,
            JobPrototypes = Array.Empty<string>(),
            AntagPrototypes = Array.Empty<string>(),
            Antag = antag,
            Observer = observer,
            Connected = connected,
        };
    }

    private static RoundEndMessageEvent Message(
        int roundId,
        RoundEndPlayerInfo[] players,
        RoundEndSummaryStats stats)
    {
        return new RoundEndMessageEvent(
            "Test mode",
            "Round complete",
            TimeSpan.FromMinutes(73),
            roundId,
            players.Length,
            players,
            null,
            stats);
    }

    private static void AssertSearch(RoundEndSummaryWindow window, string search, params string[] expectedIcNames)
    {
        SetPrivate(window, "_searchText", search);
        AssertPlayerOrder(window, expectedIcNames);
    }

    private static void AssertPlayerOrder(RoundEndSummaryWindow window, params string[] expectedIcNames)
    {
        var method = typeof(RoundEndSummaryWindow)
            .GetMethod("GetSortedPlayers", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var players = ((IEnumerable) method.Invoke(window, null)!)
            .Cast<RoundEndPlayerInfo>()
            .Select(player => player.PlayerICName)
            .ToArray();
        Assert.That(players, Is.EqualTo(expectedIcNames));
    }

    private static void InvokeSort(RoundEndSummaryWindow window, string fieldName)
    {
        var sortType = typeof(RoundEndSummaryWindow)
            .GetNestedType("SortField", BindingFlags.NonPublic)!;
        var method = typeof(RoundEndSummaryWindow)
            .GetMethod("SortBy", BindingFlags.Instance | BindingFlags.NonPublic)!;
        method.Invoke(window, [Enum.Parse(sortType, fieldName)]);
    }

    private static T GetPrivate<T>(RoundEndSummaryWindow window, string fieldName)
    {
        return (T) typeof(RoundEndSummaryWindow)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(window)!;
    }

    private static void SetPrivate(RoundEndSummaryWindow window, string fieldName, object value)
    {
        typeof(RoundEndSummaryWindow)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(window, value);
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (var child in root.Children)
        {
            yield return child;
            foreach (var nested in Descendants(child))
                yield return nested;
        }
    }
}
