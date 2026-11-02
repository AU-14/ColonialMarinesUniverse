using System.Linq;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using DiagnosticStopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.CMU14.ZLevelBuilding;

public sealed partial class ZLevelBuildingSystem
{
    /// <summary>Construction checks must inspect materialized terrain, never the empty space of an unloaded chunk.</summary>
    public void PrepareCavernConstruction(EntityUid map, System.Numerics.Vector2 position, int radius = 1, bool clearRock = false)
    {
        if (!TryComp<ZGeneratedStoneComponent>(map, out var stone) || stone.LocalizedToAuthoredLevel ||
            !_gridQuery.TryComp(stone.StoneGrid, out var grid) || !TryComp<MapComponent>(map, out var mapComp))
            return;

        var tile = _map.TileIndicesFor(stone.StoneGrid, grid, new MapCoordinates(position, mapComp.MapId));
        var size = Math.Max(2, GetLevelSettings(map).ChunkSize);
        for (var x = FloorDiv(tile.X - radius, size); x <= FloorDiv(tile.X + radius, size); x++)
        for (var y = FloorDiv(tile.Y - radius, size); y <= FloorDiv(tile.Y + radius, size); y++)
            EnsureChunkAt((map, stone), map, new Vector2i(x, y) * size);

        if (clearRock)
        {
            var rocks = GetGeneratedRockIds(GetLevelSettings(map), map);
            for (var x = -radius; x <= radius; x++)
            for (var y = -radius; y <= radius; y++)
                ClearRockAt((stone.StoneGrid, grid), tile + new Vector2i(x, y), rocks);
        }
    }

    private const int StreamChunksPerTick = 2;
    private readonly Queue<(EntityUid Map, Vector2i Chunk)> _generationQueue = new();
    private readonly HashSet<(EntityUid Map, Vector2i Chunk)> _queuedChunks = new();
    private readonly HashSet<(EntityUid Map, Vector2i Chunk)> _wantedChunks = new();
    private readonly Dictionary<EntityUid, (List<Entity<ZGeneratedStoneComponent>> Levels, Vector2i Tile, TimeSpan Expires)> _digPreparation = new();

    private void ClearGenerationQueue()
    {
        _generationQueue.Clear();
        _queuedChunks.Clear();
        _wantedChunks.Clear();
        _digPreparation.Clear();
        _nextStream = TimeSpan.Zero;
    }

    /// <summary>Prepare terrain during the digging action, while the unopened floor still seals the destination.</summary>
    public void PrefetchDigDestination(EntityUid user, EntityCoordinates location, TimeSpan duration)
    {
        var coords = _transform.ToMapCoordinates(location);
        if (_transform.GetMap(location) is not { } source || !IsEnabledOn(source) ||
            !_mapManager.TryFindGridAt(coords, out var sourceGrid, out _))
            return;

        Entity<ZGeneratedStoneComponent> below;
        if (TryComp<CMUZLevelMapComponent>(source, out var zMap) && zMap.MapBelow is { } lower)
        {
            if (TryGetAuthoredStationGrid(lower, coords.Position, out _) ||
                !HasComp<ZGeneratedStoneComponent>(lower) && HasWalkableFloorAt(lower, coords.Position) ||
                !EnsureStoneOnMap(lower, sourceGrid, coords.Position, out below))
                return;
        }
        else if (!CreateStoneBelow(source, sourceGrid, out below))
            return;

        if (!_gridQuery.TryComp(below.Comp.StoneGrid, out var grid) ||
            !TryComp<MapComponent>(below.Owner, out var map))
            return;

        var tile = _map.TileIndicesFor(below.Comp.StoneGrid, grid, new MapCoordinates(coords.Position, map.MapId));
        _digPreparation[user] = (GetCavernColumn(below, tile), tile, _timing.CurTime + duration + TimeSpan.FromSeconds(1));
    }

    public void CancelDigPreparation(EntityUid user) => _digPreparation.Remove(user);

    private void QueueDigPreparation()
    {
        foreach (var (user, preparation) in _digPreparation.ToArray())
        {
            if (!Exists(user) || _timing.CurTime > preparation.Expires)
            {
                _digPreparation.Remove(user);
                continue;
            }

            foreach (var level in preparation.Levels)
            {
                if (!Exists(level.Owner))
                    continue;
                var size = Math.Max(2, GetLevelSettings(level.Owner).ChunkSize);
                var chunk = new Vector2i(FloorDiv(preparation.Tile.X, size), FloorDiv(preparation.Tile.Y, size));
                QueueChunk(level, chunk);
            }
        }
    }

    private void QueueChunk(Entity<ZGeneratedStoneComponent> level, Vector2i chunk)
    {
        var key = (level.Owner, chunk);
        _wantedChunks.Add(key);
        if (!level.Comp.GeneratedChunks.Contains(chunk) && _queuedChunks.Add(key))
            _generationQueue.Enqueue(key);
    }

    /// <summary>
    /// Immediate landing preparation. All slices below a hole are ready before the hole is exposed.
    /// Normal exploration uses the budgeted queue instead of this synchronous path.
    /// </summary>
    public void EnsureChunkAt(Entity<ZGeneratedStoneComponent> below, EntityUid mapUid, Vector2i tile)
    {
        foreach (var level in GetCavernColumn(below, tile))
            GenerateChunk(level, level.Owner, tile);
    }

    private void EnsureStairLanding(Entity<ZGeneratedStoneComponent> level, MapGridComponent grid, Vector2i tile)
    {
        if (_map.TryGetTileRef(level.Comp.StoneGrid, grid, tile, out var existing) && !existing.Tile.IsEmpty)
            return;

        var floors = GetEffectiveFloorTiles(GetLevelSettings(level.Owner), level.Owner);
        if (floors.Count > 0 && _tileDef.TryGetDefinition(floors[0], out var floor))
            _map.SetTile(level.Comp.StoneGrid, grid, tile, new Tile(floor.TileId));
    }

    private CMUCavernField? GetCavernField(Entity<ZGeneratedStoneComponent> level, ZBuildableMapComponent settings)
    {
        if (level.Comp.LocalizedToAuthoredLevel || settings.Caverns is not { } caverns ||
            GetDepth(level.Owner) > caverns.StartDepth)
            return null;

        var source = GetSourceMapForStone(level.Owner);
        var world = EnsureComp<CMUCavernWorldComponent>(source);
        if (world.Settings == null)
            EntityManager.System<CMUCavernConfigSystem>().Apply(world, caverns);
        caverns = world.Settings!;
        if (world.Seed == 0)
            world.Seed = _random.Next(1, int.MaxValue);

        if (level.Comp.CavernSeed != world.Seed || level.Comp.Caverns == null)
        {
            level.Comp.CavernSeed = world.Seed;
            level.Comp.Caverns = caverns;
            Dirty(level);
        }

        return new CMUCavernField(world.Seed, caverns);
    }

    private List<Entity<ZGeneratedStoneComponent>> GetCavernColumn(Entity<ZGeneratedStoneComponent> level, Vector2i tile)
    {
        var levels = new List<Entity<ZGeneratedStoneComponent>> { level };
        var field = GetCavernField(level, GetLevelSettings(level.Owner));
        if (field == null || !field.TryGetBand(GetDepth(level.Owner), out var top, out var bottom) ||
            !_gridQuery.TryComp(level.Comp.StoneGrid, out var grid))
            return levels;

        var position = _transform.ToMapCoordinates(_map.GridTileToLocal(level.Comp.StoneGrid, grid, tile)).Position;
        // Only generated levels participate. A mapped station level is an immutable boundary, even if
        // somebody digs into it later and gives it a localized stone marker.
        foreach (var direction in new[] { -1, 1 })
        {
            var current = level;
            var depth = GetDepth(current.Owner);
            while (direction < 0 ? depth > bottom : depth < top)
            {
                if (!EnsureNeighborLevel(current.Owner, direction, current.Comp.StoneGrid, position, out var map, out _) ||
                    TryGetAuthoredStationGrid(map, position, out _) ||
                    !HasComp<ZGeneratedStoneComponent>(map) && HasWalkableFloorAt(map, position) ||
                    !EnsureStoneOnMap(map, current.Comp.StoneGrid, position, out var next) ||
                    next.Comp.LocalizedToAuthoredLevel)
                    break;

                levels.Add(next);
                current = next;
                depth += direction;
            }
        }

        levels.Sort((a, b) => GetDepth(a.Owner).CompareTo(GetDepth(b.Owner)));
        return levels;
    }

    private void QueueExploration(Entity<ZGeneratedStoneComponent> level, Vector2i tile, int size)
    {
        var levels = GetCavernColumn(level, tile);
        var center = new Vector2i(FloorDiv(tile.X, size), FloorDiv(tile.Y, size));
        for (var radius = 0; radius <= StreamRadiusChunks; radius++)
        {
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (Math.Max(Math.Abs(x), Math.Abs(y)) != radius)
                        continue;

                    var chunk = center + new Vector2i(x, y);
                    foreach (var slice in levels)
                        QueueChunk(slice, chunk);
                }
            }
        }

        // A teleport or fast mover may outrun prefetch. Finish only the nearby landing columns rather
        // than allowing an ungenerated tile to behave as a bottomless pit. These calls are idempotent.
        var localX = tile.X - center.X * size;
        var localY = tile.Y - center.Y * size;
        for (var x = -1; x <= 1; x++)
        {
            if (x < 0 && localX >= 3 || x > 0 && localX < size - 3)
                continue;
            for (var y = -1; y <= 1; y++)
            {
                if (y < 0 && localY >= 3 || y > 0 && localY < size - 3)
                    continue;
                foreach (var slice in levels)
                    GenerateChunk(slice, slice.Owner, (center + new Vector2i(x, y)) * size);
            }
        }
    }

    private void ProcessGenerationQueue()
    {
        var generated = 0;
        var started = DiagnosticStopwatch.GetTimestamp();
        // Discard stale requests after explorers leave. Never unload or regenerate an explored mine.
        var examined = 0;
        while (generated < StreamChunksPerTick && examined++ < 128 && _generationQueue.TryDequeue(out var request))
        {
            _queuedChunks.Remove(request);
            if (!_wantedChunks.Contains(request) || !Exists(request.Map) ||
                !IsEnabledOn(request.Map) || !TryComp<ZGeneratedStoneComponent>(request.Map, out var stone) ||
                stone.GeneratedChunks.Contains(request.Chunk))
                continue;

            var size = Math.Max(2, GetLevelSettings(request.Map).ChunkSize);
            GenerateChunk((request.Map, stone), request.Map, request.Chunk * size);
            generated++;
            // Entity creation is atomic per chunk; yield before starting another if this chunk used the budget.
            if (DiagnosticStopwatch.GetElapsedTime(started) >= TimeSpan.FromMilliseconds(4))
                break;
        }
    }
}
