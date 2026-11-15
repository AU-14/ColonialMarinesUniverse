// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2026 wray-git
using System.Numerics;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server.CMU14.ZLevelBuilding;

/// <summary>
/// Where what has fallen through a collapsed floor comes to rest: it falls on until there is a solid tile under it, not just to the next level. A level has something
/// to land on when the tile at that spot is not empty (a floor, or the floor of the cave); over an empty tile it goes on to the level below, and so on.
/// </summary>
public sealed class ZLandingSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    // The levels are a short stack: this only keeps a broken chain of maps from looping.
    private const int MaxDrop = 16;

    /// <summary>
    /// The map that something dropped onto <paramref name="map"/> at <paramref name="worldPos"/> lands on: this one if it has a solid tile there, otherwise the first
    /// level below that has. If no level has one, the last level is where it ends up (the bottom of the stack).
    /// </summary>
    public EntityUid FindLandingMap(EntityUid map, Vector2 worldPos)
    {
        var current = map;
        for (var i = 0; i < MaxDrop; i++)
        {
            if (HasSolidTile(current, worldPos))
                return current;

            if (!TryComp<CMUZLevelMapComponent>(current, out var z) || z.MapBelow is not { } below || TerminatingOrDeleted(below))
                return current;

            current = below;
        }

        return current;
    }

    private readonly HashSet<Entity<ZFallenDebrisComponent>> _debris = new();

    /// <summary>
    /// What has fallen before and lies loose on this tile (it is not anchored, so the anchored lists of a collapse never hold it). It is brought down again when the floor under it
    /// gives way, as often as that happens.
    /// </summary>
    public void GetFallenDebrisOnTile(EntityUid gridUid, MapGridComponent grid, Vector2i tile, List<EntityUid> result)
    {
        var center = _map.GridTileToWorld(gridUid, grid, tile);
        var box = Box2.CenteredAround(center.Position, new Vector2(grid.TileSize, grid.TileSize));

        _debris.Clear();
        _lookup.GetEntitiesIntersecting(center.MapId, box, _debris, LookupFlags.Uncontained | LookupFlags.Dynamic | LookupFlags.Static | LookupFlags.Sundries);
        foreach (var debris in _debris)
        {
            if (!TerminatingOrDeleted(debris) && box.Contains(_transform.GetWorldPosition(debris)))
                result.Add(debris.Owner);
        }

        _debris.Clear();
    }

    private bool HasSolidTile(EntityUid map, Vector2 worldPos)
    {
        if (!TryComp<MapComponent>(map, out var mapComp))
            return false;

        var coords = new MapCoordinates(worldPos, mapComp.MapId);
        if (!_map.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        return !_map.GetTileRef(gridUid, grid, coords).Tile.IsEmpty;
    }

    /// <summary>
    /// On one level a thing falls only once: this notes that it fell from <paramref name="fromMap"/>, and says false when it has already fallen from there. A thing that is
    /// not marked yet is marked.
    /// </summary>
    public bool TryMarkFall(EntityUid ent, EntityUid? fromMap)
    {
        var debris = EnsureComp<ZFallenDebrisComponent>(ent);
        return fromMap is not { } map || debris.FellFrom.Add(map);
    }

    /// <summary>True when something solid and anchored (a rock, a wall, a table) already stands on the tile it would land on; it would sit inside it, so it must lose its collision.</summary>
    public bool LandsInsideSolid(EntityUid map, Vector2 worldPos)
    {
        if (!TryComp<MapComponent>(map, out var mapComp))
            return false;

        var coords = new MapCoordinates(worldPos, mapComp.MapId);
        if (!_map.TryFindGridAt(coords, out var gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);
        foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (!TryComp<FixturesComponent>(anchored, out var fixtures))
                continue;

            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (fixture.Hard && fixture.CollisionLayer != 0)
                    return true;
            }
        }

        return false;
    }
}
