using Content.Shared._RMC14.Xenonids.Construction.Nest;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Map;

namespace Content.IntegrationTests._RMC14.Construction;

[TestFixture]
[TestOf(typeof(XenoNestSystem))]
public sealed class XenoNestMapTeardownTest
{
    [Test]
    public async Task DeletingMapWithNestedEntityDoesNotTryToReparentIt()
    {
        var (server, logHandler) = await PoolManager.GenerateServer(new PoolSettings(), TestContext.Out);

        try
        {
            logHandler.FailureLevel = LogLevel.Warning;
            logHandler.JudgeLog += (sawmill, message) =>
                sawmill != "system.transform" ||
                !message.RenderMessage().Contains("Failed to attach entity to map or grid");

            await server.WaitPost(() =>
            {
                var mapSystem = server.System<SharedMapSystem>();
                var map = mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                var nest = server.EntMan.SpawnEntity(null, new EntityCoordinates(grid, 0, 0));
                var nested = server.EntMan.SpawnEntity(null, new EntityCoordinates(nest, 0, 0));
                var nestComponent = server.EntMan.AddComponent<XenoNestComponent>(nest);
#pragma warning disable RA0002 // Integration regression intentionally seeds an active nest relationship.
                nestComponent.Nested = nested;
#pragma warning restore RA0002

                server.EntMan.DeleteEntity(map);
            });

            Assert.That(logHandler.FailingLogs, Is.Empty);
        }
        finally
        {
            server.Dispose();
        }
    }
}
