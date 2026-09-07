using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Content.Shared.CMU14.ZLevels.Core;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>Opt-in warm-cache measurement of the real shared aperture queries, without rendering.</summary>
[TestFixture, Explicit("Select this fixture explicitly for before/after Release measurements.")]
public sealed class CMUZOpeningQueryMeasurement
{
    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task OpeningQueries(bool fragmented, bool centers)
    {
        var report = new List<string>();
        await using var pair = await PoolManager.GetServerClient();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var transform = entities.System<SharedTransformSystem>();
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = entities.EnsureComponent<MapGridComponent>(mapUid);
            try
            {
                var floor = new Tile(tiles["Plating"].TileId);
                var transparent = new Tile(tiles["Lattice"].TileId);
                for (var x = -24; x < 24; x++)
                for (var y = -24; y < 24; y++)
                    maps.SetTile(mapUid, grid, new Vector2i(x, y), fragmented && (x + y) % 2 == 0 ? transparent : floor);

                var cache = new CMUZLevelOpeningCache();
                var bounds = new Box2(-16, -16, 16, 16);
                var outputBounds = new List<Box2>(4096);
                var outputCenters = new List<(Vector2 Center, float Distance)>(4096);
                var grids = new List<Entity<MapGridComponent>>(16);
                var count = 0L;
                void Query()
                {
                    if (centers)
                    {
                        outputCenters.Clear();
                        cache.FindOpeningCentersNear(mapId, Vector2.Zero, 16f, outputCenters, grids, maps, transform, tiles, edgeOnly: false);
                        count += outputCenters.Count;
                    }
                    else
                    {
                        outputBounds.Clear();
                        cache.TryFindOpeningBounds(mapId, bounds, outputBounds, out _, 4096, true, grids, maps, transform, tiles);
                        count += outputBounds.Count;
                    }
                }

                for (var i = 0; i < 512; i++)
                    Query();
                const int queries = 256;
                var times = new double[17];
                var allocations = new long[times.Length];
                for (var batch = 0; batch < times.Length; batch++)
                {
                    var before = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    for (var i = 0; i < queries; i++)
                        Query();
                    times[batch] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocations[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
                }

                Assert.That(count, fragmented ? Is.GreaterThan(0) : Is.EqualTo(0));
                report.Add($"fragmented={fragmented} centers={centers} queriesPerBatch={queries} resultCount={count}");
                report.Add("milliseconds=" + string.Join(",", times.Select(t => t.ToString("F4", CultureInfo.InvariantCulture))));
                report.Add("allocatedBytes=" + string.Join(",", allocations));
            }
            finally
            {
                entities.DeleteEntity(mapUid);
            }
        });
        foreach (var line in report)
            TestContext.Out.WriteLine(line);
        await pair.CleanReturnAsync();
    }
}
