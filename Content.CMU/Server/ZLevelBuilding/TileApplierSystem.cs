// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
// SPDX-License-Identifier: AGPL-3.0-only
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.ZLevelBuilding;

/// <summary>
/// Building overhaul (z-level): turns a constructed <see cref="TileApplierComponent"/> entity into an actual grid
/// tile, then deletes the entity. This is how the construction menu's Tiles section "builds" floor tiles.
///
/// To give tiles structural integrity (tiles cannot carry components themselves), each tile laid on an UPPER
/// z-level also gets an invisible <see cref="TileFloorSupportComponent"/> marker so it participates in the
/// support graph; when that marker is collapsed for lacking support, it removes the floor tile under it.
/// </summary>
public sealed class TileApplierSystem : EntitySystem
{
    [Dependency] private readonly MapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;

    /// <summary>The invisible per-tile support marker spawned on laid floors (see tiles.yml).</summary>
    private const string TileFloorSupportProto = "AU14TileFloorSupport";

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;
    private readonly Queue<(EntityUid Grid, Vector2i Tile)> _pendingTileRemovals = new();
    private readonly HashSet<(EntityUid Grid, Vector2i Tile)> _queuedTileRemovals = new();

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
        SubscribeLocalEvent<TileApplierComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<TileFloorSupportComponent, EntityTerminatingEvent>(OnSupportTerminating);
    }

    private void OnMapInit(Entity<TileApplierComponent> ent, ref MapInitEvent args)
    {
        var xform = Transform(ent);

        if (xform.GridUid is { } gridUid &&
            _gridQuery.TryComp(gridUid, out var grid) &&
            _tileDef.TryGetDefinition(ent.Comp.Tile, out var def))
        {
            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            _map.SetTile(gridUid, grid, tile, new Tile(def.TileId));

            // On an upper z-level the floor needs structural support or it caves in: drop an invisible support
            // marker so the tile is tracked. Ground / underground floors rest on the ground and are inherently
            // stable, so we skip the marker there (and avoid a per-tile entity on every normal floor).
            if (IsUpperLevel(xform.MapUid) && !TileHasFloorSupport(gridUid, grid, tile))
                Spawn(TileFloorSupportProto, _map.GridTileToLocal(gridUid, grid, tile));
        }

        // The tile is laid; the applier entity has done its job.
        QueueDel(ent.Owner);
    }

    /// <summary>When a tile's support marker dies (collapses), remove the floor tile it stood for.</summary>
    private void OnSupportTerminating(Entity<TileFloorSupportComponent> ent, ref EntityTerminatingEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || TerminatingOrDeleted(gridUid) || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (_queuedTileRemovals.Add((gridUid, tile)))
            _pendingTileRemovals.Enqueue((gridUid, tile));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Process only work that existed at the start of this update. SetTile can terminate other support
        // markers, and those callbacks must also escape their current lifecycle traversal before mutating tiles.
        var count = _pendingTileRemovals.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _pendingTileRemovals.Dequeue();
            _queuedTileRemovals.Remove(pending);

            if (TerminatingOrDeleted(pending.Grid) || !_gridQuery.TryComp(pending.Grid, out var grid))
                continue;

            _map.SetTile(pending.Grid, grid, pending.Tile, Tile.Empty);
        }
    }

    /// <summary>True if the map is an upper z-level (depth above the ground). Ground/underground/no-z = false.</summary>
    private bool IsUpperLevel(EntityUid? mapUid)
    {
        return mapUid != null && _zMapQuery.TryComp(mapUid.Value, out var z) && z.Depth > 0;
    }

    private bool TileHasFloorSupport(EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (HasComp<TileFloorSupportComponent>(anchored))
                return true;
        }

        return false;
    }
}
