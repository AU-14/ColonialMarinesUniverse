using Content.Server.RoundEnd;
using Content.Server.StationEvents;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[TestOf(typeof(EventManagerSystem))]
public sealed class StationEventRoundEndEligibilityTest
{
    [Test]
    public async Task RecallableEvacKeepsRestrictedEventsEligible()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var eventManager = server.System<EventManagerSystem>();
        var roundEnd = server.System<RoundEndSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(
                eventManager.AllEvents().Single(entry => entry.Key.ID == "SleeperAgents")
                    .Value.OccursDuringRoundEnd,
                Is.False);

            roundEnd.DefaultCooldownDuration = TimeSpan.Zero;
            roundEnd.RequestRoundEnd(TimeSpan.FromMinutes(1));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(roundEnd.IsRoundEndRequested(), Is.True);
                Assert.That(roundEnd.CanCallOrRecall(), Is.True);
            });

            var available = eventManager.AvailableEvents(
                ignoreEarliestStart: true,
                playerCountOverride: int.MaxValue);

            Assert.That(
                available.Keys.Select(prototype => prototype.ID),
                Does.Contain("SleeperAgents"));

            roundEnd.CancelRoundEndCountdown(checkCooldown: false);
            roundEnd.DefaultCooldownDuration = TimeSpan.FromSeconds(30);
        });

        await pair.CleanReturnAsync();
    }
}
