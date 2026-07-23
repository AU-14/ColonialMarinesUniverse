using System.Buffers;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Profiling;

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
    [Dependency] protected ProfManager Prof = default!;

    protected EntityQuery<MapGridComponent> GridQuery;
    protected EntityQuery<RoofComponent> RoofQuery;
    protected EntityQuery<CMUZLevelMapComponent> ZMapQuery;

    public override void Initialize()
    {
        base.Initialize();

        GridQuery = GetEntityQuery<MapGridComponent>();
        RoofQuery = GetEntityQuery<RoofComponent>();
        ZMapQuery = GetEntityQuery<CMUZLevelMapComponent>();

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

        if (ent.Comp.MapBelow is not { } firstMapBelow)
            return;

        using var profile = Prof.Group("CMU Z Roof Propagation");
        var rentedRoofStates = ArrayPool<bool>.Shared.Rent(args.Changes.Length);
        var roofStates = rentedRoofStates.AsSpan(0, args.Changes.Length);

        try
        {
            for (var i = 0; i < args.Changes.Length; i++)
            {
                ref readonly var change = ref args.Changes[i];
                var roovedAbove = Roof.IsRooved((ent, currentMapGrid, currentRoof), change.GridIndices);
                var roovedTile = !CMUZLevelOpeningCache.IsOpeningTile(change.NewTile, TilDefMan);
                roofStates[i] = roovedAbove || roovedTile;
            }

            var mapsVisited = 0;
            var roofWrites = 0;
            EntityUid? currentMapBelow = firstMapBelow;
            while (currentMapBelow is { } mapBelow &&
                   ZMapQuery.TryComp(mapBelow, out var zMapBelow))
            {
                mapsVisited++;
                currentMapBelow = zMapBelow.MapBelow;

                if (!GridQuery.TryComp(mapBelow, out var mapGridBelow))
                    continue;

                var roofBelow = EnsureComp<RoofComponent>(mapBelow);
                for (var i = 0; i < args.Changes.Length; i++)
                {
                    ref readonly var change = ref args.Changes[i];
                    Roof.SetRoof(
                        (mapBelow, mapGridBelow, roofBelow),
                        change.GridIndices,
                        roofStates[i]);
                    roofWrites++;

                    if (Map.TryGetTile(mapGridBelow, change.GridIndices, out var tile) &&
                        !tile.IsEmpty)
                    {
                        roofStates[i] = true;
                    }
                }
            }

            if (Prof.IsEnabled)
            {
                Prof.WriteValue("CMU Z Roof Changed Tiles", args.Changes.Length);
                Prof.WriteValue("CMU Z Roof Maps Visited", mapsVisited);
                Prof.WriteValue("CMU Z Roof Writes", roofWrites);
            }
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(rentedRoofStates);
        }
    }
}
