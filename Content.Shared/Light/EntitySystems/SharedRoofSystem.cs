using System.Diagnostics.Contracts;
using Content.Shared._RMC14.Areas;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.Light.EntitySystems;

/// <summary>
/// Handles the roof flag for tiles that gets used for the RoofOverlay.
/// </summary>
public abstract partial class SharedRoofSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedTransformSystem _transform = default!; // CMU14

    private HashSet<Entity<IsRoofComponent>> _roofSet = new();
    private readonly HashSet<Entity<IsRoofComponent>> _renderRoofSet = new(); // CMU14

    /// <summary>
    /// Returns whether the specified tile is roof-occupied.
    /// </summary>
    /// <returns>Returns false if no data or not rooved.</returns>
    [Pure]
    public bool IsRooved(Entity<MapGridComponent, RoofComponent> grid, Vector2i index)
    {
        var roof = grid.Comp2;
        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);

        if (roof.Data.TryGetValue(chunkOrigin, out var bitMask))
        {
            var chunkRelative = SharedMapSystem.GetChunkRelative(index, RoofComponent.ChunkSize);
            var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);

            var isRoof = (bitMask & bitFlag) == bitFlag;

            // Early out, otherwise check for components on tile.
            if (isRoof)
                return true;
        }

        _roofSet.Clear();
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, index, _roofSet);

        foreach (var isRoofEnt in _roofSet)
        {
            if (!isRoofEnt.Comp.Enabled)
                continue;

            return true;
        }

        return false;
    }

    /// <summary>
    /// CMU14: Conservatively marks tiles that may intersect entity roofs for this render.
    /// One broad-phase query replaces per-tile searches across the open ground in a
    /// wide camera view. Marked tiles still use the normal exact lookup and priority.
    /// Rebuild each pass so movement, toggles, containers and replication stay current.
    /// </summary>
    public void GetEntityRoofTiles(Entity<MapGridComponent> grid, Box2 localBounds, HashSet<Vector2i> tiles)
    {
        tiles.Clear();
        _renderRoofSet.Clear();
        var size = grid.Comp.TileSize;
        // Include the boundary tiles and their normal tile-query enlargement.
        var queryBounds = localBounds.Enlarged(size + EntityLookupSystem.TileEnlargementRadius);
        _lookup.GetLocalEntitiesIntersecting(grid.Owner, queryBounds, _renderRoofSet,
            EntityLookupSystem.DefaultFlags | LookupFlags.Approximate);
        var inverse = _transform.GetInvWorldMatrix(grid.Owner);
        foreach (var (uid, roof) in _renderRoofSet)
        {
            if (!roof.Enabled) continue;
            var bounds = inverse.TransformBox(_lookup.GetWorldAABB(uid)).Enlarged(size);
            var left = (int) MathF.Floor(Math.Max(bounds.Left, queryBounds.Left) / size);
            var right = (int) MathF.Floor(Math.Min(bounds.Right, queryBounds.Right) / size);
            var bottom = (int) MathF.Floor(Math.Max(bounds.Bottom, queryBounds.Bottom) / size);
            var top = (int) MathF.Floor(Math.Min(bounds.Top, queryBounds.Top) / size);
            for (var x = left; x <= right; x++)
            for (var y = bottom; y <= top; y++)
                tiles.Add(new Vector2i(x, y));
        }
        _renderRoofSet.Clear();
    }

    // CMU14 method
    [Pure]
    public Color? GetColor(Entity<MapGridComponent, RoofComponent> grid, Vector2i index) => GetColor(grid, index, true);

    /// <param name="queryEntities">Whether this tile is in the current <see cref="GetEntityRoofTiles"/> batch.</param>
    // CMU14 method
    [Pure]
    public Color? GetColor(Entity<MapGridComponent, RoofComponent> grid, Vector2i index, bool queryEntities)
    {
        var roof = grid.Comp2;
        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);

        if (roof.Data.TryGetValue(chunkOrigin, out var bitMask))
        {
            var chunkRelative = SharedMapSystem.GetChunkRelative(index, RoofComponent.ChunkSize);
            var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);

            var isRoof = (bitMask & bitFlag) == bitFlag;

            // Early out, otherwise check for components on tile.
            if (isRoof)
            {
                return roof.Color;
            }
        }

        // CMU14: run exact checks only for candidates from the conservative roof batch.
        if (queryEntities)
        {
            _roofSet.Clear();
            _lookup.GetLocalEntitiesIntersecting(grid.Owner, index, _roofSet);

            foreach (var isRoofEnt in _roofSet)
            {
                if (!isRoofEnt.Comp.Enabled)
                    continue;

                return isRoofEnt.Comp.Color ?? roof.Color;
            }
        }

        //RMC14 - This goes last so we can still use upstream methods to define additional roof colors
        if (_area.IsLightBlocked(grid, index))
            return roof.Color;

        return null;
    }

    public void SetRoof(Entity<MapGridComponent?, RoofComponent?> grid, Vector2i index, bool value)
    {
        if (!Resolve(grid, ref grid.Comp1, ref grid.Comp2, false))
            return;

        var chunkOrigin = SharedMapSystem.GetChunkIndices(index, RoofComponent.ChunkSize);
        var roof = grid.Comp2;

        if (!roof.Data.TryGetValue(chunkOrigin, out var chunkData))
        {
            // No value to remove so leave it.
            if (!value)
            {
                return;
            }

            chunkData = 0;
        }

        var chunkRelative = SharedMapSystem.GetChunkRelative(index, RoofComponent.ChunkSize);
        var bitFlag = (ulong) 1 << (chunkRelative.X + chunkRelative.Y * RoofComponent.ChunkSize);

        if (value)
        {
            // Already set
            if ((chunkData & bitFlag) == bitFlag)
                return;

            chunkData |= bitFlag;
        }
        else
        {
            // Not already set
            if ((chunkData & bitFlag) == 0x0)
                return;

            chunkData &= ~bitFlag;
        }

        roof.Data[chunkOrigin] = chunkData;
        Dirty(grid.Owner, roof);
    }
}
