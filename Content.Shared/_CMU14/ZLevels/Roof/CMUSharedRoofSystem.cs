using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CMU14.ZLevels.Roof;

/// <summary>
/// Systems that automatically covers tiles with roofs (or removes roofs)
/// if there is a tile on one of the levels above in the ZLevels network.
/// </summary>
public abstract partial class CMUSharedRoofSystem : EntitySystem
{
    [Dependency] protected CMUSharedZLevelsSystem ZLevel = default!;
    [Dependency] protected SharedRoofSystem Roof = default!;
    [Dependency] protected SharedMapSystem Map = default!;
    [Dependency] protected ITileDefinitionManager TilDefMan = default!;

    protected EntityQuery<MapGridComponent> GridQuery;
    protected EntityQuery<RoofComponent> RoofQuery;

    public override void Initialize()
    {
        base.Initialize();

        GridQuery = GetEntityQuery<MapGridComponent>();
        RoofQuery = GetEntityQuery<RoofComponent>();

        SubscribeLocalEvent<CMUZLevelMapComponent, TileChangedEvent>(OnTileChanged);
    }

    /// <summary>
    /// When changing tiles, we iteratively go down to the end of the ZLevels network, repeatedly calculating whether the tiles at the bottom now have a roof or not.
    /// </summary>
    private void OnTileChanged(Entity<CMUZLevelMapComponent> ent, ref TileChangedEvent args)
    {
        if (!GridQuery.TryComp(ent, out var currentMapGrid))
            return;
        if (!RoofQuery.TryComp(ent, out var currentRoof))
            return;

        if (args.Changes.Length == 0)
            return;

        Dictionary<Vector2i, bool> roofMap = new();
        foreach (var change in args.Changes)
        {
            var roovedAbove = Roof.IsRooved((ent, currentMapGrid, currentRoof), change.GridIndices);
            var roovedTile = !CMUZLevelOpeningCache.IsOpeningTile(change.NewTile, TilDefMan);
            roofMap.Add(change.GridIndices, roovedAbove || roovedTile);
        }

        var mapsBelow = ZLevel.GetAllMapsBelow(ent);

        if (mapsBelow.Count == 0)
            return;

        foreach (var mapBelow in mapsBelow)
        {
            if (!GridQuery.TryComp(mapBelow, out var mapGridBelow))
                continue;

            var roofBelow = EnsureComp<RoofComponent>(mapBelow);
            var coveredByThisLevel = new List<Vector2i>();

            foreach (var (indices, rooved) in roofMap)
            {
                Roof.SetRoof((mapBelow, mapGridBelow, roofBelow), indices, rooved);

                if (Map.TryGetTile(mapGridBelow, indices, out var tile) && !tile.IsEmpty)
                    coveredByThisLevel.Add(indices);
            }

            foreach (var indices in coveredByThisLevel)
            {
                roofMap[indices] = true;
            }
        }
    }
}
