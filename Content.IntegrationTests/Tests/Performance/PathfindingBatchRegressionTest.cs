#pragma warning disable RA0002 // Control dirty-chunk batches to cover both rebuild paths.

using System.Threading;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Physics;

namespace Content.IntegrationTests.Tests.Performance;

[TestFixture]
public sealed class PathfindingBatchRegressionTest : GameTest
{
    [TestCase(1)]
    [TestCase(4)]
    public async Task SingleChunkRebuildKeepsCrossChunkRoutesForSingleAndParallelRequests(int requests)
    {
        var map = await Pair.CreateTestMap();
        var paths = new List<Task<PathResultEvent>>();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var system = Server.System<PathfindingSystem>();
            for (var x = 0; x <= 20; x++)
            for (var y = 0; y < 3; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);

            var grid = SEntMan.GetComponent<GridPathfindingComponent>(map.Grid.Owner);
            grid.NextUpdate = TimeSpan.Zero;
            system.Update(0f);
            Assert.That(grid.DirtyChunks, Is.Empty);
            Assert.That(grid.Chunks.Count, Is.GreaterThan(1));

            // Rebuild only the first chunk, retaining its connections to the unchanged neighbors.
            grid.DirtyChunks.Add(Vector2i.Zero);
            grid.NextUpdate = TimeSpan.Zero;
            system.Update(0f);
            Assert.That(grid.DirtyChunks, Is.Empty);
            var start = new EntityCoordinates(map.Grid.Owner, new Vector2(0.5f, 1.5f));
            var end = new EntityCoordinates(map.Grid.Owner, new Vector2(19.5f, 1.5f));
            Assert.That(system.GetPoly(start), Is.Not.Null);
            Assert.That(system.GetPoly(end), Is.Not.Null);
            for (var i = 0; i < requests; i++)
                paths.Add(system.GetPath(start, end, 0.2f, (int) CollisionGroup.MobLayer,
                    (int) CollisionGroup.MobMask, CancellationToken.None));
        });

        await Pair.RunTicksSync(30);
        Assert.That(paths.All(path => path.IsCompletedSuccessfully), Is.True);
        var results = await Task.WhenAll(paths);
        Assert.That(results.Select(result => result.Result), Is.All.EqualTo(PathResult.Path));
    }
}
