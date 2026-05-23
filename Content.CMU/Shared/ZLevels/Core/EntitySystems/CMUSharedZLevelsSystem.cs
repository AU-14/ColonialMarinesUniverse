using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.ZLevels.Core.EntitySystems;

public abstract partial class CMUSharedZLevelsSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] protected ProfManager Prof = default!;

    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<CMUZLevelMapComponent> _zMapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _mapQuery = GetEntityQuery<MapComponent>();
        _zMapQuery = GetEntityQuery<CMUZLevelMapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();

        InitMovement();
        InitThrowing();
        InitView();
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
            zLevel = (zLevelMapComp.NetworkUid, cachedNetwork);
            return true;
        }

        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var uid, out var zLevelComp))
        {
            if (!zLevelComp.ZLevelByEntity.ContainsKey(mapUid))
                continue;

            zLevel = (uid, zLevelComp);
            return true;
        }

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
            outputMapUid = (mapAbove, mapAboveComp);
            return true;
        }

        if (offset == -1 &&
            inputMapUid.Comp.MapBelow is { } mapBelow &&
            _zMapQuery.TryComp(mapBelow, out var mapBelowComp))
        {
            outputMapUid = (mapBelow, mapBelowComp);
            return true;
        }

        if (inputMapUid.Comp.NetworkUid.IsValid() &&
            TryComp<CMUZLevelsNetworkComponent>(inputMapUid.Comp.NetworkUid, out var cachedNetwork) &&
            cachedNetwork.ZLevels.TryGetValue(inputMapUid.Comp.Depth + offset, out var cachedTargetMapUid) &&
            _zMapQuery.TryComp(cachedTargetMapUid, out var cachedTargetZLevelComp))
        {
            outputMapUid = (cachedTargetMapUid.Value, cachedTargetZLevelComp);
            return true;
        }

        var query = EntityQueryEnumerator<CMUZLevelsNetworkComponent>();
        while (query.MoveNext(out var network))
        {
            if (!network.ZLevelByEntity.TryGetValue(inputMapUid, out var inputDepth))
                continue;

            if (!network.ZLevels.TryGetValue(inputDepth + offset, out var targetMapUid))
                continue;

            if (!_zMapQuery.TryComp(targetMapUid, out var targetZLevelComp))
                continue;

            outputMapUid = (targetMapUid.Value, targetZLevelComp);
            return true;
        }

        return false;
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

    /// <summary>
    /// Returns a list of all maps above the specified map. The closest map at the top is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsAbove(Entity<CMUZLevelMapComponent> inputMapUid)
    {
        var result = new List<EntityUid>();
        var currentMap = inputMapUid;

        while (currentMap.Comp.MapAbove is { } above &&
               _zMapQuery.TryComp(above, out var aboveComp))
        {
            result.Add(above);
            currentMap = (above, aboveComp);
        }

        return result;
    }

    /// <summary>
    /// Returns a list of all maps below the specified map. The closest map at the bottom is returned first.
    /// </summary>
    [PublicAPI]
    public List<EntityUid> GetAllMapsBelow(Entity<CMUZLevelMapComponent> inputMapUid)
    {
        var result = new List<EntityUid>();
        var currentMap = inputMapUid;

        while (currentMap.Comp.MapBelow is { } below &&
               _zMapQuery.TryComp(below, out var belowComp))
        {
            result.Add(below);
            currentMap = (below, belowComp);
        }

        return result;
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
