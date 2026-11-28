using Content.Shared.Atmos;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Wall;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

/// <summary>
/// Whether a room on a z-level is closed in: a bounded flood fill over floor tiles that are each roofed from the
/// level above, stopped by walls and closed doors.
/// </summary>
/// <remarks>
/// <para>The fill fails (the room is not enclosed) when it reaches:</para>
/// <list type="bullet">
///   <item>more than <c>maxArea</c> tiles;</item>
///   <item>a tile that is missing or fall-through (nothing to stand on);</item>
///   <item>a tile not roofed: <see cref="CMUSharedZLevelsSystem.IsCeiled"/> with <see cref="CMUZCeilingMode.Roof"/>;</item>
///   <item>an entity opening (<see cref="CMUZLevelFallThroughComponent"/>: a pit, an open hatch);</item>
///   <item>a stair or other high ground (<see cref="CMUZLevelHighGroundComponent"/>, <see cref="ZStairComponent"/>);</item>
///   <item>an open door: a <see cref="DoorComponent"/> whose state is Open, Opening or Closing (not sealed).</item>
/// </list>
/// <para>
/// Bounds (not added, not crossed): a wall, which is an anchored <see cref="ZLevelWallSupportComponent"/> (the z-level
/// "fills its level" predicate, also used by z-physics) or <see cref="WallComponent"/>; and a closed door
/// (Closed, Welded, Denying, Emagging). <c>AirtightComponent</c> is server-only, so windows and other airtight
/// structures without those components are not bounds here.
/// </para>
/// <para>Re-validation on tile or door changes is left to the consumer.</para>
/// </remarks>
public sealed class CMUZEnclosureSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    private readonly Stack<Vector2i> _toVisit = new();
    private readonly HashSet<Vector2i> _bounds = new();

    /// <summary>
    /// Fills <paramref name="result"/> with the enclosed room containing <paramref name="start"/> on
    /// <paramref name="grid"/>, roofed from the level above <paramref name="map"/>. Returns false, with
    /// <paramref name="result"/> holding the partial fill, if the room is not enclosed.
    /// </summary>
    [PublicAPI]
    public bool TryGetEnclosure(EntityUid map, Entity<MapGridComponent> grid, Vector2i start, int maxArea, HashSet<Vector2i> result)
    {
        result.Clear();
        _toVisit.Clear();
        _bounds.Clear();

        try
        {
            _toVisit.Push(start);
            while (_toVisit.Count > 0)
            {
                var current = _toVisit.Pop();
                if (result.Contains(current) || _bounds.Contains(current))
                    continue;

                switch (Classify(map, grid, current))
                {
                    case TileKind.Bound:
                        // The start tile itself must be inside the room.
                        if (current == start)
                            return false;

                        _bounds.Add(current);
                        continue;
                    case TileKind.Fail:
                        return false;
                }

                result.Add(current);
                if (result.Count > maxArea)
                    return false;

                for (var i = 0; i < 4; i++)
                {
                    var neighbor = current.Offset((AtmosDirection) (1 << i));
                    if (!result.Contains(neighbor) && !_bounds.Contains(neighbor))
                        _toVisit.Push(neighbor);
                }
            }

            return result.Count > 0;
        }
        finally
        {
            _toVisit.Clear();
            _bounds.Clear();
        }
    }

    private TileKind Classify(EntityUid map, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var bound = false;
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (TryComp<DoorComponent>(uid, out var door))
            {
                if (IsOpenDoor(door.State))
                    return TileKind.Fail;

                bound = true;
                continue;
            }

            if (HasComp<ZLevelWallSupportComponent>(uid) || HasComp<WallComponent>(uid))
            {
                bound = true;
                continue;
            }

            if (HasComp<CMUZLevelHighGroundComponent>(uid) || HasComp<ZStairComponent>(uid))
                return TileKind.Fail;
        }

        // A wall or closed door bounds the room even with an opening under it.
        if (bound)
            return TileKind.Bound;

        if (!_map.TryGetTileRef(grid.Owner, grid.Comp, tile, out var tileRef) ||
            CMUZLevelOpeningCache.IsFallThroughTile(tileRef.Tile, _tileDefinitions))
        {
            return TileKind.Fail;
        }

        if (_zLevels.HasEntityOpening(grid, tile))
            return TileKind.Fail;

        var worldPos = _map.GridTileToWorldPos(grid.Owner, grid.Comp, tile);
        return _zLevels.IsCeiled(map, worldPos, CMUZCeilingMode.Roof) ? TileKind.Floor : TileKind.Fail;
    }

    private static bool IsOpenDoor(DoorState state)
    {
        return state is DoorState.Open or DoorState.Opening or DoorState.Closing;
    }

    private enum TileKind : byte
    {
        Floor,
        Bound,
        Fail,
    }
}
