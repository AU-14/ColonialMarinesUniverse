using System.Numerics;
using Content.Shared._RMC14.Areas;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Damage.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Whitelist;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.CMU14.DropPod;

/// <summary>
/// Finds where a drop pod lands near a planet tactical map tile.
/// </summary>
public sealed partial class CMUDropPodLandingSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    private EntityQuery<DamageableComponent> _damageableQuery;

    public override void Initialize()
    {
        _damageableQuery = GetEntityQuery<DamageableComponent>();
    }

    /// <summary>
    /// Picks a random landable tile within <paramref name="radius"/> tiles of <paramref name="target"/>.
    /// Pods land on <paramref name="depth"/> unless blocking walls are in the way, in which case they land on the level above.
    /// </summary>
    public bool TryFindLanding(
        EntityUid planetMap,
        Vector2i target,
        float radius,
        int attempts,
        int depth,
        EntityWhitelist? blockers,
        bool blockersIndestructible,
        bool respectAreas,
        out EntityCoordinates landing)
    {
        landing = default;
        if (!TryComp(planetMap, out MapGridComponent? planetGrid))
            return false;

        var center = _map.GridTileToWorldPos(planetMap, planetGrid, target);
        for (var i = 0; i < Math.Max(1, attempts); i++)
        {
            var offset = Vector2.Zero;
            if (radius > 0)
                offset = _random.NextAngle().ToVec() * radius * MathF.Sqrt(_random.NextFloat());

            var tile = (center + offset).Floored();
            var world = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
            if (TryResolveColumn(planetMap, world, depth, blockers, blockersIndestructible, respectAreas, out landing))
                return true;
        }

        return false;
    }

    private bool TryResolveColumn(
        EntityUid planetMap,
        Vector2 world,
        int depth,
        EntityWhitelist? blockers,
        bool blockersIndestructible,
        bool respectAreas,
        out EntityCoordinates landing)
    {
        landing = default;
        var mapUid = planetMap;
        Entity<CMUZLevelsNetworkComponent>? network = null;
        if (_zLevels.TryGetZNetwork(planetMap, out var found))
        {
            network = found;
            if (!_zLevels.TryGetMapAtDepth(found.Value, depth, out mapUid))
                return false;
        }

        var climbed = false;
        while (true)
        {
            if (!TryGetTile(mapUid, world, out var grid, out var tileRef))
            {
                if (!climbed)
                    return false;

                landing = new EntityCoordinates(mapUid, world);
                return true;
            }

            if (HasBlocker(grid, tileRef.GridIndices, blockers, blockersIndestructible))
            {
                depth++;
                if (network == null || !_zLevels.TryGetMapAtDepth(network.Value, depth, out mapUid))
                    return false;

                climbed = true;
                continue;
            }

            var empty = tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef);
            if (empty && !climbed)
                return false;

            if (!empty && _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                return false;

            landing = _map.GridTileToLocal(grid, grid, tileRef.GridIndices);
            if (!climbed && respectAreas && !_area.CanParadrop(landing))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Finds the grid tile under <paramref name="world"/> on <paramref name="mapUid"/> and whether something larger than a pod
    /// may land there: no blocking walls and nothing impassable anchored on it. Empty tiles are fine because the unit brings its floor.
    /// </summary>
    public bool TryGetClearTile(
        EntityUid mapUid,
        Vector2 world,
        EntityWhitelist? blockers,
        bool blockersIndestructible,
        out Entity<MapGridComponent> grid,
        out Vector2i indices)
    {
        indices = default;
        if (!TryGetGrid(mapUid, world, out grid))
            return false;

        indices = _map.WorldToTile(grid, grid, world);
        if (HasBlocker(grid, indices, blockers, blockersIndestructible))
            return false;

        var tileRef = _map.GetTileRef(grid, grid, indices);
        return tileRef.Tile.IsEmpty || !_turf.IsTileBlocked(tileRef, CollisionGroup.Impassable);
    }

    private bool TryGetTile(EntityUid mapUid, Vector2 world, out Entity<MapGridComponent> grid, out TileRef tileRef)
    {
        tileRef = default;
        if (!TryGetGrid(mapUid, world, out grid))
            return false;

        return _map.TryGetTileRef(grid, grid, _map.WorldToTile(grid, grid, world), out tileRef);
    }

    private bool TryGetGrid(EntityUid mapUid, Vector2 world, out Entity<MapGridComponent> grid)
    {
        grid = default;
        if (_map.TryFindGridAt(mapUid, world, out var gridUid, out var gridComp))
            grid = (gridUid, gridComp);
        else if (TryComp(mapUid, out MapGridComponent? mapGrid))
            grid = (mapUid, mapGrid);
        else
            return false;

        return true;
    }

    private bool HasBlocker(Entity<MapGridComponent> grid, Vector2i indices, EntityWhitelist? blockers, bool indestructible)
    {
        if (blockers == null)
            return false;

        var anchored = _map.GetAnchoredEntities(grid, indices);
        while (anchored.MoveNext(out var uid))
        {
            if (!_whitelist.IsValid(blockers, uid.Value))
                continue;

            if (indestructible && _damageableQuery.HasComp(uid.Value))
                continue;

            return true;
        }

        return false;
    }
}
