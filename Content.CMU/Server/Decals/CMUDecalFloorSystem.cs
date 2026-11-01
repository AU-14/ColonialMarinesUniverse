using Content.Shared.CMU14.ZLevels.Core;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.Decals;

public sealed class CMUDecalFloorSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;

    public bool HasFloor(EntityCoordinates coordinates)
    {
        return HasFloor(_transform.ToMapCoordinates(coordinates));
    }

    public bool HasFloor(MapCoordinates coordinates)
    {
        return _map.TryFindGridAt(coordinates, out var gridUid, out var grid) &&
               HasFloor(gridUid, grid, _map.WorldToTile(gridUid, grid, coordinates.Position));
    }

    public bool HasFloor(EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        return _map.TryGetTileRef(gridUid, grid, indices, out var tileRef) &&
               !CMUZLevelOpeningCache.IsFallThroughTile(tileRef.Tile, _tileDefinitions);
    }
}
