using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Maps;

namespace Content.IntegrationTests.Tests.Performance;

// Regression coverage for failures reproduced during the independent audit.
[TestFixture, NonParallelizable]
public sealed class IndependentPerformanceAuditTest : GameTest
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test, Explicit("Independent audit: measure the cache capacity boundary in Release.")]
    public async Task MeasureSparseAnchoredQueriesWithoutRetention()
    {
        var map = await Pair.CreateTestMap();
        var observations = new List<string>();
        await Server.WaitAssertion(() =>
        {
            var cache = Server.System<AnchoredTileCacheSystem>();
            var maps = Server.System<SharedMapSystem>();
            var clear = typeof(AnchoredTileCacheSystem).GetMethod("ClearTiles", Private)!;
            for (var round = 0; round < 5; round++)
            foreach (var count in round % 2 == 0 ? new[] { 4096, 4097 } : new[] { 4097, 4096 })
            {
                clear.Invoke(cache, null);
                // Empty cells must never consume the snapshot cache.
                for (var i = 0; i < count; i++) cache.Get(map.Grid, new Vector2i(i + 10, 10));
                var total = 0;
                double Direct()
                {
                    var start = Stopwatch.GetTimestamp();
                    for (var repeat = 0; repeat < 10; repeat++)
                    for (var i = 0; i < count; i++)
                    {
                        var query = maps.GetAnchoredEntitiesEnumerator(map.Grid, map.Grid.Comp, new Vector2i(i + 10, 10));
                        while (query.MoveNext(out _)) total++;
                    }
                    return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                }
                // Warm the direct path too; alternate which measured path runs first.
                var direct = Direct();
                if (round % 2 == 0) direct = Direct();
                var misses = cache.CacheMisses;
                var allocated = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                for (var repeat = 0; repeat < 10; repeat++)
                for (var i = 0; i < count; i++) total += cache.Get(map.Grid, new Vector2i(i + 10, 10)).Length;
                var cached = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                var bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                var refreshes = cache.CacheMisses - misses;
                if (round % 2 != 0) direct = Direct();
                Assert.That(total, Is.Zero);
                Assert.That(refreshes, Is.EqualTo(count * 10L));
                Assert.That(cache.CachedTileCount, Is.Zero);
                Assert.That(bytes, Is.Zero);
                observations.Add($"AUDIT cache round={round} cells={count} lookups={count * 10} misses={refreshes} cachedMs={cached:F4} directMs={direct:F4} allocatedBytes={bytes}");
            }
        });
        // Server callbacks can inherit a pooled server's older NUnit context.
        foreach (var observation in observations) TestContext.Out.WriteLine(observation);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ExplosionPreparationKeepsGeometryAcrossRealTicks(bool moveGrid)
    {
        var map = await Pair.CreateTestMap();
        IEnumerator steps = null;
        object state = null;
        object[] args = null;
        string before = null;
        var type = typeof(ExplosionSystem);
        var sync = type.GetMethod("GetExplosionTiles", Private)!;
        var stateType = type.GetNestedType("ExplosionPreparation", BindingFlags.NonPublic)!;
        await Server.WaitAssertion(() =>
        {
            var system = Server.System<ExplosionSystem>();
            var maps = Server.System<SharedMapSystem>();
            for (var x = -5; x <= 5; x++)
            for (var y = -5; y <= 5; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            var epicenter = Server.System<SharedTransformSystem>().ToMapCoordinates(map.GridCoords);
            args = new object[] { epicenter, "RMC", 50000f, 6f, 100f };
            before = Describe(sync.Invoke(system, args)!);
            state = Activator.CreateInstance(stateType, true)!;
            steps = ((IEnumerable) type.GetMethod("PrepareExplosionTiles", Private)!
                .Invoke(system, args.Append(state).ToArray())!).GetEnumerator();
            Assert.That(steps.MoveNext(), Is.True);
        });
        try
        {
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                var system = Server.System<ExplosionSystem>();
                var maps = Server.System<SharedMapSystem>();
                maps.SetTile(map.Grid, new Vector2i(5, 0), Tile.Empty);
                var wall = SEntMan.SpawnEntity("CMWallRock", new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f)));
                // Remove and replace a blocker to exercise tolerance-cache slot recycling.
                SEntMan.DeleteEntity(wall);
                SEntMan.SpawnEntity("CMWallRock", new EntityCoordinates(map.Grid, new Vector2(2.5f, 0.5f)));
                if (moveGrid)
                    Server.System<SharedTransformSystem>().SetLocalPosition(map.Grid, new Vector2(50, 0));
            });
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                var system = Server.System<ExplosionSystem>();
                while (steps.MoveNext()) { }
                var resumed = Describe(stateType.GetField("Result")!.GetValue(state)!);
                var after = Describe(sync.Invoke(system, args)!);
                Assert.That(resumed, Is.EqualTo(before), "All preparation slices must use the starting geometry.");
                Assert.That(after, Is.Not.EqualTo(before), "The edits must actually change the next blast.");
            });
        }
        finally { await Server.WaitPost(() => (steps as IDisposable)?.Dispose()); }
    }

    private static string Describe(object result)
    {
        var value = ((int Area, List<float> Intensities, ExplosionSpaceTileFlood? Space,
            Dictionary<EntityUid, ExplosionGridTileFlood> Grids, Matrix3x2 Matrix)) result;
        string Tiles(ExplosionTileFlood flood) => string.Join(";", flood.TileLists.OrderBy(e => e.Key)
            .Select(e => $"{e.Key}:{string.Join(',', e.Value)}"));
        return $"{value.Area}|{string.Join(',', value.Intensities)}|{value.Matrix}|" +
               (value.Space == null ? "" : Tiles(value.Space)) + "|" +
               string.Join("|", value.Grids.OrderBy(e => e.Key.Id).Select(e => $"{e.Key}:{Tiles(e.Value)}"));
    }
}
