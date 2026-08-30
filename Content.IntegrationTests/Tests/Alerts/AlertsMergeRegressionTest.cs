using Content.Client.Alerts;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Tracker.Xeno;
using Content.Shared.Alert;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests.Alerts;

[TestFixture]
[TestOf(typeof(AlertsSystem))]
public sealed class AlertsMergeRegressionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: MobHuman
  id: AlertsMergePlayer
  components:
  - type: AlertsMergeProbe
";

    [Test]
    public async Task DynamicStateAndNormalAltClicksRemainExclusiveAndValidated()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        EntityUid player = default;
        NetEntity playerNet = default;

        try
        {
            await Server.WaitPost(() =>
            {
                _ = Server.System<AlertsMergeProbeSystem>();
                player = SEntMan.SpawnEntity("AlertsMergePlayer", map.GridCoords);
                playerNet = SEntMan.GetNetEntity(player);
                Server.PlayerMan.SetAttachedEntity(session, player);
            });
            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                var alerts = Server.EntMan.System<AlertsSystem>();
                alerts.ShowAlert(player, "HiveTracker", severity: 1, dynamicMessage: "first message");
                var key = Server.ProtoMan.Index<AlertPrototype>("HiveTracker").AlertKey;
                Assert.That(alerts.TryGetAlertState(player, key, out var state), Is.True);
                Assert.That(state.DynamicMessage, Is.EqualTo("first message"));

                alerts.ShowAlert(player, "HiveTracker", severity: 1, dynamicMessage: "replacement message");
                Assert.That(alerts.TryGetAlertState(player, key, out state), Is.True);
                Assert.That(state.DynamicMessage, Is.EqualTo("replacement message"),
                    "ShowAlert must replace a same-key state when only its dynamic message changes");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                _ = Client.System<AlertsMergeProbeSystem>();
                var clientPlayer = CEntMan.GetEntity(playerNet);
                var alerts = Client.System<ClientAlertsSystem>();
                var key = Client.ProtoMan.Index<AlertPrototype>("HiveTracker").AlertKey;
                Assert.That(alerts.TryGetAlertState(clientPlayer, key, out var state), Is.True);
                Assert.That(state.DynamicMessage, Is.EqualTo("replacement message"),
                    "the replacement dynamic message must be present in the owner-only client state");
            });

            await Server.WaitAssertion(() =>
            {
                var alerts = Server.EntMan.System<AlertsSystem>();
                alerts.ClearAlert(player, "HiveTracker");
                SEntMan.GetComponent<AlertsMergeProbeComponent>(player).Reset();
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientPlayer = CEntMan.GetEntity(playerNet);
                var alerts = Client.System<ClientAlertsSystem>();
                var probe = CEntMan.GetComponent<AlertsMergeProbeComponent>(clientPlayer);
                probe.Reset();
                var key = Client.ProtoMan.Index<AlertPrototype>("HiveTracker").AlertKey;
                Assert.That(alerts.TryGetAlertState(clientPlayer, key, out _), Is.False);

                alerts.AlertClicked("HiveTracker");
                alerts.AlertClickedAlt("HiveTracker");
                alerts.AlertClicked("AlertsMergeUnknown");
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Clicks, Is.Zero);
                    Assert.That(probe.AltClicks, Is.Zero);
                });
            });
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                var probe = SEntMan.GetComponent<AlertsMergeProbeComponent>(player);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Clicks, Is.Zero,
                        "normal clicks for hidden or unknown alerts must be rejected before activation");
                    Assert.That(probe.AltClicks, Is.Zero,
                        "alt clicks must validate that the alert is shown before activation");
                });

                Server.EntMan.System<AlertsSystem>()
                    .ShowAlert(player, "HiveTracker", severity: 1, dynamicMessage: "clickable");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                var clientPlayer = CEntMan.GetEntity(playerNet);
                var alerts = Client.System<ClientAlertsSystem>();
                var probe = CEntMan.GetComponent<AlertsMergeProbeComponent>(clientPlayer);
                probe.Reset();

                alerts.AlertClicked("HiveTracker");
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Clicks, Is.EqualTo(1));
                    Assert.That(probe.AltClicks, Is.Zero,
                        "normal activation must raise only the configured ClickEvent");
                    Assert.That(probe.FirstPredictionClicks, Is.EqualTo(1),
                        "the handled normal event must execute on the first predicted client pass");
                });
            });
            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                var probe = SEntMan.GetComponent<AlertsMergeProbeComponent>(player);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Clicks, Is.EqualTo(1));
                    Assert.That(probe.AltClicks, Is.Zero);
                });
            });

            await Client.WaitAssertion(() =>
            {
                var clientPlayer = CEntMan.GetEntity(playerNet);
                var alerts = Client.System<ClientAlertsSystem>();
                var probe = CEntMan.GetComponent<AlertsMergeProbeComponent>(clientPlayer);

                alerts.AlertClickedAlt("HiveTracker");
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Clicks, Is.EqualTo(1),
                        "alt validation must not invoke the normal ClickEvent");
                    Assert.That(probe.AltClicks, Is.EqualTo(1));
                });
            });
            await Pair.RunTicksSync(3);

            await Server.WaitAssertion(() =>
            {
                var probe = SEntMan.GetComponent<AlertsMergeProbeComponent>(player);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.Clicks, Is.EqualTo(1));
                    Assert.That(probe.AltClicks, Is.EqualTo(1),
                        "validated alt activation must raise only the configured AltClickEvent");
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                if (player.Valid && SEntMan.EntityExists(player))
                    SEntMan.DeleteEntity(player);
            });
        }
    }
}

[RegisterComponent]
public sealed partial class AlertsMergeProbeComponent : Component
{
    public int Clicks;
    public int AltClicks;
    public int FirstPredictionClicks;

    public void Reset()
    {
        Clicks = 0;
        AltClicks = 0;
        FirstPredictionClicks = 0;
    }
}

public sealed partial class AlertsMergeProbeSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlertsMergeProbeComponent, HiveTrackerClickedAlertEvent>(OnClicked);
        SubscribeLocalEvent<AlertsMergeProbeComponent, HiveTrackerAltClickedAlertEvent>(OnAltClicked);
    }

    private void OnClicked(Entity<AlertsMergeProbeComponent> entity, ref HiveTrackerClickedAlertEvent args)
    {
        entity.Comp.Clicks++;
        if (_timing.IsFirstTimePredicted)
            entity.Comp.FirstPredictionClicks++;
        args.Handled = true;
    }

    private static void OnAltClicked(
        Entity<AlertsMergeProbeComponent> entity,
        ref HiveTrackerAltClickedAlertEvent args)
    {
        entity.Comp.AltClicks++;
        args.Handled = true;
    }
}
