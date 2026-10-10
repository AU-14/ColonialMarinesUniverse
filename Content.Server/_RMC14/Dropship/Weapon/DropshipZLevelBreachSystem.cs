using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Coordinates;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Dropship.Weapon;

/// <summary>
/// Carves the ceiling tiles crossed by penetrating dropship ordnance into passable z-level openings.
/// </summary>
public sealed class DropshipZLevelBreachSystem : EntitySystem
{
    private static readonly ProtoId<ContentTileDefinition> SpaceTile = ContentTileDefinition.SpaceID;
    private const string BreachMarkerPrototype = "RMCDropshipZLevelBreach";

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AmmoInFlightComponent, DropshipWeaponImpactEvent>(OnWeaponImpact);
    }

    private void OnWeaponImpact(Entity<AmmoInFlightComponent> ent, ref DropshipWeaponImpactEvent args)
    {
        CarvePenetrationPath(args.Coordinates, ent.Comp.ZLevelPenetration);
    }

    public void CarvePenetrationPath(MapCoordinates impactCoordinates, int zLevelPenetration)
    {
        if (zLevelPenetration <= 0 ||
            !_map.TryGetMap(impactCoordinates.MapId, out var sourceMap) ||
            sourceMap is not { } sourceMapUid ||
            _tile[SpaceTile] is not ContentTileDefinition spaceTile ||
            !spaceTile.Transparent)
        {
            return;
        }

        for (var offset = 1; offset <= zLevelPenetration; offset++)
        {
            if (!_zLevels.TryMapOffset((sourceMapUid, null), offset, out _, out var targetMap))
                continue;

            var surfaceCoordinates = new MapCoordinates(impactCoordinates.Position, targetMap.MapId);
            TryCarveOpening(surfaceCoordinates, spaceTile);
        }
    }

    private void TryCarveOpening(MapCoordinates coordinates, ContentTileDefinition spaceTile)
    {
        if (!_map.TryFindGridAt(coordinates, out var gridUid, out var grid))
            return;

        var tileIndices = _map.WorldToTile(gridUid, grid, coordinates.Position);
        if (!_map.TryGetTileRef(gridUid, grid, tileIndices, out var tileRef) ||
            tileRef.Tile.IsEmpty ||
            CMUZLevelOpeningCache.IsOpeningTile(tileRef.Tile, _tile) ||
            _tile[tileRef.Tile.TypeId] is not ContentTileDefinition tileDefinition ||
            tileDefinition.Indestructible)
        {
            return;
        }

        // Spawn while the original tile is still present so map-coordinate resolution can find and parent to the grid.
        SpawnBreachMarker(coordinates);
        _map.SetTile(gridUid, grid, tileIndices, new Tile(spaceTile.TileId));
    }

    private void SpawnBreachMarker(MapCoordinates coordinates)
    {
        // Spawn while the ceiling tile is still solid so the marker is parented to its grid, but leave it unanchored
        // so opening the tile does not immediately unanchor or delete the visual.
        Spawn(BreachMarkerPrototype, coordinates);
    }
}
