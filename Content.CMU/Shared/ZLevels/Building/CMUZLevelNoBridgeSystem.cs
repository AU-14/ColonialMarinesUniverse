using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.Tiles;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.ZLevels.Building;

/// <summary>
///     Tiles can't be laid over open air on a level that has another level below it, so nobody builds bridges
///     across the gap between buildings. Floors still go down on solid ground and over existing floor.
/// </summary>
public sealed class CMUZLevelNoBridgeSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private CMUSharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUZLevelMapComponent, FloorTileAttemptEvent>(OnFloorTileAttempt);
    }

    private void OnFloorTileAttempt(Entity<CMUZLevelMapComponent> ent, ref FloorTileAttemptEvent args)
    {
        if (args.Cancelled ||
            !TryComp(ent, out MapGridComponent? grid) ||
            !_zLevels.TryMapDown((ent.Owner, ent.Comp), out _))
        {
            return;
        }

        if (_map.TryGetTileRef(ent.Owner, grid, args.GridIndices, out var tile) &&
            !CMUZLevelOpeningCache.IsOpeningTile(tile.Tile, _tileDefinitions))
        {
            return;
        }

        args.Cancelled = true;
    }
}
