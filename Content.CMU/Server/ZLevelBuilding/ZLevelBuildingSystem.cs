// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Ghost;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Tag;
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
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly ZBorderSyncSystem _borderSync = default!;

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
    private EntityQuery<GhostComponent> _ghostQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _ghostQuery = GetEntityQuery<GhostComponent>();

        // Keyed by round-scoped map uids; drop with the round so stale entries never accumulate.
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ => _reflectedBorderChunks.Clear());
        _borderSync.ListsChanged += ClearReflectedBorderChunks;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _borderSync.ListsChanged -= ClearReflectedBorderChunks;
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

        // The map entity ITSELF must be the grid (a "map-grid", like authored z-level maps and biome planets):
        // the CMU z-movement code resolves tiles/high-grounds via MapGridComponent ON the map entity. A child
        // grid entity leaves the level invisible to DistanceToGround/HasTileAbove, so stairs and falling
        // silently stop working the moment a player stands on a lazily created level.
        EnsureComp<MapGridComponent>(newMapUid);

        if (!_zLevels.TryAddMapsIntoZNetwork(network.Value, new Dictionary<EntityUid, int> { [newMapUid] = depth - 1 }))
        {
            Del(newMapUid);
            return false;
        }

        var stone = EnsureComp<ZGeneratedStoneComponent>(newMapUid);
        stone.StoneGrid = newMapUid;
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

            // Map-grid, not a child grid: the CMU z-movement code only sees tiles/high-grounds on the map
            // entity's own MapGridComponent (see CreateStoneBelow).
            EnsureComp<MapGridComponent>(newMap);

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
            EnsureComp<MapGridComponent>(targetMap);
            targetGrid = targetMap;
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

        // Prefer a grid already under the player's x/y on that level; otherwise make the level a map-grid
        // (the CMU z-movement code only sees tiles on the map entity's own grid - see CreateStoneBelow).
        EntityUid stoneGrid;
        if (_mapManager.TryFindGridAt(new MapCoordinates(worldPos, belowMapComp.MapId), out var foundGrid, out _))
        {
            stoneGrid = foundGrid;
        }
        else
        {
            EnsureComp<MapGridComponent>(belowMap);
            stoneGrid = belowMap;
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

                // Map-border reflection: if an indestructible border wall stands at this spot on the level
                // above, mirror THAT wall here instead of a mineable rock, so the playfield boundary continues
                // downward. Because chunks (and levels) only generate as players actually reach them, the
                // border never propagates into levels nobody has entered.
                if (TryGetBorderWallAbove(mapUid, _transform.ToMapCoordinates(coords).Position, out var wallProto))
                {
                    Spawn(wallProto, coords);
                    continue;
                }

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

        // NOTE: pass the STONE level itself (below.Owner), not the digger's current map - EnsureChunkAt
        // resolves generation settings relative to the map it is handed.
        EnsureChunkAt(below, below.Owner, landingTile);
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

    /// <summary>An indestructible map-border wall: tagged as a wall but with no Damageable at all (the
    /// CMBaseWallInvincible family). These mark the playfield boundary.</summary>
    private bool IsIndestructibleWall(EntityUid uid)
    {
        return _tag.HasTag(uid, "Wall") && !HasComp<Content.Shared.Damage.DamageableComponent>(uid);
    }

    /// <summary>If an indestructible border wall stands at <paramref name="worldPos"/> on the level directly
    /// above <paramref name="stoneMap"/>, returns its prototype id so the boundary can be mirrored down.</summary>
    private bool TryGetBorderWallAbove(EntityUid stoneMap, Vector2 worldPos, out string wallProto)
    {
        wallProto = string.Empty;
        var source = GetSourceMapForStone(stoneMap);
        if (source == stoneMap || !TryComp<MapComponent>(source, out var sourceMapComp))
            return false;

        var coords = new MapCoordinates(worldPos, sourceMapComp.MapId);
        if (!_mapManager.TryFindGridAt(coords, out var sourceGridUid, out var sourceGrid))
            return false;

        var tile = _map.TileIndicesFor(sourceGridUid, sourceGrid, coords);
        foreach (var anchored in _map.GetAnchoredEntities(sourceGridUid, sourceGrid, tile))
        {
            // Which prototypes count as borders is admin-editable (Z-Sync Lists tool); the default is the
            // CMBaseWallInvincible family, minus anything blacklisted (e.g. dropship walls sharing the parent).
            if (MetaData(anchored).EntityPrototype is { } proto && _borderSync.ShouldReflect(proto.ID))
            {
                wallProto = proto.ID;
                return true;
            }
        }

        return false;
    }

    // Border reflection on NON-stone z-levels (player-built upper platforms and void levels): which chunks of
    // which map have already been mirrored, so each area is only processed once. Keyed by round-scoped map uids.
    private readonly Dictionary<EntityUid, HashSet<Vector2i>> _reflectedBorderChunks = new();

    private void ClearReflectedBorderChunks()
    {
        _reflectedBorderChunks.Clear();
    }

    /// <summary>Side length (in tiles) of the areas the border-reflection pass processes and remembers.</summary>
    private const int BorderReflectChunk = 8;

    /// <summary>
    /// Mirrors indestructible map-border walls onto the z-level a player is standing on, from the neighbouring
    /// level toward the ground (upper levels copy from below, underground copies from above). Runs only around
    /// actual players, chunk by chunk, so the boundary appears exactly when someone first enters an area of a
    /// level and never cascades into levels nobody visits.
    /// </summary>
    private void ReflectBorderAroundPlayer(EntityUid mapUid, TransformComponent xform)
    {
        if (!TryComp<CMUZLevelMapComponent>(mapUid, out var z) || z.Depth == 0)
            return;

        var source = z.Depth > 0 ? z.MapBelow : z.MapAbove;
        if (source is not { } sourceMap || !TryComp<MapComponent>(sourceMap, out var sourceMapComp))
            return;

        if (!TryComp<MapComponent>(mapUid, out var mapComp))
            return;

        var done = _reflectedBorderChunks.TryGetValue(mapUid, out var set)
            ? set
            : _reflectedBorderChunks[mapUid] = new HashSet<Vector2i>();

        var worldPos = _transform.GetWorldPosition(xform);
        var playerChunk = new Vector2i(
            FloorDiv((int) MathF.Floor(worldPos.X), BorderReflectChunk),
            FloorDiv((int) MathF.Floor(worldPos.Y), BorderReflectChunk));

        for (var cx = -1; cx <= 1; cx++)
        {
            for (var cy = -1; cy <= 1; cy++)
            {
                var chunk = playerChunk + new Vector2i(cx, cy);
                if (!done.Add(chunk))
                    continue;

                ReflectBorderChunk(mapUid, mapComp, sourceMap, sourceMapComp, chunk);
            }
        }
    }

    /// <summary>Mirrors every border wall found in one world-space chunk of <paramref name="sourceMap"/> onto
    /// the same spots of <paramref name="targetMap"/> (laying plating under each wall if the tile is empty).</summary>
    private void ReflectBorderChunk(EntityUid targetMap, MapComponent targetMapComp, EntityUid sourceMap, MapComponent sourceMapComp, Vector2i chunk)
    {
        for (var x = 0; x < BorderReflectChunk; x++)
        {
            for (var y = 0; y < BorderReflectChunk; y++)
            {
                var worldPos = new Vector2(
                    chunk.X * BorderReflectChunk + x + 0.5f,
                    chunk.Y * BorderReflectChunk + y + 0.5f);

                var sourceCoords = new MapCoordinates(worldPos, sourceMapComp.MapId);
                if (!_mapManager.TryFindGridAt(sourceCoords, out var sourceGridUid, out var sourceGrid))
                    continue;

                var sourceTile = _map.TileIndicesFor(sourceGridUid, sourceGrid, sourceCoords);
                string? wallProto = null;
                foreach (var anchored in _map.GetAnchoredEntities(sourceGridUid, sourceGrid, sourceTile))
                {
                    // Admin-editable border set (Z-Sync Lists tool) - see TryGetBorderWallAbove.
                    if (MetaData(anchored).EntityPrototype is { } proto && _borderSync.ShouldReflect(proto.ID))
                    {
                        wallProto = proto.ID;
                        break;
                    }
                }

                if (wallProto == null)
                    continue;

                // Only mirror onto grids that already exist on the target level; a level with no grid here has
                // nothing to bound yet (and reflecting would otherwise create endless grids on void levels).
                var targetCoords = new MapCoordinates(worldPos, targetMapComp.MapId);
                if (!_mapManager.TryFindGridAt(targetCoords, out var targetGridUid, out var targetGrid))
                    continue;

                var targetTile = _map.TileIndicesFor(targetGridUid, targetGrid, targetCoords);

                // Anchoring needs a real tile under the wall; the wall sprite fully covers the plating anyway.
                if ((!_map.TryGetTileRef(targetGridUid, targetGrid, targetTile, out var tileRef) || tileRef.Tile.IsEmpty)
                    && _tileDef.TryGetDefinition("Plating", out var plating))
                {
                    _map.SetTile(targetGridUid, targetGrid, targetTile, new Tile(plating.TileId));
                }

                SpawnAnchoredOnce(wallProto, targetGridUid, targetGrid, targetTile);
            }
        }
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
            // Ghosts (dead/observers) drift the map freely; only real spawned players should carve caves.
            if (_ghostQuery.HasComponent(uid))
                continue;

            if (xform.MapUid is not { } mapUid)
                continue;

            if (!TryComp<ZGeneratedStoneComponent>(mapUid, out var stone) ||
                !_gridQuery.TryComp(stone.StoneGrid, out var grid))
            {
                // Non-stone z-levels (upper platforms, void levels) still mirror the map-border walls around
                // players; stone levels get theirs during chunk generation instead.
                ReflectBorderAroundPlayer(mapUid, xform);
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
