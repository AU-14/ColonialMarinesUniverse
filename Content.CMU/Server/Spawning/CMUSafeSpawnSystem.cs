using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Falling;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Spawning;

/// <summary>
/// FLOW-01: whether a spot on the planet is somewhere a player can safely appear. Ghost-role and antag rules
/// that place a body themselves run their candidates through this instead of trusting a spawn point or a
/// random station tile.
/// </summary>
public sealed partial class CMUSafeSpawnSystem : EntitySystem
{
    [Dependency] private AreaSystem _area = default!;
    [Dependency] private AtmosphereSystem _atmosphere = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    /// <summary>A fall trigger this close (in tiles) makes a spot unsafe: anything touching one drops a z-level.</summary>
    public const float FallTriggerRange = 1.5f;

    private readonly HashSet<Entity<CMUFallingComponent>> _falling = new();

    /// <summary>
    /// True when <paramref name="coordinates"/> is a safe tile: on a grid of a planet map, a real floor
    /// (not empty, not space), not blocked by an impassable or airtight anchored entity, inside an RMC area,
    /// and with no fall trigger within <see cref="FallTriggerRange"/> tiles.
    /// </summary>
    /// <param name="ignore">An entity on the tile that does not count as blocking it, such as the body being checked.</param>
    public bool IsSafeSpawnTile(EntityCoordinates coordinates, EntityUid? ignore = null)
    {
        return GetUnsafeReason(coordinates, ignore) == null;
    }

    /// <summary>Why <paramref name="coordinates"/> is not a safe spawn tile, or null when it is.</summary>
    public CMUUnsafeSpawnReason? GetUnsafeReason(EntityCoordinates coordinates, EntityUid? ignore = null)
    {
        if (!coordinates.IsValid(EntityManager))
            return CMUUnsafeSpawnReason.InvalidCoordinates;

        // the planet only: never a ship, a holding map or an admin map
        if (_transform.GetMap(coordinates) is not { } mapUid || !HasComp<RMCPlanetComponent>(mapUid))
            return CMUUnsafeSpawnReason.NotPlanet;

        // the grid the coordinates belong to, resolved the way AreaSystem resolves it; a map-position grid
        // search can land on a different grid (a planet map that is itself a grid) whose area data is empty
        if (_transform.GetGrid(coordinates) is not { } gridUid || !TryComp(gridUid, out MapGridComponent? grid))
            return CMUUnsafeSpawnReason.NoGrid;

        if (!_map.TryGetTileRef(gridUid, grid, coordinates, out var tile))
            return CMUUnsafeSpawnReason.NoTile;

        if (tile.Tile.IsEmpty || _turf.IsSpace(tile))
            return CMUUnsafeSpawnReason.SpaceTile;

        Func<EntityUid, bool>? skip = ignore is { } ignored ? uid => uid == ignored : null;
        if (_turf.IsTileBlocked(tile.GridUid, tile.GridIndices, CollisionGroup.Impassable, grid, ignore: skip))
            return CMUUnsafeSpawnReason.Blocked;

        if (_atmosphere.IsTileAirBlocked(tile.GridUid, tile.GridIndices, mapGridComp: grid))
            return CMUUnsafeSpawnReason.AirBlocked;

        // no area is the proxy for an unmapped or unplayable region of the planet grid
        var areaGrid = new Entity<MapGridComponent, AreaGridComponent?>(tile.GridUid, grid, null);
        if (!_area.TryGetArea(areaGrid, tile.GridIndices, out _, out _))
            return CMUUnsafeSpawnReason.NoArea;

        _falling.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(coordinates), FallTriggerRange, _falling);
        return _falling.Count == 0 ? null : CMUUnsafeSpawnReason.FallTrigger;
    }

    /// <summary>The map-coordinate form of the entity-coordinate check above.</summary>
    public bool IsSafeSpawnTile(MapCoordinates coordinates, EntityUid? ignore = null)
    {
        if (coordinates.MapId == MapId.Nullspace)
            return false;

        // express the point in the frame of the grid under it, so the grid-based checks see that grid
        if (!_map.TryFindGridAt(coordinates, out var gridUid, out _))
            return false;

        return IsSafeSpawnTile(_transform.ToCoordinates(gridUid, coordinates), ignore);
    }

    /// <summary>
    /// Tries up to <paramref name="attempts"/> distinct random tiles across <paramref name="grids"/> (weighted by
    /// tile count) and returns the centre of the first safe one.
    /// </summary>
    public bool TryFindSafeTile(IEnumerable<EntityUid> grids, int attempts, out EntityCoordinates coordinates)
    {
        coordinates = EntityCoordinates.Invalid;

        var tiles = new List<TileRef>();
        foreach (var gridUid in grids)
        {
            if (!TryComp(gridUid, out MapGridComponent? grid))
                continue;

            var enumerator = _map.GetAllTiles(gridUid, grid);
            while (enumerator.MoveNext(out var tileRef))
            {
                tiles.Add(tileRef.Value);
            }
        }

        for (var i = 0; i < attempts && tiles.Count > 0; i++)
        {
            var index = _random.Next(tiles.Count);
            var tile = tiles[index];
            tiles.RemoveSwap(index);

            if (!TryComp(tile.GridUid, out MapGridComponent? grid))
                continue;

            var candidate = _map.GridTileToLocal(tile.GridUid, grid, tile.GridIndices);
            if (!IsSafeSpawnTile(candidate))
                continue;

            coordinates = candidate;
            return true;
        }

        return false;
    }
}

/// <summary>The first check a spawn tile failed in <see cref="CMUSafeSpawnSystem"/>.</summary>
public enum CMUUnsafeSpawnReason
{
    InvalidCoordinates,
    NotPlanet,
    NoTile,
    SpaceTile,
    NoGrid,
    Blocked,
    AirBlocked,
    NoArea,
    FallTrigger,
}
