using Content.Shared._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Robust.Server.GameObjects;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem : CMUSharedZLevelsSystem
{
    [Dependency] private MapSystem _map = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    public override void Initialize()
    {
        base.Initialize();
        InitView();
        InitAudio();
        InitTransitionBudget();
        InitializeActivation();

        SubscribeLocalEvent<MapRemovedEvent>(OnMapRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ApplyPendingViewConfiguration();
        ApplyPendingAudioConfiguration();
        ProcessPendingCrossZAudioSources();

        if (!ZLevelsEnabled)
            return;

        UpdateZMovement(frameTime);
        UpdateView(frameTime);
    }

    private void OnMapRemoved(MapRemovedEvent ev)
    {
        using var profile = Prof.Group("CMU Z Topology Map Removal");
        if (TryComp<CMUZLevelMapComponent>(ev.Uid, out var removedMap) &&
            removedMap.NetworkUid.IsValid() &&
            TryComp<CMUZLevelsNetworkComponent>(removedMap.NetworkUid, out var directNetwork) &&
            RemoveMapFromNetwork(ev.Uid, removedMap.NetworkUid, directNetwork))
        {
            if (Prof.IsEnabled)
                Prof.WriteValue("CMU Z Topology Removal Direct Hits", 1);
            return;
        }

        var fallbackNetworksScanned = 0;
        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var networkUid, out var network))
        {
            fallbackNetworksScanned++;
            if (RemoveMapFromNetwork(ev.Uid, networkUid, network))
                break;
        }

        if (Prof.IsEnabled)
        {
            Prof.WriteValue("CMU Z Topology Removal Direct Hits", 0);
            Prof.WriteValue("CMU Z Topology Removal Fallback Networks", fallbackNetworksScanned);
        }
    }

    private bool RemoveMapFromNetwork(
        EntityUid removedMap,
        EntityUid networkUid,
        CMUZLevelsNetworkComponent network)
    {
        if (!network.ZLevelByEntity.Remove(removedMap, out var depth))
            return false;

        if (network.ZLevels.TryGetValue(depth, out var mapAtDepth) &&
            mapAtDepth == removedMap)
        {
            network.ZLevels.Remove(depth);
        }

        ClearRemovedMapNeighbour(network, depth - 1, clearAbove: true);
        ClearRemovedMapNeighbour(network, depth + 1, clearAbove: false);

        if (network.ZLevels.Count == 0)
        {
            QueueDel(networkUid);
            return true;
        }

        Dirty(networkUid, network);

        var updated = new CMUZLevelNetworkUpdatedEvent(
            (networkUid, network),
            CMUZLevelNetworkUpdateKind.MapRemoved,
            removedMap,
            depth);
        RaiseLocalEvent(ref updated);
        RefreshViewersForNetwork((networkUid, network));
        return true;
    }

    private void ClearRemovedMapNeighbour(
        CMUZLevelsNetworkComponent network,
        int depth,
        bool clearAbove)
    {
        if (!network.ZLevels.TryGetValue(depth, out var neighbourUid) ||
            neighbourUid is not { } neighbour ||
            !TryComp<CMUZLevelMapComponent>(neighbour, out var neighbourMap))
        {
            return;
        }

        if (clearAbove)
            neighbourMap.MapAbove = null;
        else
            neighbourMap.MapBelow = null;
    }
}
