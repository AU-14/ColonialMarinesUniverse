using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
[TestOf(typeof(SharedMindSystem))]
public sealed class VisitingMindCleanupTest
{
    [Test]
    public async Task DeletingVisitedEntityAfterMindDoesNotLogCleanupFailure()
    {
        var (server, logHandler) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            logHandler.FailureLevel = LogLevel.Error;
            logHandler.JudgeLog += (sawmill, message) =>
                sawmill != "system.mind" ||
                !message.RenderMessage().Contains("Can't resolve \"Content.Shared.Mind.MindComponent\"");

            await server.WaitPost(() =>
            {
                var mind = server.System<SharedMindSystem>().CreateMind(null);
                var visited = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                server.EntMan.EnsureComponent<VisitingMindComponent>(visited).MindId = mind;

                server.EntMan.DeleteEntity(mind);
                server.EntMan.DeleteEntity(visited);
            });

            Assert.That(logHandler.FailingLogs, Is.Empty);
        }
        finally
        {
            server.Dispose();
        }
    }
}
