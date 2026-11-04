using Content.Server.GameTicking;
using Content.Shared.Follower;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Follower;

[TestFixture]
[TestOf(typeof(FollowerSystem))]
public sealed class FollowerRoundRestartTest
{
    [Test]
    public async Task RestartWithFollowerOnTerminatingMapDoesNotLogCleanupFailure()
    {
        var (server, logHandler) = await PoolManager.GenerateServer(new PoolSettings
        {
            DummyTicker = false,
        }, TestContext.Out);

        try
        {
            logHandler.FailureLevel = LogLevel.Warning;
            logHandler.JudgeLog += (sawmill, message) =>
                sawmill != "system.transform" ||
                !message.RenderMessage().Contains("Failed to attach entity to map or grid");

            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                var followerSystem = server.System<FollowerSystem>();

                mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                var coordinates = new EntityCoordinates(grid, 0, 0);
                var followed = server.EntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
                var follower = server.EntMan.SpawnEntity(GameTicker.ObserverPrototypeName, coordinates);
                followerSystem.StartFollowingEntity(follower, followed);

                server.System<GameTicker>().RestartRound();
            });

            Assert.That(logHandler.FailingLogs, Is.Empty);
        }
        finally
        {
            server.Dispose();
        }
    }
}
