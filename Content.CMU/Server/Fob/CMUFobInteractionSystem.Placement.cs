using System.Numerics;
using Content.Shared.Physics;
using Content.Shared.Maps;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.Server.CMU14.Fob;

public sealed partial class CMUFobInteractionSystem
{
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private ITileDefinitionManager _tiles = default!;

    /// <summary>Allow outdoor terrain and loose clutter; reject walls, empty space and the landing footprint.</summary>
    public string? GetPlacementFailure(EntityUid device, Entity<CMUFobNetworkComponent> network,
        out EntityCoordinates coordinates)
    {
        coordinates = default;
        if (_containers.IsEntityInContainer(device))
            return "cmu-fob-placement-carried";
        if (Transform(device).MapUid != network.Comp.Map)
            return "cmu-fob-placement-range";
        if (!_map.TryFindGridAt(_transform.GetMapCoordinates(device), out var grid, out var mapGrid))
            return "cmu-fob-placement-floor";

        coordinates = _transform.ToCoordinates(grid, _transform.GetMapCoordinates(device));
        var tile = _map.GetTileRef(grid, mapGrid, coordinates);
        var center = _map.GridTileToWorld(grid, mapGrid, tile.GridIndices).Position;
        if (tile.Tile.IsEmpty || _tiles[tile.Tile.TypeId] is ContentTileDefinition { CMUFallThrough: true })
            return "cmu-fob-placement-floor";
        if (Vector2.DistanceSquared(center, _transform.GetWorldPosition(network))
            > network.Comp.DeploymentRange * network.Comp.DeploymentRange)
            return "cmu-fob-placement-range";
        if (!_maps.CanPlaceUtility(network, center))
            return "cmu-fob-placement-landing";
        if (!_construction.CanBuildAt(coordinates, Name(device), out _, anchoring: true,
                collision: CollisionGroup.Impassable, ignoreBlockedTerrain: true))
            return "cmu-fob-placement-blocked";
        return null;
    }
}
