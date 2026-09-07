using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ClientSession;

[TestFixture]
[EnsureCVar(Side.Client, typeof(CCVars), nameof(CCVars.CrewManifestWithoutEntity), true)]
public sealed class TickerLateJoinMergeRegressionTest : GameTest
{
    private static readonly NetEntity Station = new(880001);

    [TestPrototypes]
    private const string Prototypes = """
- type: playTimeTracker
  id: ClientSessionGovTracker

- type: playTimeTracker
  id: ClientSessionOpTracker

- type: job
  id: ClientSessionGovJob
  name: client-session-gov-job
  description: client-session-gov-job-description
  playTimeTracker: ClientSessionGovTracker
  icon: JobIconUnknown

- type: job
  id: ClientSessionOpJob
  name: client-session-op-job
  description: client-session-op-job-description
  playTimeTracker: ClientSessionOpTracker
  icon: JobIconUnknown

- type: department
  parent: CMDepartmentBase
  id: ClientSessionGovDepartment
  name: department-Cargo
  description: department-Cargo-description
  color: "#334455"
  faction: govfor
  roles: [ ClientSessionGovJob ]

- type: department
  parent: CMDepartmentBase
  id: ClientSessionOpDepartment
  name: department-Cargo
  description: department-Cargo-description
  color: "#553344"
  faction: opfor
  roles: [ ClientSessionOpJob ]

- type: jobWeight
  id: ClientSessionStationWeights
  weights:
    ClientSessionGovJob: 1
    ClientSessionOpJob: 99
""";

    [Test]
    public async Task TickerCopiesTypedJobDataAndAdvancesRoundElapsedTime()
    {
        await Client.WaitAssertion(() =>
        {
            var ticker = Client.System<ClientGameTicker>();
            var updated = 0;
            IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>>? observed = null;
            void OnUpdated(IReadOnlyDictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> jobs)
            {
                updated++;
                observed = jobs;
            }

            ticker.LobbyJobsAvailableUpdated += OnUpdated;
            try
            {
                var names = new Dictionary<NetEntity, string> { [Station] = "Client Session Station" };
                var jobs = new Dictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>>
                {
                    [Station] = new()
                    {
                        ["ClientSessionGovJob"] = 2,
                        ["ClientSessionOpJob"] = null,
                    },
                };
                var weights = new Dictionary<NetEntity, ProtoId<JobWeightPrototype>?>
                {
                    [Station] = "ClientSessionStationWeights",
                };

                InvokePrivate(ticker, "UpdateJobsAvailable", new TickerJobsAvailableEvent(names, jobs, weights));
                names[Station] = "mutated after delivery";
                jobs.Clear();
                weights[Station] = null;

                Assert.Multiple(() =>
                {
                    Assert.That(updated, Is.EqualTo(1));
                    Assert.That(observed, Is.SameAs(ticker.JobsAvailable));
                    Assert.That(ticker.StationNames[Station], Is.EqualTo("Client Session Station"));
                    Assert.That(ticker.JobsAvailable[Station]["ClientSessionGovJob"], Is.EqualTo(2));
                    Assert.That(ticker.JobsAvailable[Station]["ClientSessionOpJob"], Is.Null);
                    Assert.That(ticker.JobWeightsByStation[Station]?.Id, Is.EqualTo("ClientSessionStationWeights"));
                });

                var roundUpdates = 0;
                ticker.RoundStatusUpdated += OnRoundUpdated;
                try
                {
                    InvokePrivate(ticker, "RoundStatus", new TickerRoundStatusEvent(
                        "Colony",
                        "Ship",
                        42,
                        17,
                        "Extended",
                        TimeSpan.FromSeconds(100),
                        TimeSpan.FromSeconds(10),
                        true));

                    SetPrivate(ticker, "_roundElapsedTimeReceivedAt", Client.Timing.RealTime - TimeSpan.FromSeconds(5));
                    Assert.Multiple(() =>
                    {
                        Assert.That(roundUpdates, Is.EqualTo(1));
                        Assert.That(ticker.CurrentMapName, Is.EqualTo("Colony"));
                        Assert.That(ticker.CurrentShipMapName, Is.EqualTo("Ship"));
                        Assert.That(ticker.RoundId, Is.EqualTo(42));
                        Assert.That(ticker.CurrentPlayerCount, Is.EqualTo(17));
                        Assert.That(ticker.CurrentGamemodeTitle, Is.EqualTo("Extended"));
                        Assert.That(ticker.RoundRealTimeDuration().TotalSeconds, Is.EqualTo(15).Within(0.2));
                    });

                    SetPrivate(ticker, "_roundElapsedTimeReceivedAt", Client.Timing.RealTime + TimeSpan.FromSeconds(20));
                    Assert.That(ticker.RoundRealTimeDuration(), Is.EqualTo(TimeSpan.Zero),
                        "a future receive timestamp must clamp the displayed elapsed time to zero");
                }
                finally
                {
                    ticker.RoundStatusUpdated -= OnRoundUpdated;
                }

                void OnRoundUpdated() => roundUpdates++;
            }
            finally
            {
                ticker.LobbyJobsAvailableUpdated -= OnUpdated;
            }
        });
    }

    [Test]
    public async Task LateJoinFiltersCmDepartmentsUsesStationWeightsAndCleansSubscriptions()
    {
        await Client.WaitAssertion(() =>
        {
            var ticker = Client.System<ClientGameTicker>();
            DeliverJobs(ticker, includeGov: true, includeOp: true);
            var baselineSubscribers = SubscriberCount(ticker, "LobbyJobsAvailableUpdated");
            var gui = new LateJoinGui("govfor");

            try
            {
                var buttons = GetPrivate<IDictionary>(gui, "_jobButtons");
                var stationButtons = (IDictionary) buttons[Station]!;
                var descendants = Descendants(GetPrivate<Control>(gui, "_base")).ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(stationButtons.Contains("ClientSessionGovJob"), Is.True);
                    Assert.That(stationButtons.Contains("ClientSessionOpJob"), Is.False,
                        "the faction constructor must filter even when both jobs are available");
                    Assert.That(descendants.OfType<Button>().Any(button =>
                            button.Text == Loc.GetString("crew-manifest-button-label")),
                        Is.True, "the no-station-entity crew manifest CVar must add its request button");
                    Assert.That(SubscriberCount(ticker, "LobbyJobsAvailableUpdated"),
                        Is.EqualTo(baselineSubscribers + 1));
                });

                Assert.That(JobUIComparer.TryCreate(Client.ProtoMan,
                    ticker.JobWeightsByStation[Station], out var comparer), Is.True);
                var gov = Client.ProtoMan.Index<JobPrototype>("ClientSessionGovJob");
                var op = Client.ProtoMan.Index<JobPrototype>("ClientSessionOpJob");
                Assert.That(comparer!.Compare(op, gov), Is.LessThan(0),
                    "LateJoin must use the station's typed weight profile rather than source order");
            }
            finally
            {
                gui.Dispose();
            }

            Assert.That(SubscriberCount(ticker, "LobbyJobsAvailableUpdated"), Is.EqualTo(baselineSubscribers),
                "disposing the CRT window must release the ticker callback");

            DeliverJobs(ticker, includeGov: false, includeOp: true);
            var empty = new LateJoinGui("govfor");
            try
            {
                var lists = GetPrivate<IEnumerable<ScrollContainer>>(empty, "_jobLists").ToArray();
                Assert.Multiple(() =>
                {
                    Assert.That(lists, Has.Length.EqualTo(1));
                    Assert.That(lists[0].VerticalExpand, Is.False,
                        "an empty faction section must collapse instead of splitting the window height");
                    Assert.That(Descendants(lists[0]).OfType<Label>().Any(label =>
                            label.Text == Loc.GetString("late-join-gui-no-departments-available")), Is.True);
                });
            }
            finally
            {
                empty.Dispose();
            }
        });
    }

    private static void DeliverJobs(ClientGameTicker ticker, bool includeGov, bool includeOp)
    {
        var jobs = new Dictionary<ProtoId<JobPrototype>, int?>();
        if (includeGov)
            jobs["ClientSessionGovJob"] = 1;
        if (includeOp)
            jobs["ClientSessionOpJob"] = 1;

        InvokePrivate(ticker, "UpdateJobsAvailable", new TickerJobsAvailableEvent(
            new Dictionary<NetEntity, string> { [Station] = "Client Session Station" },
            new Dictionary<NetEntity, Dictionary<ProtoId<JobPrototype>, int?>> { [Station] = jobs },
            new Dictionary<NetEntity, ProtoId<JobWeightPrototype>?>
            {
                [Station] = "ClientSessionStationWeights",
            }));
    }

    private static void InvokePrivate(ClientGameTicker ticker, string method, object argument)
    {
        typeof(ClientGameTicker)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(ticker, [argument]);
    }

    private static void SetPrivate(ClientGameTicker ticker, string field, object value)
    {
        typeof(ClientGameTicker)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(ticker, value);
    }

    private static int SubscriberCount(ClientGameTicker ticker, string eventName)
    {
        return ((Delegate?) typeof(ClientGameTicker)
            .GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(ticker))?.GetInvocationList().Length ?? 0;
    }

    private static T GetPrivate<T>(LateJoinGui gui, string field)
    {
        return (T) typeof(LateJoinGui)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(gui)!;
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
