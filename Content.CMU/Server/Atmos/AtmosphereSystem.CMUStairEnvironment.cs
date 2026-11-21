using Content.Shared.Atmos;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    [Dependency] private CMUSharedZLevelsSystem _cmuStairLevels = default!;

    private bool CMUTryGetStairMixture(Entity<TransformComponent?> entity, bool excite, out GasMixture? mixture)
    {
        mixture = null;
        var coordinates = XformSystem.GetMapCoordinates(entity.Owner);
        if (_mapSystem.TryFindGridAt(coordinates, out var gridUid, out var grid)
            && _mapSystem.TryGetTileRef(gridUid, grid, _mapSystem.WorldToTile(gridUid, grid, coordinates.Position), out var tile)
            && !tile.Tile.IsEmpty && !((ContentTileDefinition) _tileDefinitionManager[tile.Tile.TypeId]).MapAtmosphere)
        {
            return false;
        }

        if (!_cmuStairLevels.TryGetStairEnvironment(coordinates, out var environment))
            return false;

        var transform = Transform(environment.EntityId);
        var indices = _mapSystem.WorldToTile(environment.EntityId,
            Comp<MapGridComponent>(environment.EntityId),
            XformSystem.ToMapCoordinates(environment).Position);
        mixture = GetTileMixture(environment.EntityId, transform.MapUid, indices, excite);
        return mixture != null;
    }
}
