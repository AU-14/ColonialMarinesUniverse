// SPDX-License-Identifier: LicenseRef-AdvancedAtkinsonatorv2-Proprietary
// Copyright (c) 2026 wray-git. All rights reserved.
// Proprietary - reuse only with the Author's prior written authorization. See LICENSE-AdvancedAtkinsonatorv2.md.
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Random;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level), Phase 2: dig-down on ANY map.
///
/// Lazily bootstraps a CMU z-network and a stone level beneath a map the first time someone digs there, so
/// vertical building works even on maps authored as single-z. Stone is generated per-chunk on demand (on dig
/// and as players approach), using the existing minable <c>AsteroidRock</c> so no new art is required.
///
/// Opt a map out via <see cref="ZBuildableMapComponent"/> (<c>enabled: false</c>) or globally via
/// <see cref="GloballyEnabled"/> in code.
/// </summary>
public sealed class ZLevelBuildingSystem : EntitySystem
{
    [Dependency] private readonly CMUZLevelsSystem _zLevels = default!;
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Global code switch for the whole building overhaul. Set to <c>false</c> to disable dig-down / lazy
    /// z-level generation on every map at once (per-map opt-out lives on <see cref="ZBuildableMapComponent"/>).
    /// </summary>
    public bool GloballyEnabled = true;

    /// <summary>How far (in chunks) around a player to pre-generate stone as they move on a stone level.</summary>
    private const int StreamRadiusChunks = 1;

    // The bare walk-through traversal stair spawned by the dig commands. It always goes on the LOWER of the two
    // connected levels, and the shaft tile on the upper level is opened (emptied) so descent works - see the
    // CMU traversal model documented in z_stairs.yml. Using the companion proto (no ZStair) so no recursive
    // stone-generation or beam-placement fires when a dig command places it.
    private const string TraversalStair = "AU14ZStairPure";

    private TimeSpan _nextStream;
    private static readonly TimeSpan StreamInterval = TimeSpan.FromSeconds(1);

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
    }

    /// <summary>Whether the building overhaul is allowed to operate on the given map.</summary>
    public bool IsEnabledOn(EntityUid mapUid)
    {
        if (!GloballyEnabled)
            return false;

        // Default-on: only a component that explicitly says enabled:false opts the map out.
        return !TryComp<ZBuildableMapComponent>(mapUid, out var settings) || settings.Enabled;
    }

    private ZBuildableMapComponent GetSettings(EntityUid mapUid)
        => CompOrNull<ZBuildableMapComponent>(mapUid) ?? new ZBuildableMapComponent();

    /// <summary>True if there is a grid with a real (non-empty) floor tile directly under <paramref name="worldPos"/>.</summary>
    private bool HasWalkableFloorAt(EntityUid mapUid, Vector2 worldPos)
    {
        if (!TryComp<MapComponent>(mapUid, out var mapComp))
            return false;

        var coords = new MapCoordinates(worldPos, mapComp.MapId);
        if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        return _map.TryGetTileRef(gridUid, grid, tile, out var tileRef) && !tileRef.Tile.IsEmpty;
    }

    /// <summary>
    /// Creates a brand-new underground stone map directly below <paramref name="mapUid"/>, bootstrapping a
    /// z-network for single-z maps on the fly. Only used when there is nothing below at all.
    /// </summary>
    private bool CreateStoneBelow(EntityUid mapUid, EntityUid sourceGrid, out Entity<ZGeneratedStoneComponent> below)
    {
        below = default;

        // Make sure the source map is in a network at depth 0.
        if (!_zLevels.TryGetZNetwork(mapUid, out var network))
        {
            var created = _zLevels.CreateZNetwork();
            if (!_zLevels.TryAddMapsIntoZNetwork(created, new Dictionary<EntityUid, int> { [mapUid] = 0 }))
                return false;

            network = created;
        }

        if (!network.HasValue)
            return false;

        var depth = TryComp<CMUZLevelMapComponent>(mapUid, out var nowZ) ? nowZ.Depth : 0;

        var newMapUid = _map.CreateMap(out _, runMapInit: true);
        var grid = _mapManager.CreateGridEntity(newMapUid);

        if (_gridQuery.TryComp(sourceGrid, out _))
            _transform.SetWorldPosition(grid.Owner, _transform.GetWorldPosition(sourceGrid));

        if (!_zLevels.TryAddMapsIntoZNetwork(network.Value, new Dictionary<EntityUid, int> { [newMapUid] = depth - 1 }))
        {
            Del(newMapUid);
            return false;
        }

        var stone = EnsureComp<ZGeneratedStoneComponent>(newMapUid);
        stone.StoneGrid = grid.Owner;
        below = (newMapUid, stone);
        return true;
    }

    /// <summary>
    /// Called by <see cref="ZStairSystem"/> when a DOWN stair is constructed: ensures a stone underground level
    /// exists below <paramref name="mapUid"/>, generates the landing chunk, clears a pocket at
    /// <paramref name="worldPos"/>, and returns the stone GRID for companion stair placement.
    ///
    /// Mirrors <see cref="DigIntoStone"/> but does NOT teleport any entity - only prepares the underground.
    /// </summary>
    public bool PrepareStoneForStair(EntityUid mapUid, EntityUid sourceGrid, Vector2 worldPos, out EntityUid stoneGrid)
    {
        stoneGrid = default;

        if (!IsEnabledOn(mapUid))
            return false;

        Entity<ZGeneratedStoneComponent> below;

        if (TryComp<CMUZLevelMapComponent>(mapUid, out var zMapComp) && zMapComp.MapBelow is { } belowMap)
        {
            if (!HasComp<ZGeneratedStoneComponent>(belowMap) && HasWalkableFloorAt(belowMap, worldPos))
            {
                // Real authored walkable floor below (not underground) - return its grid for companion placement.
                if (TryComp<MapComponent>(belowMap, out var floorMapC) &&
                    _mapManager.TryFindGridAt(new MapCoordinates(worldPos, floorMapC.MapId), out var foundGrid, out _))
                {
                    stoneGrid = foundGrid;
                    return true;
                }
                return false;
            }

            if (!EnsureStoneOnMap(belowMap, sourceGrid, worldPos, out below))
                return false;
        }
        else
        {
            if (!CreateStoneBelow(mapUid, sourceGrid, out below))
                return false;
        }

        if (!_gridQuery.TryComp(below.Comp.StoneGrid, out var belowGridComp))
            return false;

        if (!TryComp<MapComponent>(below.Owner, out var belowMapComp))
            return false;

        var landingCoords = new MapCoordinates(worldPos, belowMapComp.MapId);
        var landingTile = _map.TileIndicesFor(below.Comp.StoneGrid, belowGridComp, landingCoords);

        EnsureChunkAt(below, below.Owner, landingTile);
        ClearLandingPocket((below.Comp.StoneGrid, belowGridComp), landingTile, GetSettings(GetSourceMapForStone(below.Owner)).StoneRockEntity);

        stoneGrid = below.Comp.StoneGrid;
        return true;
    }

    /// <summary>
    /// Ensures the z-level one step in <paramref name="direction"/> from <paramref name="mapUid"/> exists (creating
    /// an empty linked level + grid if needed), and returns a grid on it under <paramref name="worldPos"/>. Used by
    /// the z-stairs to reflect a support onto the adjacent level. Does NOT generate stone.
    /// </summary>
    public bool EnsureNeighborLevel(EntityUid mapUid, int direction, EntityUid sourceGrid, Vector2 worldPos, out EntityUid targetMap, out EntityUid targetGrid)
    {
        targetMap = default;
        targetGrid = default;

        if (!IsEnabledOn(mapUid))
            return false;

        EntityUid? existing = null;
        if (TryComp<CMUZLevelMapComponent>(mapUid, out var z))
            existing = direction > 0 ? z.MapAbove : z.MapBelow;

        if (existing is { } ex)
        {
            targetMap = ex;
        }
        else
        {
            if (!_zLevels.TryGetZNetwork(mapUid, out var network))
            {
                var created = _zLevels.CreateZNetwork();
                if (!_zLevels.TryAddMapsIntoZNetwork(created, new Dictionary<EntityUid, int> { [mapUid] = 0 }))
                    return false;

                network = created;
            }

            if (!network.HasValue)
                return false;

            var depth = TryComp<CMUZLevelMapComponent>(mapUid, out var nz) ? nz.Depth : 0;
            var newMap = _map.CreateMap(out _, runMapInit: true);

            if (!_zLevels.TryAddMapsIntoZNetwork(network.Value, new Dictionary<EntityUid, int> { [newMap] = depth + direction }))
            {
                Del(newMap);
                return false;
            }

            targetMap = newMap;
        }

        if (!TryComp<MapComponent>(targetMap, out var targetMapComp))
            return false;

        if (_mapManager.TryFindGridAt(new MapCoordinates(worldPos, targetMapComp.MapId), out var found, out _))
        {
            targetGrid = found;
        }
        else
        {
            var grid = _mapManager.CreateGridEntity(targetMap);
            if (_gridQuery.TryComp(sourceGrid, out _))
                _transform.SetWorldPosition(grid.Owner, _transform.GetWorldPosition(sourceGrid));
            targetGrid = grid.Owner;
        }

        return true;
    }

    /// <summary>
    /// Turns an EXISTING level below (one already linked in the z-network, e.g. an empty/void level on a multi-z
    /// map like Shepherd's Pride) into a diggable stone level: marks it and gives it a stone grid aligned to the
    /// source so the digger always lands on solid generated ground instead of falling into space.
    /// </summary>
    private bool EnsureStoneOnMap(EntityUid belowMap, EntityUid sourceGrid, Vector2 worldPos, out Entity<ZGeneratedStoneComponent> below)
    {
        below = default;

        if (TryComp<ZGeneratedStoneComponent>(belowMap, out var existing) && _gridQuery.HasComponent(existing.StoneGrid))
        {
            below = (belowMap, existing);
            return true;
        }

        if (!TryComp<MapComponent>(belowMap, out var belowMapComp))
            return false;

        // Prefer a grid already under the player's x/y on that level; otherwise create one aligned to the source.
        EntityUid stoneGrid;
        if (_mapManager.TryFindGridAt(new MapCoordinates(worldPos, belowMapComp.MapId), out var foundGrid, out _))
        {
            stoneGrid = foundGrid;
        }
        else
        {
            var grid = _mapManager.CreateGridEntity(belowMap);
            if (_gridQuery.TryComp(sourceGrid, out _))
                _transform.SetWorldPosition(grid.Owner, _transform.GetWorldPosition(sourceGrid));
            stoneGrid = grid.Owner;
        }

        var stone = EnsureComp<ZGeneratedStoneComponent>(belowMap);
        stone.StoneGrid = stoneGrid;
        below = (belowMap, stone);
        return true;
    }

    /// <summary>
    /// Generates (once) the dirt/stone chunk that contains <paramref name="tile"/> on the stone level: fills
    /// every tile with the stone floor and plants a minable rock on each, so the player has solid ground to
    /// mine through. The landing pocket is cleared separately by the caller.
    /// </summary>
    public void EnsureChunkAt(Entity<ZGeneratedStoneComponent> below, EntityUid mapUid, Vector2i tile)
    {
        if (!_gridQuery.TryComp(below.Comp.StoneGrid, out var grid))
            return;

        var settings = GetSettings(GetSourceMapForStone(mapUid));
        var size = Math.Max(2, settings.ChunkSize);
        var chunk = new Vector2i(FloorDiv(tile.X, size), FloorDiv(tile.Y, size));

        if (!below.Comp.GeneratedChunks.Add(chunk))
            return;

        if (settings.StoneFloorTiles.Count == 0)
            return;

        var origin = new Vector2i(chunk.X * size, chunk.Y * size);
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                var index = new Vector2i(origin.X + x, origin.Y + y);

                // Never overwrite pre-built / mapped content: skip tiles that already have a real floor tile or
                // any anchored entity. This keeps existing structures on an already-mapped below level intact;
                // only true empty space gets filled with stone.
                if (_map.TryGetTileRef(below.Comp.StoneGrid, grid, index, out var existing) && !existing.Tile.IsEmpty)
                    continue;

                if (TileHasAnchored(below.Comp.StoneGrid, grid, index))
                    continue;

                // Random stone/dirt mix for the floor.
                var floorId = _random.Pick(settings.StoneFloorTiles);
                if (!_tileDef.TryGetDefinition(floorId, out var floorDef))
                    continue;

                _map.SetTile(below.Comp.StoneGrid, grid, index, new Tile(floorDef.TileId));

                var coords = _map.GridTileToLocal(below.Comp.StoneGrid, grid, index);
                Spawn(settings.StoneRockEntity, coords);
            }
        }
    }

    /// <summary>
    /// Digs straight down at <paramref name="digger"/>'s position.
    ///
    /// Decision order:
    ///  - A real walkable floor directly below (an upper floor of a multi-floor building) -> step down onto it.
    ///  - Otherwise (the ground z-level, an existing stone level, or an empty/void level on a multi-z map) -> dig
    ///    into stone: ensure/generate it, clear a landing pocket, and drop the digger onto solid generated ground.
    ///
    /// The "dig into stone even when a void level already exists below" path is what stops players falling into
    /// space on multi-z maps such as Shepherd's Pride that have z-levels but no authored underground.
    /// </summary>
    public bool DigDown(EntityUid digger)
    {
        var xform = Transform(digger);
        if (xform.MapUid is not { } mapUid || xform.GridUid is not { } gridUid)
            return false;

        if (!IsEnabledOn(mapUid))
            return false;

        var worldPos = _transform.GetWorldPosition(digger);

        if (TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) && zMap.MapBelow is { } belowMap)
        {
            // A real, walkable authored floor below (a building floor) -> just step down onto it.
            if (!HasComp<ZGeneratedStoneComponent>(belowMap) && HasWalkableFloorAt(belowMap, worldPos))
                return DescendToFloor(digger, gridUid, xform.Coordinates, belowMap);

            // Our stone, or an empty/void level -> turn it into diggable stone and dig in.
            if (!EnsureStoneOnMap(belowMap, gridUid, worldPos, out var existingStone))
                return false;

            return DigIntoStone(digger, gridUid, xform.Coordinates, existingStone);
        }

        // Nothing below at all -> create a fresh underground stone level and dig into it.
        if (!CreateStoneBelow(mapUid, gridUid, out var newStone))
            return false;

        return DigIntoStone(digger, gridUid, xform.Coordinates, newStone);
    }

    /// <summary>
    /// Generates stone around the landing spot, clears a pocket, drops the digger onto it, and leaves a two-way
    /// ladder shaft (this is the underground case, where being able to climb back out of the mine matters).
    /// </summary>
    private bool DigIntoStone(EntityUid digger, EntityUid sourceGridUid, EntityCoordinates sourceCoords, Entity<ZGeneratedStoneComponent> below)
    {
        if (!_gridQuery.TryComp(below.Comp.StoneGrid, out var belowGrid))
            return false;

        var sourceMap = Transform(digger).MapUid ?? EntityUid.Invalid;
        var worldPos = _transform.GetWorldPosition(digger);
        var landingCoords = new MapCoordinates(worldPos, Comp<MapComponent>(below.Owner).MapId);
        var landingTile = _map.TileIndicesFor(below.Comp.StoneGrid, belowGrid, landingCoords);

        var settings = GetSettings(GetSourceMapForStone(below.Owner));

        EnsureChunkAt(below, sourceMap, landingTile);
        ClearLandingPocket((below.Comp.StoneGrid, belowGrid), landingTile, settings.StoneRockEntity);

        // One traversal stair on the LOWER (stone) level, and an OPEN shaft on the surface above it so the dig
        // hole is a real, re-traversable shaft. Putting a stair on both levels (the old behavior) stacked two
        // ramps at the same spot and flung the player between z-levels.
        SpawnAnchoredOnce(TraversalStair, below.Comp.StoneGrid, belowGrid, landingTile);

        if (_gridQuery.TryComp(sourceGridUid, out var sourceGrid))
        {
            var sourceTile = _map.TileIndicesFor(sourceGridUid, sourceGrid, sourceCoords);
            _map.SetTile(sourceGridUid, sourceGrid, sourceTile, Tile.Empty);
        }

        _transform.SetMapCoordinates(digger, landingCoords);

        var fall = new DamageSpecifier();
        fall.DamageDict.Add("Blunt", FixedPoint2.New(3));
        _damage.TryChangeDamage(digger, fall, origin: digger);

        return true;
    }

    /// <summary>
    /// Steps the digger down onto an existing authored floor below, at the SAME world x/y (no stone is generated
    /// under a building). Leaves a down-ladder where we dug so the hole is traversable; the return trip is a
    /// <see cref="DigUp"/> (or any existing stairs on the floor below).
    /// </summary>
    private bool DescendToFloor(EntityUid digger, EntityUid sourceGridUid, EntityCoordinates sourceCoords, EntityUid floorBelow)
    {
        if (!TryComp<MapComponent>(floorBelow, out var belowMap))
            return false;

        var worldPos = _transform.GetWorldPosition(digger);
        var belowCoords = new MapCoordinates(worldPos, belowMap.MapId);

        // Traversal stair on the floor BELOW (the lower level) and an open shaft above so the hole works both ways.
        if (_mapManager.TryFindGridAt(belowCoords, out var belowGridUid, out var belowGrid))
        {
            var belowTile = _map.TileIndicesFor(belowGridUid, belowGrid, belowCoords);
            SpawnAnchoredOnce(TraversalStair, belowGridUid, belowGrid, belowTile);
        }

        if (_gridQuery.TryComp(sourceGridUid, out var sourceGrid))
        {
            var sourceTile = _map.TileIndicesFor(sourceGridUid, sourceGrid, sourceCoords);
            _map.SetTile(sourceGridUid, sourceGrid, sourceTile, Tile.Empty);
        }

        _transform.SetMapCoordinates(digger, belowCoords);

        var fall = new DamageSpecifier();
        fall.DamageDict.Add("Blunt", FixedPoint2.New(3));
        _damage.TryChangeDamage(digger, fall, origin: digger);
        return true;
    }

    /// <summary>
    /// Digs straight up to the level above at the SAME world x/y, so where you surface reflects how far you
    /// travelled underground. Blocked only if a solid wall sits directly above the spot (the "wall above"
    /// rule); open space above is fine. Fails if there is no level above (you are already at the top).
    /// </summary>
    public bool DigUp(EntityUid digger)
    {
        var xform = Transform(digger);
        if (xform.MapUid is not { } mapUid)
            return false;

        if (!IsEnabledOn(mapUid))
            return false;

        if (!TryComp<CMUZLevelMapComponent>(mapUid, out var zMap) ||
            zMap.MapAbove is not { } aboveMap ||
            !TryComp<MapComponent>(aboveMap, out var aboveMapComp))
        {
            return false;
        }

        var worldPos = _transform.GetWorldPosition(digger);
        var targetCoords = new MapCoordinates(worldPos, aboveMapComp.MapId);

        // Wall-above rule: if a solid (impassable) structure occupies the tile directly above, you can't dig up
        // here. Otherwise open a shaft on the level above so the climb is re-traversable (descent needs a hole).
        if (_mapManager.TryFindGridAt(targetCoords, out var aboveGridUid, out var aboveGrid))
        {
            var aboveTile = _map.TileIndicesFor(aboveGridUid, aboveGrid, targetCoords);
            if (_turf.IsTileBlocked(aboveGridUid, aboveTile, CollisionGroup.Impassable))
                return false;

            _map.SetTile(aboveGridUid, aboveGrid, aboveTile, Tile.Empty);
        }

        // Place the traversal stair on THIS (lower) level so the shaft is walkable both ways.
        if (xform.GridUid is { } curGridUid && _gridQuery.TryComp(curGridUid, out var curGrid))
        {
            var curTile = _map.TileIndicesFor(curGridUid, curGrid, xform.Coordinates);
            SpawnAnchoredOnce(TraversalStair, curGridUid, curGrid, curTile);
        }

        _transform.SetMapCoordinates(digger, targetCoords);
        return true;
    }

    /// <summary>Removes the generated rock on the landing tile (and its cardinal neighbours) so the digger isn't buried.</summary>
    private void ClearLandingPocket(Entity<MapGridComponent> grid, Vector2i tile, string rockId)
    {
        ClearRockAt(grid, tile, rockId);
        ClearRockAt(grid, tile + new Vector2i(1, 0), rockId);
        ClearRockAt(grid, tile + new Vector2i(-1, 0), rockId);
        ClearRockAt(grid, tile + new Vector2i(0, 1), rockId);
        ClearRockAt(grid, tile + new Vector2i(0, -1), rockId);
    }

    private void ClearRockAt(Entity<MapGridComponent> grid, Vector2i tile, string rockId)
    {
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile);
        foreach (var ent in anchored)
        {
            // Only remove our generated rock, never the player's own builds.
            if (MetaData(ent).EntityPrototype?.ID == rockId)
                QueueDel(ent);
        }
    }

    /// <summary>Spawns an anchored entity at a tile, but never stacks a duplicate of the same prototype there.</summary>
    private void SpawnAnchoredOnce(string proto, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var ent in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (MetaData(ent).EntityPrototype?.ID == proto)
                return;
        }

        Spawn(proto, _map.GridTileToLocal(gridUid, grid, tile));
    }

    private bool TileHasAnchored(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var _ in _map.GetAnchoredEntities(gridUid, grid, tile))
            return true;
        return false;
    }

    /// <summary>Find the map directly above a stone level (its "source" / parent map) so we can read its settings.</summary>
    private EntityUid GetSourceMapForStone(EntityUid stoneMap)
    {
        if (TryComp<CMUZLevelMapComponent>(stoneMap, out var z) && z.MapAbove is { } above)
            return above;
        return stoneMap;
    }

    public override void Update(float frameTime)
    {
        if (!GloballyEnabled || _timing.CurTime < _nextStream)
            return;

        _nextStream = _timing.CurTime + StreamInterval;

        // Per-chunk-on-approach: generate stone around every player standing on a stone level.
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (xform.MapUid is not { } mapUid ||
                !TryComp<ZGeneratedStoneComponent>(mapUid, out var stone) ||
                !_gridQuery.TryComp(stone.StoneGrid, out var grid))
            {
                continue;
            }

            var settings = GetSettings(GetSourceMapForStone(mapUid));
            var size = Math.Max(2, settings.ChunkSize);
            var tile = _map.TileIndicesFor(stone.StoneGrid, grid, xform.Coordinates);

            for (var cx = -StreamRadiusChunks; cx <= StreamRadiusChunks; cx++)
            {
                for (var cy = -StreamRadiusChunks; cy <= StreamRadiusChunks; cy++)
                {
                    EnsureChunkAt((mapUid, stone), mapUid, tile + new Vector2i(cx * size, cy * size));
                }
            }
        }
    }

    private static int FloorDiv(int a, int b) => (int) Math.Floor((double) a / b);
}
