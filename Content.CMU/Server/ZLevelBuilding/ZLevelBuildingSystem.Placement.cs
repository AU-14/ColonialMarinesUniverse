using System.Numerics;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>How a structure placed by <see cref="ZLevelBuildingSystem.TryPlaceStructureOnLevel"/> takes part in collapse.</summary>
public enum ZSupportMode : byte
{
    /// <summary>
    /// Like mapped content: the floor gets no support marker, the structure is never <c>PlayerBuilt</c> and never
    /// enrolled, so the support graph cannot collapse it.
    /// </summary>
    Authored,

    /// <summary>
    /// Like player construction: each new floor tile on an upper level gets an <c>AU14TileFloorSupport</c> marker and
    /// the structure is enrolled in the support graph, so it falls when its support goes.
    /// </summary>
    Collapsible,
}

/// <summary>Options for <see cref="ZLevelBuildingSystem.TryPlaceStructureOnLevel"/>.</summary>
public sealed class ZPlacementOptions
{
    /// <summary>
    /// D12 rule: production callers pass <see cref="ZSupportMode.Authored"/> until the TEST-02 idle-collapse soak
    /// (<c>CMUStableGarrisonIdleCollapseTest</c>) is green and the multi-tile collapse (FLOW-06) is approved.
    /// </summary>
    public ZSupportMode Mode = ZSupportMode.Authored;

    /// <summary>
    /// On a generated stone level, clear the generated rock over the footprint. The rock does not block validation,
    /// and is cleared only once the placement has passed it.
    /// </summary>
    public bool ClearCaveRock = false;

    /// <summary>
    /// For <see cref="ZSupportMode.Collapsible"/>: every footprint tile must be one the support solver would hold up,
    /// so a collapsible structure is never placed where it would fall five seconds later. Ignored for
    /// <see cref="ZSupportMode.Authored"/>, which cannot collapse. Set false to place one anyway. This option, not
    /// <see cref="ZFootprintRules.RequireSupport"/> on <see cref="Footprint"/>, decides it, so passing custom rules for
    /// another reason cannot switch the check off.
    /// </summary>
    public bool RequireSupport = true;

    /// <summary>
    /// The footprint rules; null uses the defaults. Their <see cref="ZFootprintRules.RequireSupport"/> is ignored here;
    /// see <see cref="RequireSupport"/>.
    /// </summary>
    public ZFootprintRules? Footprint = null;

    /// <summary>The floor laid on footprint tiles that are empty or fall-through, as the plating applier lays.</summary>
    public string FloorTile = "Plating";
}

public sealed partial class ZLevelBuildingSystem
{
    /// <summary>The invisible per-tile support marker, as <see cref="TileApplierSystem"/> spawns it.</summary>
    private const string PlacementFloorSupportProto = "AU14TileFloorSupport";

    /// <summary>The farthest level a single placement may step to (and so create levels through).</summary>
    public const int MaxPlacementLevelOffset = 16;

    private static readonly ZFootprintRules DefaultPlacementRules = new();

    // ZFootprintSystem's blocking area: any real overlap blocks, rounding noise does not.
    private const float PlacementBlockingArea = 0.001f;

    /// <summary>
    /// ARCH-04: places <paramref name="proto"/> on the level <paramref name="levelOffset"/> steps above (positive) or
    /// below (negative) <paramref name="sourceMap"/>, with its footprint's bottom-left tile under
    /// <paramref name="worldPos"/>. Levels are resolved one step at a time, creating missing ones as the z-stairs do.
    /// </summary>
    /// <remarks>
    /// <para>Order: the map must allow z-building; each level is resolved with <see cref="EnsureNeighborLevel"/>
    /// (never more than one step), which applies the dig-down policy: a new level below is refused while digging
    /// down is disallowed, an existing one is used (placing into its void digs nothing); a stone target
    /// materializes its chunks; the footprint is validated; cave rock is cleared
    /// (<see cref="ZPlacementOptions.ClearCaveRock"/>); the floor is laid; the structure is spawned anchored on the
    /// origin tile; a <see cref="ZSupportMode.Collapsible"/> structure gets its floor markers and is enrolled.</para>
    /// <para>Validation is split around the floor. Before it, while the tiles to be floored are still empty or
    /// fall-through: blocking (ignoring cave rock that will be cleared), ceiling, open sky and, for Collapsible,
    /// support, which the solver predicts for the marker floor such a tile will get. After the floor is laid, and
    /// before any marker or structure exists: solid tile and no tile or entity opening (a pit, an open hatch, a
    /// grate). If that fails, every tile this call set is restored. Nothing is spawned or cleared before both pass,
    /// so a refusal changes nothing but the levels created while resolving, which are kept as empty
    /// infrastructure. Only a failure to anchor the spawned structure, after the rock is cleared, leaves that rock
    /// gone.</para>
    /// <para>No rate limit is applied yet, as there is no bulk caller. A bulk caller must slice its placements per
    /// tick through its own placement queue.</para>
    /// <para>The size comes from the prototype's <see cref="ZFootprintComponent"/>, or 1x1 without one. Fixed
    /// facing: no rotation.</para>
    /// </remarks>
    public bool TryPlaceStructureOnLevel(
        EntityUid sourceMap,
        Vector2 worldPos,
        int levelOffset,
        EntProtoId proto,
        ZPlacementOptions opts,
        out EntityUid placed)
    {
        placed = default;

        if (!IsEnabledOn(sourceMap) || Math.Abs(levelOffset) > MaxPlacementLevelOffset ||
            !_protoMan.TryIndex(proto, out var prototype))
            return false;

        // Resolve the level one step at a time. EnsureNeighborLevel applies the dig-down policy: it refuses to create
        // a level below while digging down is disallowed, but returns one that already exists.
        var map = sourceMap;
        var grid = TryComp<MapComponent>(sourceMap, out var sourceMapComp) &&
                   _mapManager.TryFindGridAt(new MapCoordinates(worldPos, sourceMapComp.MapId), out var sourceGrid, out _)
            ? sourceGrid
            : sourceMap;
        var step = Math.Sign(levelOffset);
        for (var i = 0; i < Math.Abs(levelOffset); i++)
        {
            if (!EnsureNeighborLevel(map, step, grid, worldPos, out var nextMap, out var nextGrid))
                return false;

            map = nextMap;
            grid = nextGrid;
        }

        if (!IsEnabledOn(map) || !_gridQuery.TryComp(grid, out var gridComp) || !TryComp<MapComponent>(map, out var mapComp))
            return false;

        var size = prototype.TryComp<ZFootprintComponent>(out var footprint, EntityManager.ComponentFactory)
            ? footprint.Size
            : Vector2i.One;
        if (size.X <= 0 || size.Y <= 0)
            return false;

        var origin = _map.TileIndicesFor(grid, gridComp, new MapCoordinates(worldPos, mapComp.MapId));

        // Terrain checks must see materialized stone, never an unloaded chunk. The rock is not cleared yet: a refusal
        // below must leave the cave exactly as it was.
        List<string>? caveRock = null;
        HashSet<Vector2i>? existingChunks = null;
        var chunkSize = 0;
        if (TryComp<ZGeneratedStoneComponent>(map, out var stone))
        {
            // A square around the footprint's centre tile that covers the whole footprint.
            var centre = origin + new Vector2i((size.X - 1) / 2, (size.Y - 1) / 2);
            var radius = Math.Max(size.X - 1 - (size.X - 1) / 2, size.Y - 1 - (size.Y - 1) / 2);
            chunkSize = Math.Max(2, GetLevelSettings(map).ChunkSize);
            existingChunks = GeneratedChunksAround(stone, centre, radius, chunkSize);
            PrepareCavernConstruction(map, _map.GridTileToWorldPos(grid, gridComp, centre), radius, clearRock: false);
            if (opts.ClearCaveRock && stone.StoneGrid == grid)
                caveRock = GetGeneratedRockIds(GetLevelSettings(map), map);
        }

        // Resolved per call rather than injected: ZFootprintSystem depends on ZLevelSupportSystem, which depends on
        // this system.
        var footprints = EntityManager.System<ZFootprintSystem>();
        var rules = opts.Footprint ?? DefaultPlacementRules;
        var gridEnt = new Entity<MapGridComponent>(grid, gridComp);

        // Before the floor: blocking, ceiling, open sky, and (Collapsible only) support, judged while the tiles to
        // be floored are still empty or fall-through, so the solver predicts the marker floor they will get.
        // Rock that will be cleared does not block; everything else on its tile still does.
        var preFloorRules = new ZFootprintRules
        {
            RequireSolidTile = false,
            RejectOpenings = false,
            BlockMask = caveRock == null ? rules.BlockMask : CollisionGroup.None,
            RequireCeiling = rules.RequireCeiling,
            RequireOpenSky = rules.RequireOpenSky,
            RequireSupport = opts.Mode == ZSupportMode.Collapsible && opts.RequireSupport,
        };
        if (!footprints.ValidateFootprint(map, gridEnt, origin, size, preFloorRules))
            return false;

        if (caveRock != null && rules.BlockMask != CollisionGroup.None)
        {
            List<string> ignoredRock = caveRock;
            for (var x = 0; x < size.X; x++)
            for (var y = 0; y < size.Y; y++)
            {
                var tile = origin + new Vector2i(x, y);
                if (_turf.IsTileBlocked(grid, tile, rules.BlockMask, gridComp, minIntersectionArea: PlacementBlockingArea,
                        ignore: uid => MetaData(uid).EntityPrototype?.ID is { } id && ignoredRock.Contains(id)))
                    return false;
            }
        }

        if (!_tileDef.TryGetDefinition(opts.FloorTile, out var floorDef))
            return false;

        // Lay the floor on empty and fall-through tiles, remembering what was there.
        var floor = new Tile(floorDef.TileId);
        var previous = new List<(Vector2i GridIndices, Tile Tile)>();
        var laid = new List<(Vector2i GridIndices, Tile Tile)>();
        for (var x = 0; x < size.X; x++)
        {
            for (var y = 0; y < size.Y; y++)
            {
                var tile = origin + new Vector2i(x, y);
                var old = _map.TryGetTileRef(grid, gridComp, tile, out var tileRef) ? tileRef.Tile : Tile.Empty;
                if (!old.IsEmpty && !CMUZLevelOpeningCache.IsFallThroughTile(old, _tileDef))
                    continue;

                previous.Add((tile, old));
                laid.Add((tile, floor));
            }
        }

        if (laid.Count > 0)
        {
            if (opts.Mode == ZSupportMode.Authored)
            {
                _map.SetTiles(grid, gridComp, laid);
            }
            else
            {
                foreach (var (tile, newTile) in laid)
                    _map.SetTile(grid, gridComp, tile, newTile);
            }
        }

        // After the floor: solid tile and no tile or entity opening. Blocking, ceiling, sky and support were judged
        // before it, and laying a floor changes none of them (a marker-less plating would only read as authored floor).
        var postFloorRules = new ZFootprintRules
        {
            RequireSolidTile = rules.RequireSolidTile,
            RejectOpenings = rules.RejectOpenings,
            BlockMask = CollisionGroup.None,
        };
        if (!footprints.ValidateFootprint(map, gridEnt, origin, size, postFloorRules))
        {
            RollBackPlacementFloor(grid, gridComp, previous);
            return false;
        }

        // Validation passed: only now clear the cave rock under the footprint. Rock of a chunk that already existed
        // is a lost support, as for any other excavation (GraphAudit ECS-02).
        if (caveRock != null)
        {
            for (var x = 0; x < size.X; x++)
            for (var y = 0; y < size.Y; y++)
            {
                var tile = origin + new Vector2i(x, y);
                ClearRockAt(gridEnt, tile, caveRock, !existingChunks!.Contains(ChunkOf(tile, chunkSize)));
            }
        }

        placed = Spawn(proto, _map.GridTileToLocal(grid, gridComp, origin));
        var placedXform = Transform(placed);
        if (!placedXform.Anchored && !_transform.AnchorEntity(placed, placedXform))
        {
            Del(placed);
            placed = default;
            RollBackPlacementFloor(grid, gridComp, previous);
            return false;
        }

        if (opts.Mode != ZSupportMode.Collapsible)
            return true;

        // As TileApplierSystem: only a new floor on an upper level carries a support marker.
        if (TryComp<CMUZLevelMapComponent>(map, out var zMap) && zMap.Depth > 0)
        {
            foreach (var (tile, _) in laid)
            {
                if (!PlacementTileHasFloorSupport(grid, gridComp, tile))
                    Spawn(PlacementFloorSupportProto, _map.GridTileToLocal(grid, gridComp, tile));
            }
        }

        // A prototype that is already a support (a beam) keeps its own span and kind; a wall projects as a
        // player-built wall does; anything else holds only itself.
        int span;
        bool isVertical;
        if (TryComp<StructuralSupportComponent>(placed, out var ownSupport))
        {
            span = ownSupport.CantileverSpan;
            isVertical = ownSupport.IsVerticalSupport;
        }
        else if (HasComp<ZLevelWallSupportComponent>(placed))
        {
            span = ZLevelWallSupportComponent.CantileverSpan;
            isVertical = true;
        }
        else
        {
            span = 1;
            isVertical = false;
        }

        // Resolved here rather than injected: ZLevelSupportSystem already depends on this system.
        EntityManager.System<ZLevelSupportSystem>().Enroll(placed, span, isVertical);
        return true;
    }

    private void RollBackPlacementFloor(EntityUid grid, MapGridComponent gridComp, List<(Vector2i GridIndices, Tile Tile)> previous)
    {
        if (previous.Count > 0)
            _map.SetTiles(grid, gridComp, previous);
    }

    private bool PlacementTileHasFloorSupport(EntityUid grid, MapGridComponent gridComp, Vector2i tile)
    {
        foreach (var anchored in _map.GetAnchoredEntities(grid, gridComp, tile))
        {
            if (!TerminatingOrDeleted(anchored) && HasComp<TileFloorSupportComponent>(anchored))
                return true;
        }

        return false;
    }
}
