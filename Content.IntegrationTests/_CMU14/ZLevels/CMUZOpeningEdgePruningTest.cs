using System.Diagnostics;
using System.Globalization;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZOpeningEdgePruningTest : GameTest
{
    [TestCase(4, 1, 0)]
    [TestCase(8, 1, 0)]
    [TestCase(4, 2, 45)]
    [TestCase(8, 2, 45)]
    public Task EdgeQueriesMatchFullEnumerationOnMovedGrids(int chunkSize, int tileSize, int rotation)
    {
        return WithGrid((mapId, grid, maps, transform, tiles) =>
        {
            var floor = new Tile(tiles["Plating"].TileId);
            var lattice = new Tile(tiles["Lattice"].TileId);
            var random = new Random(20260927);
            for (var x = -16; x < 16; x++)
            for (var y = -16; y < 16; y++)
            {
                // A full negative chunk, sparse openings, and a solid positive chunk.
                var open = x >= -8 && x < 0 && y >= -8 && y < 0 || random.Next(4) == 0;
                if (x >= 8 && y >= 8 || x == 4 && y == 4)
                    open = false;
                maps.SetTile(grid, new Vector2i(x, y), open ? lattice : floor);
            }

            transform.SetLocalRotation(grid.Owner, Angle.FromDegrees(rotation));
            var cache = new CMUZLevelOpeningCache(chunkSize);
            foreach (var shift in new[] { Vector2.Zero, new Vector2(5f, -2f) })
            {
                transform.SetLocalPosition(grid.Owner, shift);
                foreach (var (localSource, localRadius) in new[]
                         {
                             (new Vector2(-3.5f, -3.5f), 7.25f),
                             (new Vector2(4.5f, 4.5f), 7.25f),
                             (new Vector2(-8.25f, -0.25f), 7.25f),
                             (new Vector2(10.5f, 10.5f), 2f),
                         })
                {
                    var source = Vector2.Transform(localSource * tileSize, transform.GetWorldMatrix(grid.Owner));
                    var expected = AssertMatchesFullEnumeration(cache, mapId, grid, source,
                        localRadius * tileSize, maps, transform, tiles);
                    if (localSource.X == 10.5f)
                        Assert.That(expected, Is.Empty, "The solid chunk must not acquire a source-tile opening.");
                    else
                        Assert.That(expected, Is.Not.Empty, "The parity query must exercise real opening edges.");
                }
            }
        }, tileSize);
    }

    [TestCase(4, -4)]
    [TestCase(8, -4)]
    [TestCase(8, 3)]
    public Task MissingSourceCenterSurvivesWithoutInventingSolidOpenings(int chunkSize, int coordinate)
    {
        return WithGrid((mapId, grid, maps, transform, tiles) =>
        {
            var target = new Vector2i(coordinate, coordinate);
            var source = new Vector2(coordinate + 0.5f, coordinate + 0.5f);
            var cache = new CMUZLevelOpeningCache(chunkSize);
            Assert.That(maps.TryGetTileRef(grid.Owner, grid.Comp, target, out _), Is.False);

            // A missing chunk is open, but is not an existing opening beneath the source.
            // The edge predicate deliberately accepts its center without a solid neighbor.
            var missing = AssertMatchesFullEnumeration(cache, mapId, grid, source, 0.1f,
                maps, transform, tiles);
            Assert.That(missing.Select(portal => portal.Tile), Is.EqualTo(new[] { target }));
            var nearCenter = AssertMatchesFullEnumeration(cache, mapId, grid,
                source + new Vector2(0.02f, 0f), 0.1f, maps, transform, tiles);
            Assert.That(nearCenter.Select(portal => portal.Tile), Is.EqualTo(new[] { target }));

            var floor = new Tile(tiles["Plating"].TileId);
            maps.SetTile(grid, target, floor);
            cache.InvalidateTiles(grid, new[] { new TileChangedEntry(floor, default, Vector2i.Zero, target) });
            var solid = AssertMatchesFullEnumeration(cache, mapId, grid, source, 0.1f,
                maps, transform, tiles);
            Assert.That(solid, Is.Empty,
                "Retaining the source bit must still intersect it with the actual opening mask.");
        }, mapGrid: true);
    }

    [TestCase(8388610f)]
    [TestCase(-8388610f)]
    public Task ExtremeSourcePositionsPreserveRoundedCenterCandidates(float coordinate)
    {
        return WithGrid((mapId, grid, maps, transform, tiles) =>
        {
            var cache = new CMUZLevelOpeningCache();
            var expected = AssertMatchesFullEnumeration(cache, mapId, grid, new Vector2(coordinate, 3.5f),
                0.1f, maps, transform, tiles);
            Assert.That(expected.Count, Is.GreaterThan(1),
                "Multiple integer tiles share this float center and must retain the near-center exception.");
        }, mapGrid: true);
    }

    [TestCase(4)]
    [TestCase(8)]
    public Task WarmEdgesObserveSameTickInteriorAndAdjacentChunkChanges(int chunkSize)
    {
        return WithGrid((mapId, grid, maps, transform, tiles) =>
        {
            var floor = new Tile(tiles["Plating"].TileId);
            var lattice = new Tile(tiles["Lattice"].TileId);
            for (var x = -16; x < 16; x++)
            for (var y = -16; y < 16; y++)
                maps.SetTile(grid, new Vector2i(x, y), lattice);

            var cache = new CMUZLevelOpeningCache(chunkSize);
            var tick = grid.Comp.LastTileModifiedTick;
            // The first candidate is inside a negative chunk; the second borders chunk zero.
            foreach (var target in new[] { new Vector2i(-3, -3), new Vector2i(-1, -3) })
            {
                var source = new Vector2(target.X + 0.5f, target.Y + 0.5f);
                var neighbor = target + new Vector2i(1, 0);
                var before = AssertMatchesFullEnumeration(cache, mapId, grid, source, 0.1f,
                    maps, transform, tiles);
                Assert.That(before, Is.Empty, "An opening surrounded by openings is not an edge.");

                maps.SetTile(grid, neighbor, floor);
                Assert.That(grid.Comp.LastTileModifiedTick, Is.EqualTo(tick));
                // In the boundary case, only the neighboring chunk is invalidated.
                cache.InvalidateTiles(grid, new[] { new TileChangedEntry(floor, lattice, Vector2i.Zero, neighbor) });
                var closed = AssertMatchesFullEnumeration(cache, mapId, grid, source, 0.1f,
                    maps, transform, tiles);
                Assert.That(closed.Select(portal => portal.Tile), Is.EqualTo(new[] { target }));

                maps.SetTile(grid, neighbor, lattice);
                Assert.That(grid.Comp.LastTileModifiedTick, Is.EqualTo(tick));
                cache.InvalidateTiles(grid, new[] { new TileChangedEntry(lattice, floor, Vector2i.Zero, neighbor) });
                var reopened = AssertMatchesFullEnumeration(cache, mapId, grid, source, 0.1f,
                    maps, transform, tiles);
                Assert.That(reopened, Is.Empty);
            }
        });
    }

    [TestCase(4, 2, 3)]
    [TestCase(8, 3, 2)]
    public Task NearestOpeningRetainsFirstCandidateForEqualDistances(int chunkSize, int expectedX, int expectedY)
    {
        return WithGrid((mapId, grid, maps, transform, tiles) =>
        {
            var floor = new Tile(tiles["Plating"].TileId);
            var lattice = new Tile(tiles["Lattice"].TileId);
            for (var x = 0; x < 8; x++)
            for (var y = 0; y < 8; y++)
                maps.SetTile(grid, new Vector2i(x, y), floor);
            maps.SetTile(grid, new Vector2i(2, 3), lattice);
            maps.SetTile(grid, new Vector2i(3, 2), lattice);

            var cache = new CMUZLevelOpeningCache(chunkSize);
            var source = new Vector2(3.5f, 3.5f);
            var expected = AssertMatchesFullEnumeration(cache, mapId, grid, source, 1.25f,
                maps, transform, tiles);
            Assert.That(expected, Has.Count.EqualTo(2));
            Assert.That(expected[0].Tile, Is.EqualTo(new Vector2i(expectedX, expectedY)));
            Assert.That(expected[0].Distance, Is.EqualTo(expected[1].Distance));

            var grids = new List<Entity<MapGridComponent>>();
            Assert.That(cache.TryFindNearestOpeningCenterNear(mapId, source, 1.25f, out var nearest,
                grids, maps, transform, tiles), Is.True);
            Assert.That(nearest, Is.EqualTo(expected[0].Center));
        });
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    [Explicit("Select this method explicitly for before/after Release edge-query measurements.")]
    public async Task WarmEdgeQueryMeasurement(bool fragmented, bool nearest)
    {
        var report = new List<string>();
        await WithGrid((mapId, grid, maps, transform, tiles) =>
        {
            var floor = new Tile(tiles["Plating"].TileId);
            var lattice = new Tile(tiles["Lattice"].TileId);
            for (var x = -24; x < 24; x++)
            for (var y = -24; y < 24; y++)
            {
                var open = fragmented
                    ? (x + y) % 2 == 0
                    : x >= -16 && x < 16 && y >= -16 && y < 16;
                maps.SetTile(grid, new Vector2i(x, y), open ? lattice : floor);
            }

            var cache = new CMUZLevelOpeningCache();
            var portals = new List<CMUZOpeningPortal>(4096);
            var grids = new List<Entity<MapGridComponent>>(16);
            var resultCount = 0L;
            void Query()
            {
                if (nearest)
                {
                    if (cache.TryFindNearestOpeningCenterNear(mapId, Vector2.Zero, 16f, out _,
                            grids, maps, transform, tiles))
                        resultCount++;
                }
                else
                {
                    portals.Clear();
                    cache.FindOpeningPortalsNear(mapId, Vector2.Zero, 16f, portals,
                        grids, maps, transform, tiles);
                    resultCount += portals.Count;
                }
            }

            for (var i = 0; i < 512; i++)
                Query();
            const int queriesPerBatch = 256;
            var times = new double[17];
            var allocations = new long[times.Length];
            for (var batch = 0; batch < times.Length; batch++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < queriesPerBatch; i++)
                    Query();
                times[batch] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocations[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            Assert.That(resultCount, Is.GreaterThan(0), "Both layouts must exercise real opening edges.");
            report.Add($"fragmented={fragmented} nearest={nearest} queriesPerBatch={queriesPerBatch} resultCount={resultCount}");
            report.Add("milliseconds=" + string.Join(",", times.Select(value => value.ToString("F4", CultureInfo.InvariantCulture))));
            report.Add("allocatedBytes=" + string.Join(",", allocations));
            Array.Sort(times);
            Array.Sort(allocations);
            report.Add("medianMilliseconds=" + times[times.Length / 2].ToString("F4", CultureInfo.InvariantCulture) +
                       " medianAllocatedBytes=" + allocations[allocations.Length / 2]);
        }, mapGrid: true);
        foreach (var line in report)
            TestContext.Out.WriteLine(line);
    }

    private static List<CMUZOpeningPortal> AssertMatchesFullEnumeration(
        CMUZLevelOpeningCache cache,
        MapId mapId,
        Entity<MapGridComponent> grid,
        Vector2 source,
        float radius,
        SharedMapSystem maps,
        SharedTransformSystem transform,
        ITileDefinitionManager tiles)
    {
        var grids = new List<Entity<MapGridComponent>>();
        var all = new List<CMUZOpeningPortal>();
        cache.FindOpeningPortalsNear(mapId, source, radius, all, grids, maps, transform, tiles, false);
        Assert.That(Matrix3x2.Invert(transform.GetWorldMatrix(grid.Owner), out var inverse), Is.True);
        var localSource = Vector2.Transform(source, inverse) / grid.Comp.TileSize;
        var sourceTile = new Vector2i((int) MathF.Floor(localSource.X), (int) MathF.Floor(localSource.Y));
        var inside = CMUZLevelOpeningCache.IsExistingOpeningTile(grid, sourceTile, maps, tiles);
        // The reference enumerates every opening and applies only the established live predicate.
        var expected = all.Where(portal => CMUZLevelOpeningCache.IsOpeningEdgeTile(
            grid, portal.Tile, localSource, inside, maps, tiles)).ToList();

        var actual = new List<CMUZOpeningPortal>();
        cache.FindOpeningPortalsNear(mapId, source, radius, actual, grids, maps, transform, tiles);
        Assert.That(actual, Is.EqualTo(expected), $"Ordered portals from {source}, radius {radius}");
        var centers = new List<(Vector2 Center, float Distance)>();
        cache.FindOpeningCentersNear(mapId, source, radius, centers, grids, maps, transform, tiles);
        Assert.That(centers, Is.EqualTo(expected.Select(portal => (portal.Center, portal.Distance))));
        AssertNearest(cache, mapId, source, radius, expected, grids, maps, transform, tiles, true);
        AssertNearest(cache, mapId, source, radius, all, grids, maps, transform, tiles, false);
        return expected;
    }

    private static void AssertNearest(
        CMUZLevelOpeningCache cache,
        MapId mapId,
        Vector2 source,
        float radius,
        List<CMUZOpeningPortal> expected,
        List<Entity<MapGridComponent>> grids,
        SharedMapSystem maps,
        SharedTransformSystem transform,
        ITileDefinitionManager tiles,
        bool edgeOnly)
    {
        var found = cache.TryFindNearestOpeningCenterNear(mapId, source, radius, out var nearest,
            grids, maps, transform, tiles, edgeOnly);
        Assert.That(found, Is.EqualTo(expected.Count > 0));
        if (!found)
            return;

        // OrderBy is stable, preserving the original candidate when squared distances tie.
        var closest = expected.OrderBy(portal => Vector2.DistanceSquared(source, portal.Center)).First();
        Assert.That(nearest, Is.EqualTo(closest.Center));
    }

    private async Task WithGrid(
        Action<MapId, Entity<MapGridComponent>, SharedMapSystem, SharedTransformSystem, ITileDefinitionManager> assertion,
        int tileSize = 1,
        bool mapGrid = false)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            try
            {
                Entity<MapGridComponent> grid = mapGrid
                    ? (mapUid, SEntMan.EnsureComponent<MapGridComponent>(mapUid))
                    : maps.CreateGridEntity(mapId);
                // Set the engine-owned serialized size before this fixture creates any tiles.
                typeof(MapGridComponent).GetProperty(nameof(MapGridComponent.TileSize))!
                    .SetValue(grid.Comp, (ushort) tileSize);
                assertion(mapId, grid, maps, transform, tiles);
            }
            finally
            {
                SEntMan.DeleteEntity(mapUid);
            }
        });
    }
}
