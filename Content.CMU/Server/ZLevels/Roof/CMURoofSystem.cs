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

        SubscribeLocalEvent<CMUZLevelNetworkUpdatedEvent>(OnNetworkUpdated);
    }

    private void OnNetworkUpdated(ref CMUZLevelNetworkUpdatedEvent args)
    {
        if (args.Kind == CMUZLevelNetworkUpdateKind.MapRemoved &&
            args.ChangedDepth is { } removedDepth)
        {
            if (!ZLevel.TryGetDepthBounds(args.Network, out var minimumDepth, out _) ||
                minimumDepth >= removedDepth)
            {
                return;
            }

            RecalculateNetworkRoofs(args.Network, removedDepth);
            return;
        }

        RecalculateNetworkRoofs(args.Network);
    }

    public void RecalculateNetworkRoofs(
        Entity<CMUZLevelsNetworkComponent> network,
        int? removedDepth = null)
    {
        using var profile = Prof.Group("CMU Z Roof Network Rebuild");
        _roofMap.Clear();

        if (!ZLevel.TryGetDepthBounds(network, out var minDepth, out var maxDepth))
            return;

        var mapsVisited = 0;
        var tilesVisited = 0;
        var mapsWritten = 0;
        var tilesWritten = 0;
        for (var depth = maxDepth; depth >= minDepth; depth--)
        {
            if (!ZLevel.TryGetMapAtDepth(network, depth, out var map))
                continue;

            if (!GridQuery.TryComp(map, out var mapGrid))
                continue;

            mapsVisited++;
            var writeRoofs = removedDepth is not { } removed || depth < removed;
            var enumerator = Map.GetAllTilesEnumerator(map, mapGrid);
            RoofComponent? roofComp = null;
            if (writeRoofs)
            {
                mapsWritten++;
                roofComp = EnsureComp<RoofComponent>(map);
            }

            while (enumerator.MoveNext(out var tileRef))
            {
                tilesVisited++;
                if (roofComp is not null)
                {
                    Roof.SetRoof(
                        (map, mapGrid, roofComp),
                        tileRef.Value.GridIndices,
                        _roofMap.Contains(tileRef.Value.GridIndices));
                    tilesWritten++;
                }

                if (!CMUZLevelOpeningCache.IsOpeningTile(tileRef.Value.Tile, TilDefMan))
                    _roofMap.Add(tileRef.Value.GridIndices);
            }
        }

        if (Prof.IsEnabled)
        {
            Prof.WriteValue("CMU Z Roof Network Maps", mapsVisited);
            Prof.WriteValue("CMU Z Roof Network Tiles", tilesVisited);
            Prof.WriteValue("CMU Z Roof Network Maps Written", mapsWritten);
            Prof.WriteValue("CMU Z Roof Network Writes", tilesWritten);
        }
    }
}
