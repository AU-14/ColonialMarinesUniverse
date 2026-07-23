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

        if (!_zLevelsEnabled)
            return;

        UpdateZMovement(frameTime);
        UpdateView(frameTime);
    }

    private void OnMapRemoved(MapRemovedEvent ev)
    {
        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var networkUid, out var network))
        {
            if (!network.ZLevelByEntity.Remove(ev.Uid, out var depth))
                continue;

            if (network.ZLevels.TryGetValue(depth, out var mapAtDepth) &&
                mapAtDepth == ev.Uid)
            {
                network.ZLevels.Remove(depth);
            }

            ClearRemovedMapNeighbour(network, depth - 1, clearAbove: true);
            ClearRemovedMapNeighbour(network, depth + 1, clearAbove: false);

            if (network.ZLevels.Count == 0)
            {
                QueueDel(networkUid);
                continue;
            }

            Dirty(networkUid, network);

            var updated = new CMUZLevelNetworkUpdatedEvent((networkUid, network));
            RaiseLocalEvent(ref updated);
            RefreshViewersForNetwork((networkUid, network));
        }
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

        Dirty(neighbour, neighbourMap);
    }
}
