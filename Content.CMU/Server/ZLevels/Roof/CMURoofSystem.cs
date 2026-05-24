using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Roof;
using Content.Shared.Light.Components;

namespace Content.Server._CMU14.ZLevels.Roof;

/// <inheritdoc/>
public sealed partial class CMURoofSystem : CMUSharedRoofSystem
{
    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelsNetworkComponent, CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
    }

    private void OnNetworkUpdated(Entity<CMUZLevelsNetworkComponent> ent, ref CMUZLevelNetworkUpdatedEvent args)
    {
        RecalculateNetworkRoofs(ent);
    }

    public void RecalculateNetworkRoofs(Entity<CMUZLevelsNetworkComponent> network)
    {
        _roofMap.Clear();

        if (!ZLevel.TryGetDepthBounds(network, out var minDepth, out var maxDepth))
            return;

        for (var depth = maxDepth; depth >= minDepth; depth--)
        {
            if (!ZLevel.TryGetMapAtDepth(network, depth, out var map))
                continue;

            if (!GridQuery.TryComp(map, out var mapGrid))
                continue;

            var enumerator = Map.GetAllTilesEnumerator(map, mapGrid);
            var roofComp = EnsureComp<RoofComponent>(map);

            while (enumerator.MoveNext(out var tileRef))
            {
                Roof.SetRoof((map, mapGrid, roofComp), tileRef.Value.GridIndices, _roofMap.Contains(tileRef.Value.GridIndices));

                if (!CMUZLevelOpeningCache.IsOpeningTile(tileRef.Value.Tile, TilDefMan))
                    _roofMap.Add(tileRef.Value.GridIndices);
            }
        }
    }
}
