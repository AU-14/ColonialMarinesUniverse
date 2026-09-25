using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUStairPreviewRangeTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task PreviewOpensAndClosesAtOnePointTwoTiles(bool separateGrid)
    {
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid network = default;
        EntityUid viewer = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            lower = maps.CreateMap(runMapInit: true);
            upper = maps.CreateMap(runMapInit: true);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(lower);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -3; x <= 3; x++)
            for (var y = -3; y <= 3; y++)
                maps.SetTile(lower, grid, new Vector2i(x, y), floor);
            var levels = Server.System<CMUZLevelsSystem>();
            network = levels.CreateZNetwork().Owner;
            Assert.That(levels.TryAddMapsIntoZNetwork((network, SComp<CMUZLevelsNetworkComponent>(network)),
                new() { [lower] = 0, [upper] = 1 }), Is.True);
            var stairGrid = lower;
            if (separateGrid)
            {
                var platform = maps.CreateGridEntity(SComp<MapComponent>(lower).MapId);
                maps.SetTile(platform, platform.Comp, Vector2i.Zero, floor);
                stairGrid = platform.Owner;
            }
            var stair = SEntMan.SpawnEntity("CMUMultiZStairs", new EntityCoordinates(stairGrid, 0.5f, 0.5f));
            Assert.That(SComp<CMUZLevelHighGroundComponent>(stair).PreviewRange, Is.EqualTo(1.2f));
            viewer = SEntMan.SpawnEntity(null, new EntityCoordinates(lower, 0.5f, -0.8f));
            SEntMan.EnsureComponent<EyeComponent>(viewer);
            Server.System<ViewSubscriberSystem>().AddViewSubscriber(viewer, ServerSession!);
        });
        foreach (var distance in new[] { 1.21f, 1.19f, 1.21f, 1.19f })
        {
            await Server.WaitAssertion(() =>
            {
                Server.System<SharedTransformSystem>().SetCoordinates(viewer,
                    new EntityCoordinates(lower, 0.5f, 0.5f - distance));
                Server.System<CMUZLevelsSystem>().RefreshZLevelViewer(viewer);
            });
            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() => Assert.That(SComp<CMUZLevelViewerComponent>(viewer).StairPreviewUp,
                Is.EqualTo(distance <= 1.2f), $"Preview at {distance} tiles"));
        }
        await Server.WaitPost(() => Server.System<ViewSubscriberSystem>().RemoveViewSubscriber(viewer, ServerSession!));
        await Pair.DeleteEntityTreeLeafFirst(lower);
        await Pair.DeleteEntityTreeLeafFirst(upper);
        await Pair.DeleteEntityTreeLeafFirst(network);
    }
}
