using System.Numerics; // CMU14
using Content.Shared.CMU14.ZLevels.Core;
// CMU14: unused since the 1..penetration carve was replaced.
// using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.Coordinates;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._RMC14.Dropship.Weapon;

// CMU14: carve only breach squares at the level a breaching munition struck; see CarveBreach.
// /// <summary>
// /// Carves the ceiling tiles crossed by penetrating dropship ordnance into passable z-level openings.
// /// </summary>
/// <summary>
/// Opens ceiling tiles at a breach impact into passable z-level openings. Pass-through and rock never carve.
/// </summary>
public sealed partial class DropshipZLevelBreachSystem : EntitySystem
{
    private static readonly ProtoId<ContentTileDefinition> SpaceTile = ContentTileDefinition.SpaceID;
    private const string BreachMarkerPrototype = "RMCDropshipZLevelBreach";

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tile = default!;
    // CMU14: unused since the 1..penetration carve was replaced by CarveBreach.
    // [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AmmoInFlightComponent, DropshipWeaponImpactEvent>(OnWeaponImpact);
    }

    private void OnWeaponImpact(Entity<AmmoInFlightComponent> ent, ref DropshipWeaponImpactEvent args)
    {
        // CMU14: only a breach impact carves, and only the level it struck. Pass-through and rock never open anything.
        // CarvePenetrationPath(args.Coordinates, ent.Comp.ZLevelPenetration);
        CarveBreach(args.Coordinates, args.BreachRadius);
    }

    // CMU14: the 1..penetration carve removed. It opened every ceiling in reach on the first impact.
    // public void CarvePenetrationPath(MapCoordinates impactCoordinates, int zLevelPenetration)
    // {
    //     if (zLevelPenetration <= 0 ||
    //         !_map.TryGetMap(impactCoordinates.MapId, out var sourceMap) ||
    //         sourceMap is not { } sourceMapUid ||
    //         _tile[SpaceTile] is not ContentTileDefinition spaceTile ||
    //         !spaceTile.Transparent)
    //     {
    //         return;
    //     }
    //
    //     for (var offset = 1; offset <= zLevelPenetration; offset++)
    //     {
    //         if (!_zLevels.TryMapOffset((sourceMapUid, null), offset, out _, out var targetMap))
    //             continue;
    //
    //         var surfaceCoordinates = new MapCoordinates(impactCoordinates.Position, targetMap.MapId);
    //         TryCarveOpening(surfaceCoordinates, spaceTile);
    //     }
    // }

    // CMU14 breach carve Begin
    /// <summary>
    /// Opens a square of ceiling tiles of the given radius at the breached level. Indestructible and
    /// already open tiles are left alone.
    /// </summary>
    public void CarveBreach(MapCoordinates impactCoordinates, int breachRadius)
    {
        if (breachRadius <= 0 ||
            _tile[SpaceTile] is not ContentTileDefinition spaceTile ||
            !spaceTile.Transparent)
        {
            return;
        }

        for (var x = -breachRadius; x <= breachRadius; x++)
        {
            for (var y = -breachRadius; y <= breachRadius; y++)
                TryCarveOpening(impactCoordinates.Offset(new Vector2(x, y)), spaceTile);
        }
    }
    // CMU14 End

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
