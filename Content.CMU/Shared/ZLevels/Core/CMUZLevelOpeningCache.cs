using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.ZLevels.Core;

public sealed class CMUZLevelOpeningCache
{
    public const int DefaultChunkSize = 8;

    private readonly Dictionary<EntityUid, GridOpeningCache> _gridCaches = new();
    private readonly int _chunkSize;

    public CMUZLevelOpeningCache(int chunkSize = DefaultChunkSize)
    {
        _chunkSize = chunkSize;
    }

    public int ChunkSize => _chunkSize;

    public void Clear()
    {
        _gridCaches.Clear();
    }

    public void RemoveGrid(EntityUid grid)
    {
        _gridCaches.Remove(grid);
    }

    public void InvalidateTiles(Entity<MapGridComponent> grid, ReadOnlySpan<TileChangedEntry> changes)
    {
        if (!_gridCaches.TryGetValue(grid.Owner, out var cache))
            return;

        cache.LastTileModifiedTick = grid.Comp.LastTileModifiedTick;

        if (changes.Length == 0)
        {
            cache.Chunks.Clear();
            return;
        }

        for (var i = 0; i < changes.Length; i++)
        {
            var chunk = SharedMapSystem.GetChunkIndices(changes[i].GridIndices, _chunkSize);
            cache.Chunks.Remove(chunk);
        }
    }

    public bool ChunkHasOpening(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        if (!_gridCaches.TryGetValue(grid.Owner, out var cache))
        {
            cache = new GridOpeningCache();
            _gridCaches[grid.Owner] = cache;
        }

        if (cache.LastTileModifiedTick != grid.Comp.LastTileModifiedTick)
        {
            cache.LastTileModifiedTick = grid.Comp.LastTileModifiedTick;
            cache.Chunks.Clear();
        }

        if (cache.Chunks.TryGetValue(chunk, out var cached))
            return cached;

        var hasOpening = CalculateChunkHasOpening(grid, chunk, map, tile);
        cache.Chunks[chunk] = hasOpening;
        return hasOpening;
    }

    public bool HasOpeningInTileBounds(
        Entity<MapGridComponent> grid,
        Vector2i start,
        Vector2i end,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        var startX = Math.Min(start.X, end.X);
        var endX = Math.Max(start.X, end.X);
        var startY = Math.Min(start.Y, end.Y);
        var endY = Math.Max(start.Y, end.Y);

        var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), _chunkSize);
        var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), _chunkSize);

        for (var x = startChunk.X; x <= endChunk.X; x++)
        {
            for (var y = startChunk.Y; y <= endChunk.Y; y++)
            {
                if (ChunkHasOpening(grid, new Vector2i(x, y), map, tile))
                    return true;
            }
        }

        return false;
    }

    public bool TryFindOpeningBounds(
        MapId mapId,
        Box2 worldAabb,
        List<Box2>? openingBounds,
        out Box2 combinedOpeningBounds,
        int maxOpeningBounds,
        bool exactOpeningBounds,
        List<Entity<MapGridComponent>> gridScratch,
        IMapManager mapManager,
        SharedMapSystem map,
        SharedTransformSystem transform,
        ITileDefinitionManager tileDefinition)
    {
        combinedOpeningBounds = default;
        gridScratch.Clear();

        mapManager.FindGridsIntersecting(mapId, worldAabb, ref gridScratch, approx: true, includeMap: true);
        if (gridScratch.Count == 0)
            return false;

        var foundOpening = false;
        var bottomLeft = new MapCoordinates(worldAabb.BottomLeft, mapId);
        var topRight = new MapCoordinates(worldAabb.TopRight, mapId);

        foreach (var grid in gridScratch)
        {
            GetTileSearchBounds(grid, bottomLeft, topRight, map, out var startX, out var endX, out var startY, out var endY);
            var gridWorldMatrix = transform.GetWorldMatrix(grid.Owner);

            var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), _chunkSize);
            var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), _chunkSize);

            for (var chunkX = startChunk.X; chunkX <= endChunk.X; chunkX++)
            {
                for (var chunkY = startChunk.Y; chunkY <= endChunk.Y; chunkY++)
                {
                    var chunk = new Vector2i(chunkX, chunkY);
                    if (!ChunkHasOpening(grid, chunk, map, tileDefinition))
                        continue;

                    if (openingBounds == null)
                        return true;

                    if (!exactOpeningBounds)
                    {
                        var chunkStart = chunk * _chunkSize;
                        var chunkEnd = chunkStart + new Vector2i(_chunkSize, _chunkSize);
                        var localBounds = new Box2(chunkStart.X, chunkStart.Y, chunkEnd.X, chunkEnd.Y);
                        var worldBounds = gridWorldMatrix.TransformBox(localBounds);

                        AddOpeningBounds(openingBounds, worldBounds, ref combinedOpeningBounds, ref foundOpening);
                        if (openingBounds.Count >= maxOpeningBounds)
                            return true;

                        continue;
                    }

                    var tileStart = chunk * _chunkSize;
                    var tileEnd = tileStart + new Vector2i(_chunkSize, _chunkSize);
                    var tileStartX = Math.Max(startX, tileStart.X);
                    var tileEndX = Math.Min(endX, tileEnd.X - 1);
                    var tileStartY = Math.Max(startY, tileStart.Y);
                    var tileEndY = Math.Min(endY, tileEnd.Y - 1);

                    for (var tileX = tileStartX; tileX <= tileEndX; tileX++)
                    {
                        for (var tileY = tileStartY; tileY <= tileEndY; tileY++)
                        {
                            var openingTile = new Vector2i(tileX, tileY);
                            if (!IsOpeningTile(grid, openingTile, map, tileDefinition))
                                continue;

                            var localTileBounds = new Box2(tileX, tileY, tileX + 1, tileY + 1);
                            var worldTileBounds = gridWorldMatrix.TransformBox(localTileBounds);
                            AddOpeningBounds(openingBounds, worldTileBounds, ref combinedOpeningBounds, ref foundOpening);

                            if (openingBounds.Count >= maxOpeningBounds)
                                return true;
                        }
                    }
                }
            }
        }

        return foundOpening;
    }

    public void FindOpeningCentersNear(
        MapId mapId,
        Vector2 sourcePosition,
        float searchRadius,
        List<(Vector2 Center, float Distance)> openings,
        List<Entity<MapGridComponent>> gridScratch,
        IMapManager mapManager,
        SharedMapSystem map,
        SharedTransformSystem transform,
        ITileDefinitionManager tileDefinition,
        bool edgeOnly = true)
    {
        var searchBounds = Box2.CenteredAround(sourcePosition, new Vector2(searchRadius * 2f, searchRadius * 2f));
        gridScratch.Clear();
        mapManager.FindGridsIntersecting(mapId, searchBounds, ref gridScratch, approx: true, includeMap: true);

        if (gridScratch.Count == 0)
            return;

        var bottomLeft = new MapCoordinates(searchBounds.BottomLeft, mapId);
        var topRight = new MapCoordinates(searchBounds.TopRight, mapId);
        var searchRadiusSquared = searchRadius * searchRadius;

        foreach (var grid in gridScratch)
        {
            GetTileSearchBounds(grid, bottomLeft, topRight, map, out var startX, out var endX, out var startY, out var endY);

            var startChunk = SharedMapSystem.GetChunkIndices(new Vector2i(startX, startY), _chunkSize);
            var endChunk = SharedMapSystem.GetChunkIndices(new Vector2i(endX, endY), _chunkSize);
            var gridWorldMatrix = transform.GetWorldMatrix(grid.Owner);
            if (!Matrix3x2.Invert(gridWorldMatrix, out var gridInvWorldMatrix))
                continue;

            var localSourcePosition = Vector2.Transform(sourcePosition, gridInvWorldMatrix);
            var sourceInsideOpening = IsExistingOpeningTile(
                grid,
                new Vector2i((int) MathF.Floor(localSourcePosition.X), (int) MathF.Floor(localSourcePosition.Y)),
                map,
                tileDefinition);

            for (var chunkX = startChunk.X; chunkX <= endChunk.X; chunkX++)
            {
                for (var chunkY = startChunk.Y; chunkY <= endChunk.Y; chunkY++)
                {
                    var chunk = new Vector2i(chunkX, chunkY);
                    if (!ChunkHasOpening(grid, chunk, map, tileDefinition))
                        continue;

                    var chunkStart = chunk * _chunkSize;
                    var chunkEnd = chunkStart + new Vector2i(_chunkSize, _chunkSize);
                    var tileStartX = Math.Max(startX, chunkStart.X);
                    var tileEndX = Math.Min(endX, chunkEnd.X - 1);
                    var tileStartY = Math.Max(startY, chunkStart.Y);
                    var tileEndY = Math.Min(endY, chunkEnd.Y - 1);

                    for (var tileX = tileStartX; tileX <= tileEndX; tileX++)
                    {
                        for (var tileY = tileStartY; tileY <= tileEndY; tileY++)
                        {
                            var openingTile = new Vector2i(tileX, tileY);
                            if (!IsOpeningTile(grid, openingTile, map, tileDefinition))
                                continue;

                            if (edgeOnly &&
                                !IsOpeningEdgeTile(grid, openingTile, localSourcePosition, sourceInsideOpening, map, tileDefinition))
                            {
                                continue;
                            }

                            var center = Vector2.Transform(new Vector2(tileX + 0.5f, tileY + 0.5f), gridWorldMatrix);
                            var distanceSquared = Vector2.DistanceSquared(sourcePosition, center);
                            if (distanceSquared > searchRadiusSquared)
                                continue;

                            openings.Add((center, MathF.Sqrt(distanceSquared)));
                        }
                    }
                }
            }
        }
    }

    public static bool IsOpeningTile(
        Tile tile,
        ITileDefinitionManager tileDefinition)
    {
        if (tile.IsEmpty)
            return true;

        var tileDef = (ContentTileDefinition) tileDefinition[tile.TypeId];
        return tileDef.Transparent;
    }

    public static bool IsOpeningTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef))
            return true;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsOpeningTile(
        EntityUid mapUid,
        MapGridComponent grid,
        Vector2 position,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(mapUid, grid, position, out var tileRef))
            return true;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsExistingOpeningTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (!map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef))
            return false;

        return IsOpeningTile(tileRef.Tile, tileDefinition);
    }

    public static bool IsOpeningEdgeTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        Vector2 localSourcePosition,
        bool sourceInsideOpening,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        if (sourceInsideOpening)
            return IsOpeningPerimeterTile(grid, tile, map, tileDefinition);

        var localCenter = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        var directionToSource = localSourcePosition - localCenter;
        if (directionToSource.LengthSquared() < 0.001f)
            return true;

        Vector2i sourceNeighbor;
        if (Math.Abs(directionToSource.X) > Math.Abs(directionToSource.Y))
        {
            sourceNeighbor = new Vector2i(Math.Sign(directionToSource.X), 0);
        }
        else
        {
            sourceNeighbor = new Vector2i(0, Math.Sign(directionToSource.Y));
        }

        return !IsOpeningTile(grid, tile + sourceNeighbor, map, tileDefinition);
    }

    public static bool IsOpeningPerimeterTile(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        SharedMapSystem map,
        ITileDefinitionManager tileDefinition)
    {
        return !IsOpeningTile(grid, tile + new Vector2i(1, 0), map, tileDefinition) ||
               !IsOpeningTile(grid, tile + new Vector2i(-1, 0), map, tileDefinition) ||
               !IsOpeningTile(grid, tile + new Vector2i(0, 1), map, tileDefinition) ||
               !IsOpeningTile(grid, tile + new Vector2i(0, -1), map, tileDefinition);
    }

    private bool CalculateChunkHasOpening(
        Entity<MapGridComponent> grid,
        Vector2i chunk,
        SharedMapSystem map,
        ITileDefinitionManager tile)
    {
        var startX = chunk.X * _chunkSize;
        var startY = chunk.Y * _chunkSize;
        var endX = startX + _chunkSize;
        var endY = startY + _chunkSize;

        for (var x = startX; x < endX; x++)
        {
            for (var y = startY; y < endY; y++)
            {
                if (IsOpeningTile(grid, new Vector2i(x, y), map, tile))
                    return true;
            }
        }

        return false;
    }

    private static void GetTileSearchBounds(
        Entity<MapGridComponent> grid,
        MapCoordinates bottomLeft,
        MapCoordinates topRight,
        SharedMapSystem map,
        out int startX,
        out int endX,
        out int startY,
        out int endY)
    {
        var tileBottomLeft = map.TileIndicesFor(grid.Owner, grid.Comp, bottomLeft);
        var tileTopRight = map.TileIndicesFor(grid.Owner, grid.Comp, topRight);

        startX = Math.Min(tileBottomLeft.X, tileTopRight.X) - 1;
        endX = Math.Max(tileBottomLeft.X, tileTopRight.X) + 1;
        startY = Math.Min(tileBottomLeft.Y, tileTopRight.Y) - 1;
        endY = Math.Max(tileBottomLeft.Y, tileTopRight.Y) + 1;
    }

    private static void AddOpeningBounds(
        List<Box2> openingBounds,
        Box2 bounds,
        ref Box2 combinedOpeningBounds,
        ref bool foundOpening)
    {
        openingBounds.Add(bounds);
        combinedOpeningBounds = foundOpening
            ? combinedOpeningBounds.Union(bounds)
            : bounds;
        foundOpening = true;
    }

    private sealed class GridOpeningCache
    {
        public GameTick LastTileModifiedTick;
        public readonly Dictionary<Vector2i, bool> Chunks = new();
    }
}
