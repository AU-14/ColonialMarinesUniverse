using System.Numerics;
using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZOpeningCacheLifecycleTest : GameTest
{
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(float.NegativeInfinity)]
    public async Task OpeningEdgesRejectNonFiniteSourcePositions(float coordinate)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = maps.CreateGridEntity(mapId);
            try
            {
                Fill(maps, grid, new Tile(tiles["Plating"].TileId));
                maps.SetTile(grid, Vector2i.Zero, new Tile(tiles["Lattice"].TileId));
                foreach (var position in new[] { new Vector2(coordinate, 0.5f), new Vector2(0.5f, coordinate) })
                {
                    foreach (var inside in new[] { false, true })
                    {
                        Assert.That(CMUZLevelOpeningCache.IsOpeningEdgeTile(grid, Vector2i.Zero, position,
                            inside, maps, tiles), Is.False);
                    }
                }

                Assert.That(CMUZLevelOpeningCache.IsOpeningEdgeTile(grid, Vector2i.Zero, new Vector2(1.5f, 0.5f),
                    false, maps, tiles), Is.True);
            }
            finally
            {
                SEntMan.DeleteEntity(mapUid);
            }
        });
    }

    [Test]
    public async Task ClientOwnerInvalidatesMultipleTileChangesWithinOneTick()
    {
        await Client.WaitAssertion(() =>
        {
            var maps = CEntMan.System<SharedMapSystem>();
            var tiles = Client.ResolveDependency<ITileDefinitionManager>();
            var cache = CEntMan.System<CMUClientZLevelsSystem>().OpeningCache;
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = maps.CreateGridEntity(mapId);
            try
            {
                var floor = new Tile(tiles["Plating"].TileId);
                var lattice = new Tile(tiles["Lattice"].TileId);
                var target = new Vector2i(-2, 3);
                Fill(maps, grid, floor);
                var tick = grid.Comp.LastTileModifiedTick;
                Assert.That(cache.HasOpeningInTileBounds(grid, target, target, maps, tiles), Is.False);

                // Exercise the production TileChangedEvent subscriber, without forwarding invalidation.
                maps.SetTile(grid, target, lattice);
                Assert.That(grid.Comp.LastTileModifiedTick, Is.EqualTo(tick));
                Assert.That(cache.HasOpeningInTileBounds(grid, target, target, maps, tiles), Is.True);
                maps.SetTile(grid, target, floor);
                Assert.That(grid.Comp.LastTileModifiedTick, Is.EqualTo(tick));
                Assert.That(cache.HasOpeningInTileBounds(grid, target, target, maps, tiles), Is.False);
            }
            finally
            {
                CEntMan.DeleteEntity(mapUid);
            }
        });
    }

    [TestCase(4)]
    [TestCase(8)]
    public async Task CachedQueriesFollowSameTickTileChangesAndCleanup(int chunkSize)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = maps.CreateGridEntity(mapId);
            try
            {
                var floor = new Tile(tiles["Plating"].TileId);
                var lattice = new Tile(tiles["Lattice"].TileId);
                Fill(maps, grid, floor);
                var cache = new CMUZLevelOpeningCache(chunkSize);
                var target = new Vector2i(-2, 3);
                Assert.That(cache.HasOpeningInTileBounds(grid, new Vector2i(-4, -4), new Vector2i(4, 4), maps, tiles), Is.False);
                maps.SetTile(grid, target, lattice);
                cache.InvalidateTiles(grid, new[] { new TileChangedEntry(lattice, floor, Vector2i.Zero, target) });
                Assert.That(cache.HasOpeningInTileBounds(grid, new Vector2i(4, 4), new Vector2i(-4, -4), maps, tiles), Is.True);

                var portals = new List<CMUZOpeningPortal>();
                var grids = new List<Entity<MapGridComponent>>();
                cache.FindOpeningPortalsNear(mapId, Vector2.Zero, 5f, portals, grids, maps, transform, tiles, false);
                Assert.That(portals, Has.Count.EqualTo(1));
                Assert.That(portals[0].Grid, Is.EqualTo(grid.Owner));
                Assert.That(portals[0].Tile, Is.EqualTo(target));
                Assert.That(portals[0].Center, Is.EqualTo(new Vector2(-1.5f, 3.5f)));
                Assert.That(cache.TryFindNearestOpeningCenterNear(mapId, Vector2.Zero, 5f, out var nearest,
                    grids, maps, transform, tiles, false), Is.True);
                Assert.That(nearest, Is.EqualTo(portals[0].Center));

                maps.SetTile(grid, target, floor);
                cache.InvalidateTiles(grid, new[] { new TileChangedEntry(floor, lattice, Vector2i.Zero, target) });
                portals.Clear();
                cache.FindOpeningPortalsNear(mapId, Vector2.Zero, 5f, portals, grids, maps, transform, tiles, false);
                Assert.That(portals, Is.Empty);

                maps.SetTile(grid, target, lattice);
                cache.RemoveGrid(grid.Owner);
                Assert.That(cache.HasOpeningInTileBounds(grid, target, target, maps, tiles), Is.True);
                maps.SetTile(grid, target, floor);
                cache.Clear();
                Assert.That(cache.HasOpeningInTileBounds(grid, target, target, maps, tiles), Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(mapUid);
            }
        });
    }

    [TestCase(4)]
    [TestCase(8)]
    public async Task ClippedQueriesMatchPlacedOpenTilesInOrder(int chunkSize)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = maps.CreateGridEntity(mapId);
            try
            {
                var floor = new Tile(tiles["Plating"].TileId);
                var lattice = new Tile(tiles["Lattice"].TileId);
                var random = new Random(20260905);
                var openTiles = new HashSet<Vector2i>();
                for (var x = -24; x < 24; x++)
                for (var y = -24; y < 24; y++)
                {
                    var open = random.Next(4) == 0;
                    // Include a full mask and an empty mask, alongside the sparse random tiles.
                    if (x >= -8 && x < 0 && y >= -8 && y < 0)
                        open = true;
                    else if (x >= 8 && x < 16 && y >= 8 && y < 16)
                        open = false;

                    var position = new Vector2i(x, y);
                    maps.SetTile(grid, position, open ? lattice : floor);
                    if (open)
                        openTiles.Add(position);
                }

                var cache = new CMUZLevelOpeningCache(chunkSize);
                // Single-tile queries exercise every clipped row and column, including bit 63
                // and negative chunk boundaries, against the tiles placed above.
                for (var x = -24; x < 24; x++)
                for (var y = -24; y < 24; y++)
                {
                    var position = new Vector2i(x, y);
                    Assert.That(cache.HasOpeningInTileBounds(grid, position, position, maps, tiles),
                        Is.EqualTo(openTiles.Contains(position)), $"Single tile {position}");
                }

                var queries = new List<Box2>
                {
                    new(-6.75f, -6.75f, -1.25f, -1.25f), // Exactly the full negative eight-tile chunk.
                    new(9.25f, 9.25f, 14.75f, 14.75f), // Exactly the empty eight-tile chunk.
                    new(1.25f, 1.25f, 6.75f, 6.75f),
                    new(-8.25f, -8.25f, -7.75f, -7.75f),
                    new(-0.25f, -0.25f, 0.25f, 0.25f),
                    new(6.25f, 0.25f, 8.75f, 0.75f),
                    new(0.25f, 6.25f, 0.75f, 8.75f),
                };
                for (var i = 0; i < 64; i++)
                {
                    var x = random.Next(-18, 13);
                    var y = random.Next(-18, 13);
                    queries.Add(new Box2(x + 0.25f, y + 0.25f,
                        x + random.Next(1, 8) + 0.75f, y + random.Next(1, 8) + 0.75f));
                }

                var grids = new List<Entity<MapGridComponent>>();
                var actual = new List<Box2>();
                foreach (var query in queries)
                {
                    // World-bound queries include one tile of padding around their search area.
                    // Expected results use the placed tiles and public traversal order, without masks.
                    var start = new Vector2i((int) MathF.Floor(query.Left) - 1, (int) MathF.Floor(query.Bottom) - 1);
                    var end = new Vector2i((int) MathF.Floor(query.Right) + 1, (int) MathF.Floor(query.Top) + 1);
                    var expected = openTiles
                        .Where(tile => tile.X >= start.X && tile.X <= end.X && tile.Y >= start.Y && tile.Y <= end.Y)
                        .OrderBy(tile => MathF.Floor((float) tile.X / chunkSize))
                        .ThenBy(tile => MathF.Floor((float) tile.Y / chunkSize))
                        .ThenBy(tile => chunkSize == CMUZLevelOpeningCache.DefaultChunkSize ? tile.Y : tile.X)
                        .ThenBy(tile => chunkSize == CMUZLevelOpeningCache.DefaultChunkSize ? tile.X : tile.Y)
                        .Select(tile => new Box2(tile.X, tile.Y, tile.X + 1, tile.Y + 1))
                        .ToArray();
                    Assert.That(cache.HasOpeningInTileBounds(grid, end, start, maps, tiles),
                        Is.EqualTo(expected.Length > 0), $"Reversed bounds {query}");

                    foreach (var limit in new[] { int.MaxValue, 1, 7 })
                    {
                        actual.Clear();
                        var found = cache.TryFindOpeningBounds(mapId, query, actual, out _, limit, true,
                            grids, maps, transform, tiles);
                        Assert.That(found, Is.EqualTo(expected.Length > 0), $"Bounds {query}, limit {limit}");
                        Assert.That(actual, Is.EqualTo(expected.Take(limit)), $"Ordered bounds {query}, limit {limit}");
                    }
                }
            }
            finally
            {
                SEntMan.DeleteEntity(mapUid);
            }
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task RotatedTileGeometryRetainsIdentityWhenGridMoves(int tileSize)
    {
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var mapUid = maps.CreateMap(out var mapId, runMapInit: true);
            var grid = maps.CreateGridEntity(mapId);
            try
            {
                // Serialized TileSize is engine-owned; set before creating any tiles in this fixture.
                typeof(MapGridComponent).GetProperty(nameof(MapGridComponent.TileSize))!.SetValue(grid.Comp, (ushort) tileSize);
                Fill(maps, grid, new Tile(tiles["Plating"].TileId));
                var target = new Vector2i(0, 6);
                maps.SetTile(grid, target, new Tile(tiles["Lattice"].TileId));
                transform.SetLocalRotation(grid.Owner, Angle.FromDegrees(45));
                var cache = new CMUZLevelOpeningCache();
                var grids = new List<Entity<MapGridComponent>>();
                var bounds = new List<Box2>();
                var extent = 6f * tileSize;
                Assert.That(cache.TryFindOpeningBounds(mapId, new Box2(-extent, -extent, extent, extent), bounds,
                    out _, 512, true, grids, maps, transform, tiles), Is.True);

                var expectedCenter = Vector2.Transform(new Vector2(0.5f, 6.5f) * tileSize, transform.GetWorldMatrix(grid.Owner));
                Assert.That(bounds.Any(b => b.Contains(expectedCenter)), Is.True,
                    "The visible side triangle of a rotated grid must be searched.");
                var portals = new List<CMUZOpeningPortal>();
                cache.FindOpeningPortalsNear(mapId, Vector2.Zero, 7f * tileSize, portals, grids, maps, transform, tiles, false);
                Assert.That(portals, Has.Count.EqualTo(1));
                var before = portals[0];
                Assert.That(Vector2.Distance(before.Center, expectedCenter), Is.LessThan(0.0001f));

                var shift = new Vector2(5f, -2f);
                transform.SetLocalPosition(grid.Owner, shift);
                portals.Clear();
                cache.FindOpeningPortalsNear(mapId, shift, 7f * tileSize, portals, grids, maps, transform, tiles, false);
                Assert.That(portals, Has.Count.EqualTo(1));
                Assert.That(portals[0].Grid, Is.EqualTo(before.Grid));
                Assert.That(portals[0].Tile, Is.EqualTo(before.Tile));
                Assert.That(Vector2.Distance(portals[0].Center, before.Center + shift), Is.LessThan(0.0001f));
            }
            finally
            {
                SEntMan.DeleteEntity(mapUid);
            }
        });
    }

    private static void Fill(SharedMapSystem maps, Entity<MapGridComponent> grid, Tile tile)
    {
        for (var x = -12; x <= 12; x++)
        for (var y = -12; y <= 12; y++)
            maps.SetTile(grid, new Vector2i(x, y), tile);
    }
}
