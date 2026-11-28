using Content.Shared.CMU14.ZLevelBuilding;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// Keeps multi-tile structures whole in a collapse (GraphAudit FLOW-06). A <see cref="ZFootprintComponent"/>
/// structure is anchored on one tile only, so the per-tile collapse paths would leave it floating when another tile
/// it covers gives way. While the structure is anchored this system places an invisible
/// <see cref="ZFootprintCellComponent"/> marker (<see cref="CellPrototype"/>) on each covered non-origin tile; the
/// collapse paths call <see cref="AddCollapsingOwners"/> so a collapsing cell brings its owner down with it once.
/// A cell lost with its tile (a pried floor) is placed again when a floor is laid there again.
/// </summary>
public sealed class ZFootprintCollapseSystem : EntitySystem
{
    /// <summary>The invisible, unsaved cell marker (footprint.yml). It carries ZDestroyedOnCollapse.</summary>
    public const string CellPrototype = "CMUZFootprintCell";

    [Dependency] private SharedMapSystem _map = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    // Anchored footprint owners per grid, so a tile change looks only at the owners of its own grid.
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _ownersByGrid = new();

    // Owners with a covered tile that regained a floor; their missing cells are placed in Update, outside the
    // tile-change callback.
    private readonly HashSet<EntityUid> _ownersToRefill = new();
    private readonly List<EntityUid> _refilling = new();

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<ZFootprintComponent, MapInitEvent>(OnFootprintMapInit);
        SubscribeLocalEvent<ZFootprintComponent, AnchorStateChangedEvent>(OnFootprintAnchorChanged);
        SubscribeLocalEvent<ZFootprintComponent, ComponentShutdown>(OnFootprintShutdown);
        SubscribeLocalEvent<ZFootprintCellComponent, AnchorStateChangedEvent>(OnCellAnchorChanged);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<Content.Shared.GameTicking.RoundRestartCleanupEvent>(_ =>
        {
            _ownersByGrid.Clear();
            _ownersToRefill.Clear();
        });
    }

    public override void Update(float frameTime)
    {
        if (_ownersToRefill.Count == 0)
            return;

        _refilling.Clear();
        _refilling.AddRange(_ownersToRefill);
        _ownersToRefill.Clear();

        foreach (var owner in _refilling)
        {
            if (!TerminatingOrDeleted(owner) && TryComp<ZFootprintComponent>(owner, out var footprint))
                PlaceMissingCells((owner, footprint));
        }
    }

    private void OnFootprintMapInit(Entity<ZFootprintComponent> ent, ref MapInitEvent args)
    {
        if (Transform(ent).Anchored)
            PlaceCells(ent);
    }

    private void OnFootprintAnchorChanged(Entity<ZFootprintComponent> ent, ref AnchorStateChangedEvent args)
    {
        // Before MapInit the MapInit handler places the cells.
        if (args.Anchored && LifeStage(ent) >= EntityLifeStage.MapInitialized)
            PlaceCells(ent);
        else if (!args.Anchored)
            RemoveCells(ent);
    }

    private void OnFootprintShutdown(Entity<ZFootprintComponent> ent, ref ComponentShutdown args)
        => RemoveCells(ent);

    // A cell that lost its tile (or was moved) no longer marks anything; the owner itself is unaffected.
    private void OnCellAnchorChanged(Entity<ZFootprintCellComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (args.Anchored)
            return;

        if (TryComp<ZFootprintComponent>(ent.Comp.Owner, out var owner))
            owner.Cells.Remove(ent.Owner);

        if (!TerminatingOrDeleted(ent))
            QueueDel(ent);
    }

    // A covered tile that regained a floor gets its cell back, so a later collapse there still takes the owner down.
    private void OnTileChanged(ref TileChangedEvent args)
    {
        if (!_ownersByGrid.TryGetValue(args.Entity.Owner, out var owners))
            return;

        foreach (var change in args.Changes)
        {
            if (!change.OldTile.IsEmpty || change.NewTile.IsEmpty)
                continue;

            foreach (var owner in owners)
            {
                if (TryComp<ZFootprintComponent>(owner, out var footprint) && Covers(footprint, change.GridIndices))
                    _ownersToRefill.Add(owner);
            }
        }
    }

    /// <summary>
    /// Adds to <paramref name="collapsing"/> the owner of every footprint cell in it, once, so a collapse path that
    /// drops or deletes the entities anchored on a tile also takes down a multi-tile structure that covers the tile.
    /// Only a cell the owner still lists counts: once the owner has fallen its cells are dropped from that list (even
    /// while they wait for deletion this tick), so a second covered tile collapsing in the same tick cannot move it
    /// again. The cells stay in the list; they carry ZDestroyedOnCollapse and vanish with the floor.
    /// </summary>
    public void AddCollapsingOwners(List<EntityUid> collapsing)
    {
        var count = collapsing.Count;
        for (var i = 0; i < count; i++)
        {
            if (!TryComp<ZFootprintCellComponent>(collapsing[i], out var cell) ||
                TerminatingOrDeleted(cell.Owner) ||
                !TryComp<ZFootprintComponent>(cell.Owner, out var footprint) ||
                !footprint.Cells.Contains(collapsing[i]) ||
                collapsing.Contains(cell.Owner))
                continue;

            collapsing.Add(cell.Owner);
        }
    }

    private static bool Covers(ZFootprintComponent footprint, Vector2i tile)
    {
        var offset = tile - footprint.Origin;
        return offset.X >= 0 && offset.Y >= 0 && offset.X < footprint.Size.X && offset.Y < footprint.Size.Y &&
               (offset.X != 0 || offset.Y != 0);
    }

    private void PlaceCells(Entity<ZFootprintComponent> ent)
    {
        RemoveCells(ent);

        var xform = Transform(ent);
        if (!xform.Anchored || xform.GridUid is not { } gridUid || !_gridQuery.TryComp(gridUid, out var grid))
            return;

        ent.Comp.Grid = gridUid;
        ent.Comp.Origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        if (!_ownersByGrid.TryGetValue(gridUid, out var owners))
            _ownersByGrid[gridUid] = owners = new HashSet<EntityUid>();
        owners.Add(ent.Owner);

        PlaceMissingCells(ent);
    }

    private void PlaceMissingCells(Entity<ZFootprintComponent> ent)
    {
        var xform = Transform(ent);
        if (!xform.Anchored || ent.Comp.Grid is not { } gridUid || xform.GridUid != gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
            return;

        for (var x = 0; x < ent.Comp.Size.X; x++)
        {
            for (var y = 0; y < ent.Comp.Size.Y; y++)
            {
                if (x == 0 && y == 0)
                    continue;

                var tile = ent.Comp.Origin + new Vector2i(x, y);
                if (HasOwnCell(ent, gridUid, grid, tile))
                    continue;

                // A cell can only anchor on a real tile; a placement API validates the footprint beforehand.
                if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
                {
                    Log.Warning($"[zfootprint] {ToPrettyString(ent)} covers empty tile {tile}; no cell placed there.");
                    continue;
                }

                var cellUid = Spawn(CellPrototype, _map.GridTileToLocal(gridUid, grid, tile));
                EnsureComp<ZFootprintCellComponent>(cellUid).Owner = ent.Owner;
                ent.Comp.Cells.Add(cellUid);
            }
        }
    }

    private bool HasOwnCell(Entity<ZFootprintComponent> ent, EntityUid gridUid, MapGridComponent grid, Vector2i tile)
    {
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (ent.Comp.Cells.Contains(anchored) && !TerminatingOrDeleted(anchored))
                return true;
        }

        return false;
    }

    private void RemoveCells(Entity<ZFootprintComponent> ent)
    {
        foreach (var cell in ent.Comp.Cells)
        {
            if (!TerminatingOrDeleted(cell))
                QueueDel(cell);
        }

        ent.Comp.Cells.Clear();

        if (ent.Comp.Grid is { } grid && _ownersByGrid.TryGetValue(grid, out var owners))
        {
            owners.Remove(ent.Owner);
            if (owners.Count == 0)
                _ownersByGrid.Remove(grid);
        }

        ent.Comp.Grid = null;
        _ownersToRefill.Remove(ent.Owner);
    }
}
