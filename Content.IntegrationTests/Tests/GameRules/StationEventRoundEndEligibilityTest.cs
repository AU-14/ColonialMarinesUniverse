using Content.Server.RoundEnd;
using Content.Server.StationEvents;

namespace Content.IntegrationTests.Tests.GameRules;

[TestFixture]
[TestOf(typeof(EventManagerSystem))]
public sealed class StationEventRoundEndEligibilityTest
{
    private static readonly string[] RestrictedEvents =
    {
        "SleeperAgents",
        "ZombieOutbreak",
        "ClosetSkeleton",
        "DragonSpawn",
        "NinjaSpawn",
        "ParadoxCloneSpawn",
        "RevenantSpawn",
        "WizardSpawn",
        "LoneOpsSpawn",
        "DerelictCyborgSpawn",
        "KingRatMigration",
        "UnknownShuttleCargoLost",
    };

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
            var allEvents = eventManager.AllEvents()
                .ToDictionary(entry => entry.Key.ID, entry => entry.Value);

            Assert.Multiple(() =>
            {
                foreach (var eventId in RestrictedEvents)
                    Assert.That(allEvents[eventId].OccursDuringRoundEnd, Is.False, eventId);
            });

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

            var availableIds = available.Keys.Select(prototype => prototype.ID).ToHashSet();
            Assert.Multiple(() =>
            {
                foreach (var eventId in RestrictedEvents)
                    Assert.That(availableIds, Does.Contain(eventId), eventId);
            });

            roundEnd.CancelRoundEndCountdown(checkCooldown: false);
            roundEnd.DefaultCooldownDuration = TimeSpan.FromSeconds(30);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LockedEvacBlocksRestrictedEvents()
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
            var normallyAvailable = eventManager.AvailableEvents(
                    ignoreEarliestStart: true,
                    playerCountOverride: int.MaxValue)
                .Keys.Select(prototype => prototype.ID)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                foreach (var eventId in RestrictedEvents)
                    Assert.That(normallyAvailable, Does.Contain(eventId), eventId);
            });

            roundEnd.DefaultCooldownDuration = TimeSpan.FromSeconds(30);
            roundEnd.RequestRoundEnd(TimeSpan.FromMinutes(1));

            Assert.Multiple(() =>
            {
                Assert.That(roundEnd.IsRoundEndRequested(), Is.True);
                Assert.That(roundEnd.CanCallOrRecall(), Is.False);
            });

            var duringLockedEvac = eventManager.AvailableEvents(
                    ignoreEarliestStart: true,
                    playerCountOverride: int.MaxValue)
                .Keys.Select(prototype => prototype.ID)
                .ToHashSet();

            Assert.Multiple(() =>
            {
                foreach (var eventId in RestrictedEvents)
                    Assert.That(duringLockedEvac, Does.Not.Contain(eventId), eventId);
            });

            roundEnd.CancelRoundEndCountdown(checkCooldown: false);
            roundEnd.DefaultCooldownDuration = TimeSpan.FromSeconds(30);
        });

        await pair.CleanReturnAsync();
    }
}
