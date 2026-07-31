using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem : EntitySystem
{
    /// <summary>
    /// World-space sprite displacement used when projecting adjacent z-levels into the active view.
    /// </summary>
    public const float ZLevelVisualOffset = 0.75f;

    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IConfigurationManager _configuration = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] protected ProfManager Prof = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    protected EntityQuery<TransformComponent> XformQuery;
    private int _profileZNetworkFastHits;
    private int _profileZNetworkRecoveryScans;
    private int _profileZNetworkRecoveryNetworks;
    private int _profileZNetworkRecoveryHits;
    private int _profileZNetworkMisses;
    private int _profileZOffsetNeighbourHits;
    private int _profileZOffsetNetworkHits;
    private int _profileZOffsetRecoveryScans;
    private int _profileZOffsetRecoveryNetworks;
    private int _profileZOffsetRecoveryHits;
    private int _profileZOffsetMisses;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        XformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<CMUZLevelsNetworkComponent, AfterAutoHandleStateEvent>(OnZNetworkState);
        SubscribeLocalEvent<CMUZLevelMapComponent, ComponentStartup>(OnZMapStartup);

        InitMovement();
        InitThrowing();
        InitView();
    }

    private void OnZNetworkState(Entity<CMUZLevelsNetworkComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (_net.IsClient)
            RebuildClientTopology(ent);
    }

    private void OnZMapStartup(Entity<CMUZLevelMapComponent> ent, ref ComponentStartup args)
    {
        if (!_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var networkUid, out var network))
        {
            if (!network.ZLevelByEntity.TryGetValue(ent.Owner, out var depth) &&
                !TryFindDepth(network, ent.Owner, out depth))
            {
                continue;
            }

            ApplyClientMapTopology(ent, networkUid, network, depth);
            return;
        }
    }

    private void RebuildClientTopology(Entity<CMUZLevelsNetworkComponent> ent)
    {
        foreach (var oldMap in ent.Comp.ZLevelByEntity.Keys)
        {
            if (!_zMapQuery.TryComp(oldMap, out var map))
                continue;

            map.NetworkUid = EntityUid.Invalid;
            map.MapAbove = null;
            map.MapBelow = null;
            map.Depth = 0;
        }

        ent.Comp.ZLevelByEntity.Clear();
        foreach (var (depth, mapUid) in ent.Comp.ZLevels)
        {
            if (mapUid is not { } map)
                continue;

            ent.Comp.ZLevelByEntity[map] = depth;
            if (_zMapQuery.TryComp(map, out var mapComp))
                ApplyClientMapTopology((map, mapComp), ent.Owner, ent.Comp, depth);
        }
    }

    private static bool TryFindDepth(
        CMUZLevelsNetworkComponent network,
        EntityUid map,
        out int depth)
    {
        foreach (var (candidateDepth, candidateMap) in network.ZLevels)
        {
            if (candidateMap != map)
                continue;

            depth = candidateDepth;
            network.ZLevelByEntity[map] = candidateDepth;
            return true;
        }

        depth = default;
        return false;
    }

    private static void ApplyClientMapTopology(
        Entity<CMUZLevelMapComponent> map,
        EntityUid networkUid,
        CMUZLevelsNetworkComponent network,
        int depth)
    {
        map.Comp.NetworkUid = networkUid;
        map.Comp.Depth = depth;
        map.Comp.MapAbove = network.ZLevels.GetValueOrDefault(depth + 1);
        map.Comp.MapBelow = network.ZLevels.GetValueOrDefault(depth - 1);
    }

    /// <summary>
    /// Checks whether the map is in the zLevels network. If so, returns true and the current depth + Entity of the current zLevels network.
    /// </summary>
    [PublicAPI]
    public bool TryGetZNetwork(EntityUid mapUid, [NotNullWhen(true)] out Entity<CMUZLevelsNetworkComponent>? zLevel)
    {
        zLevel = null;

        if (_zMapQuery.TryComp(mapUid, out var zLevelMapComp) &&
            zLevelMapComp.NetworkUid.IsValid() &&
            !TerminatingOrDeleted(zLevelMapComp.NetworkUid) &&
            TryComp<CMUZLevelsNetworkComponent>(zLevelMapComp.NetworkUid, out var cachedNetwork))
        {
            if (Prof.IsEnabled)
                _profileZNetworkFastHits++;

            zLevel = (zLevelMapComp.NetworkUid, cachedNetwork);
            return true;
        }

        if (Prof.IsEnabled)
            _profileZNetworkRecoveryScans++;

        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var uid, out var zLevelComp))
        {
            if (Prof.IsEnabled)
                _profileZNetworkRecoveryNetworks++;

            if (!zLevelComp.ZLevelByEntity.ContainsKey(mapUid))
                continue;

            if (Prof.IsEnabled)
                _profileZNetworkRecoveryHits++;

            zLevel = (uid, zLevelComp);
            return true;
        }

        if (Prof.IsEnabled)
            _profileZNetworkMisses++;

        return false;
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<CMUZLevelMapComponent?> inputMapUid,
        int offset,
        [NotNullWhen(true)] out Entity<CMUZLevelMapComponent>? outputMapUid)
    {
        outputMapUid = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false))
            return false;

        if (offset == 1 &&
            inputMapUid.Comp.MapAbove is { } mapAbove &&
            _zMapQuery.TryComp(mapAbove, out var mapAboveComp))
        {
            if (Prof.IsEnabled)
                _profileZOffsetNeighbourHits++;

            outputMapUid = (mapAbove, mapAboveComp);
            return true;
        }

        if (offset == -1 &&
            inputMapUid.Comp.MapBelow is { } mapBelow &&
            _zMapQuery.TryComp(mapBelow, out var mapBelowComp))
        {
            if (Prof.IsEnabled)
                _profileZOffsetNeighbourHits++;

            outputMapUid = (mapBelow, mapBelowComp);
            return true;
        }

        if (inputMapUid.Comp.NetworkUid.IsValid() &&
            TryComp<CMUZLevelsNetworkComponent>(inputMapUid.Comp.NetworkUid, out var cachedNetwork) &&
            cachedNetwork.ZLevels.TryGetValue(inputMapUid.Comp.Depth + offset, out var cachedTargetMapUid) &&
            _zMapQuery.TryComp(cachedTargetMapUid, out var cachedTargetZLevelComp))
        {
            if (Prof.IsEnabled)
                _profileZOffsetNetworkHits++;

            outputMapUid = (cachedTargetMapUid.Value, cachedTargetZLevelComp);
            return true;
        }

        if (Prof.IsEnabled)
            _profileZOffsetRecoveryScans++;

        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var network))
        {
            if (Prof.IsEnabled)
                _profileZOffsetRecoveryNetworks++;

            if (!network.ZLevelByEntity.TryGetValue(inputMapUid, out var inputDepth))
                continue;

            if (!network.ZLevels.TryGetValue(inputDepth + offset, out var targetMapUid))
                continue;

            if (!_zMapQuery.TryComp(targetMapUid, out var targetZLevelComp))
                continue;

            if (Prof.IsEnabled)
                _profileZOffsetRecoveryHits++;

            outputMapUid = (targetMapUid.Value, targetZLevelComp);
            return true;
        }

        if (Prof.IsEnabled)
            _profileZOffsetMisses++;

        return false;
    }

    private void WriteTopologyLookupProfileCounters()
    {
        Prof.WriteValue("CMU Z Network Fast Hits", _profileZNetworkFastHits);
        Prof.WriteValue("CMU Z Network Recovery Scans", _profileZNetworkRecoveryScans);
        Prof.WriteValue("CMU Z Network Recovery Networks", _profileZNetworkRecoveryNetworks);
        Prof.WriteValue("CMU Z Network Recovery Hits", _profileZNetworkRecoveryHits);
        Prof.WriteValue("CMU Z Network Misses", _profileZNetworkMisses);
        Prof.WriteValue("CMU Z Offset Neighbour Hits", _profileZOffsetNeighbourHits);
        Prof.WriteValue("CMU Z Offset Network Hits", _profileZOffsetNetworkHits);
        Prof.WriteValue("CMU Z Offset Recovery Scans", _profileZOffsetRecoveryScans);
        Prof.WriteValue("CMU Z Offset Recovery Networks", _profileZOffsetRecoveryNetworks);
        Prof.WriteValue("CMU Z Offset Recovery Hits", _profileZOffsetRecoveryHits);
        Prof.WriteValue("CMU Z Offset Misses", _profileZOffsetMisses);

        _profileZNetworkFastHits = 0;
        _profileZNetworkRecoveryScans = 0;
        _profileZNetworkRecoveryNetworks = 0;
        _profileZNetworkRecoveryHits = 0;
        _profileZNetworkMisses = 0;
        _profileZOffsetNeighbourHits = 0;
        _profileZOffsetNetworkHits = 0;
        _profileZOffsetRecoveryScans = 0;
        _profileZOffsetRecoveryNetworks = 0;
        _profileZOffsetRecoveryHits = 0;
        _profileZOffsetMisses = 0;
    }

    [PublicAPI]
    public bool TryMapOffset(
        Entity<CMUZLevelMapComponent?> inputMapUid,
        int offset,
        [NotNullWhen(true)] out Entity<CMUZLevelMapComponent>? outputMapUid,
        [NotNullWhen(true)] out MapComponent? outputMap)
    {
        outputMap = null;

        if (!TryMapOffset(inputMapUid, offset, out outputMapUid) ||
            !_mapQuery.TryComp(outputMapUid.Value.Owner, out outputMap))
        {
            return false;
        }

        return true;
    }

    [PublicAPI]
    public bool TryGetMapCoordinates(EntityUid map, Vector2 worldPosition, out MapCoordinates coordinates)
    {
        coordinates = default;
        if (!_mapQuery.TryComp(map, out var mapComp))
            return false;

        coordinates = new MapCoordinates(worldPosition, mapComp.MapId);
        return true;
    }

    [PublicAPI]
    public bool TryProjectToZMap(
        Entity<CMUZLevelMapComponent?> inputMapUid,
        int offset,
        Vector2 worldPosition,
        out MapCoordinates coordinates,
        [NotNullWhen(true)] out Entity<CMUZLevelMapComponent>? outputMapUid)
    {
        coordinates = default;

        if (!TryMapOffset(inputMapUid, offset, out outputMapUid, out var outputMap))
            return false;

        coordinates = new MapCoordinates(worldPosition, outputMap.MapId);
        return true;
    }

    [PublicAPI]
    public bool TryMapUp(Entity<CMUZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<CMUZLevelMapComponent>? aboveMapUid)
    {
        return TryMapOffset(inputMapUid, 1, out aboveMapUid);
    }

    [PublicAPI]
    public bool TryMapDown(Entity<CMUZLevelMapComponent?> inputMapUid,
        [NotNullWhen(true)] out Entity<CMUZLevelMapComponent>? belowMapUid)
    {
        return TryMapOffset(inputMapUid, -1, out belowMapUid);
    }

    [PublicAPI]
    public bool TryGetDepthBounds(Entity<CMUZLevelsNetworkComponent> network, out int minDepth, out int maxDepth)
    {
        minDepth = int.MaxValue;
        maxDepth = int.MinValue;

        foreach (var entry in network.Comp.ZLevels)
        {
            if (!entry.Value.HasValue)
                continue;

            minDepth = Math.Min(minDepth, entry.Key);
            maxDepth = Math.Max(maxDepth, entry.Key);
        }

        return minDepth != int.MaxValue;
    }

    [PublicAPI]
    public bool TryGetMapAtDepth(Entity<CMUZLevelsNetworkComponent> network, int depth, out EntityUid map)
    {
        map = default;

        if (!network.Comp.ZLevels.TryGetValue(depth, out var mapUid) ||
            mapUid is not { } resolved)
        {
            return false;
        }

        map = resolved;
        return true;
    }

    [PublicAPI]
    public bool TryGetMapAtDepth(
        Entity<CMUZLevelsNetworkComponent> network,
        int depth,
        out EntityUid map,
        [NotNullWhen(true)] out MapComponent? mapComp)
    {
        mapComp = null;

        if (!TryGetMapAtDepth(network, depth, out map) ||
            !_mapQuery.TryComp(map, out mapComp))
        {
            return false;
        }

        return true;
    }
}
