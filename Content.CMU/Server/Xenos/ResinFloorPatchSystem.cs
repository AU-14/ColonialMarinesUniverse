// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using Content.Shared._AU14.Xenos;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Xenos;

/// <summary>
/// Lays and removes the floor tile under a resin patch, so the hive can bridge holes punched by cave-ins and
/// marines can reopen them by cutting the resin back out.
/// </summary>
public sealed class ResinFloorPatchSystem : EntitySystem
{
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ResinFloorPatchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ResinFloorPatchComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMapInit(Entity<ResinFloorPatchComponent> patch, ref MapInitEvent args)
    {
        if (!TryGetTile(patch, out var grid, out var indices))
            return;

        // Only fill genuine holes. Built over existing floor this does nothing, and the floor is then left
        // alone when the resin dies.
        if (!_map.TryGetTileRef(grid.Owner, grid.Comp, indices, out var tileRef) || !tileRef.Tile.IsEmpty)
            return;

        if (_tileDef[patch.Comp.Tile] is not { } def)
            return;

        _map.SetTile(grid.Owner, grid.Comp, indices, new Tile(def.TileId));
        patch.Comp.PlacedTile = true;

        // The tile did not exist when this entity spawned, so its anchor attempt would have been refused.
        // Anchor now that there is something to anchor to.
        var xform = Transform(patch);
        if (!xform.Anchored)
            _transform.AnchorEntity(patch.Owner, xform);
    }

    private void OnTerminating(Entity<ResinFloorPatchComponent> patch, ref EntityTerminatingEvent args)
    {
        if (!patch.Comp.PlacedTile)
            return;

        if (!TryGetTile(patch, out var grid, out var indices))
            return;

        // Reopen the hole. Cutting the resin should cost the hive the crossing, not hand the marines a free
        // permanent floor.
        _map.SetTile(grid.Owner, grid.Comp, indices, Tile.Empty);
    }

    private bool TryGetTile(EntityUid uid, out Entity<MapGridComponent> grid, out Vector2i indices)
    {
        grid = default;
        indices = default;

        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var gridComp))
            return false;

        grid = (gridUid, gridComp);
        indices = _map.TileIndicesFor(gridUid, gridComp, xform.Coordinates);
        return true;
    }
}
