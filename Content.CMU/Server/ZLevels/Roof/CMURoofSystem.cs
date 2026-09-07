using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ZLevels.Roof;
using Content.Shared.Light.Components;

namespace Content.Server.CMU14.ZLevels.Roof;

/// <inheritdoc/>
public sealed partial class CMURoofSystem : CMUSharedRoofSystem
{
    private readonly HashSet<Vector2i> _roofMap = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
    }

    private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        RecalculateNetworkRoofs(args.Network);
    }

    public void RecalculateNetworkRoofs(Entity<CMUZLevelsNetworkComponent> network)
    {
        _roofMap.Clear();

        var maps = ZLevel.GetOrderedNetworkMaps(network);
        for (var i = maps.Count - 1; i >= 0; i--)
        {
            var map = maps[i].Map;

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
