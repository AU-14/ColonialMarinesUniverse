using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.CMU14.ZLevels.Roof;

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

    // CMU14 Begin: pooled per-event collections (GraphAudit ECS-04). Each tile change rents its own and returns it
    // cleared, so a tile change raised from inside another's roof update never shares one.
    private readonly Stack<Dictionary<Vector2i, bool>> _roofMapPool = new();
    private readonly Stack<List<Vector2i>> _coveredPool = new();
    // CMU14 End

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
        if (TerminatingOrDeleted(ent.Owner))
            return;

        if (!GridQuery.TryComp(ent, out var currentMapGrid))
            return;
        if (!RoofQuery.TryComp(ent, out var currentRoof))
            return;

        if (args.Changes.Length == 0)
            return;

        // CMU14: pooled (GraphAudit ECS-04); the line that was here (kept): Dictionary<Vector2i, bool> roofMap = new();
        var roofMap = _roofMapPool.Count > 0 ? _roofMapPool.Pop() : new Dictionary<Vector2i, bool>();
        foreach (var change in args.Changes)
        {
            var roovedAbove = Roof.IsRooved((ent, currentMapGrid, currentRoof), change.GridIndices);
            var roovedTile = !CMUZLevelOpeningCache.IsOpeningTile(change.NewTile, TilDefMan);
            roofMap.Add(change.GridIndices, roovedAbove || roovedTile);
        }

        // CMU14: the levels below are walked in place (nearest first, as GetAllMapsBelow listed them) instead of
        // being collected into a new list per event. The lines that were here (kept):
        // CMU14: var mapsBelow = ZLevel.GetAllMapsBelow(ent);
        // CMU14: if (mapsBelow.Count == 0)
        // CMU14:     return;
        // CMU14: foreach (var mapBelow in mapsBelow)
        var coveredByThisLevel = _coveredPool.Count > 0 ? _coveredPool.Pop() : new List<Vector2i>();
        Entity<CMUZLevelMapComponent> current = ent;
        while (ZLevel.TryMapDown((current.Owner, current.Comp), out var below))
        {
            current = below.Value;
            var mapBelow = current.Owner;
            if (!GridQuery.TryComp(mapBelow, out var mapGridBelow))
                continue;

            var roofBelow = EnsureComp<RoofComponent>(mapBelow);
            // CMU14: the list is reused per level; the line that was here (kept): var coveredByThisLevel = new List<Vector2i>();
            coveredByThisLevel.Clear();

            foreach (var (indices, rooved) in roofMap)
            {
                Roof.SetRoof((mapBelow, mapGridBelow, roofBelow), indices, rooved);

                if (Map.TryGetTile(mapGridBelow, indices, out var tile) &&
                    !CMUZLevelOpeningCache.IsFallThroughTile(tile, TilDefMan))
                    coveredByThisLevel.Add(indices);
            }

            foreach (var indices in coveredByThisLevel)
            {
                roofMap[indices] = true;
            }
        }

        coveredByThisLevel.Clear();
        _coveredPool.Push(coveredByThisLevel);
        roofMap.Clear();
        _roofMapPool.Push(roofMap);
    }
}
