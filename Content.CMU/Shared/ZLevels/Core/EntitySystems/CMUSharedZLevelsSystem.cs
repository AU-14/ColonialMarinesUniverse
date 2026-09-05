using System.Numerics;
using System.Diagnostics.CodeAnalysis;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using JetBrains.Annotations;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.ZLevels.Core.EntitySystems;

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

        InitBuckle();
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

        if (!TerminatingOrDeleted(mapUid) &&
            _mapQuery.HasComp(mapUid) &&
            _zMapQuery.TryComp(mapUid, out var zLevelMapComp) &&
            zLevelMapComp.NetworkUid.IsValid() &&
            !TerminatingOrDeleted(zLevelMapComp.NetworkUid) &&
            TryComp<CMUZLevelsNetworkComponent>(zLevelMapComp.NetworkUid, out var cachedNetwork) &&
            cachedNetwork.LifeStage <= ComponentLifeStage.Running &&
            cachedNetwork.ZLevels.TryGetValue(zLevelMapComp.Depth, out var member) &&
            member == mapUid)
        {
            zLevel = (zLevelMapComp.NetworkUid, cachedNetwork);
            return true;
        }

        return false;
    }

    [PublicAPI]
    public bool IsMapInNetwork(Entity<CMUZLevelsNetworkComponent> network, EntityUid mapUid)
        => TryGetZNetwork(mapUid, out var memberNetwork) && memberNetwork.Value.Owner == network.Owner;

    [PublicAPI]
    public bool IsSameZNetwork(MapId mapId, MapId primaryMapId)
        => mapId == primaryMapId
            || (_map.TryGetMap(mapId, out var mapUid)
            && _map.TryGetMap(primaryMapId, out var primaryMapUid)
            && IsSameZNetwork(mapUid.Value, primaryMapUid.Value));

    [PublicAPI]
    public bool IsSameZNetwork(EntityUid? mapUid, EntityUid primaryMapUid)
        => mapUid is { } map
            && (map == primaryMapUid
            || (TryGetZNetwork(primaryMapUid, out var network)
            && IsMapInNetwork(network.Value, map)));

    [PublicAPI]
    public List<EntityUid> GetAllNetworkMaps(EntityUid mapUid)
    {
        var maps = new List<EntityUid> { mapUid };

        if (TryGetZNetwork(mapUid, out var network))
        {
            foreach (var (_, map) in GetOrderedNetworkMaps(network.Value))
            {
                if (map != mapUid)
                    maps.Add(map);
            }
        }

        return maps;
    }

    /// <summary>
    /// Returns a depth-ordered snapshot of live membership. Work is proportional to the
    /// number of maps, including for sparse networks and depths at either integer limit.
    /// A snapshot permits callers to raise events that change topology while iterating.
    /// </summary>
    public List<(int Depth, EntityUid Map)> GetOrderedNetworkMaps(Entity<CMUZLevelsNetworkComponent> network)
    {
        var maps = new List<(int Depth, EntityUid Map)>(network.Comp.ZLevels.Count);
        foreach (var (depth, _) in network.Comp.ZLevels)
        {
            if (TryGetMapAtDepth(network, depth, out var map))
                maps.Add((depth, map));
        }

        maps.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        return maps;
    }

    [PublicAPI]
    public HashSet<MapId> GetAllNetworkMapIds(MapId mapId)
    {
        var ids = new HashSet<MapId> { mapId };

        foreach (var map in GetAllNetworkMaps(_map.GetMap(mapId)))
            ids.Add(_transform.GetMapId(map));

        return ids;
    }

    [PublicAPI]
    public bool TryMapOffset(Entity<CMUZLevelMapComponent?> inputMapUid,
        int offset,
        [NotNullWhen(true)] out Entity<CMUZLevelMapComponent>? outputMapUid)
    {
        outputMapUid = null;
        if (!Resolve(inputMapUid, ref inputMapUid.Comp, false) ||
            !TryGetZNetwork(inputMapUid.Owner, out var network))
            return false;

        var targetDepth = (long) inputMapUid.Comp.Depth + offset;
        if (targetDepth < int.MinValue || targetDepth > int.MaxValue ||
            !TryGetMapAtDepth(network.Value, (int) targetDepth, out var target) ||
            !_zMapQuery.TryComp(target, out var targetComp))
            return false;

        outputMapUid = (target, targetComp);
        return true;
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

        while (TryMapUp((currentMap.Owner, currentMap.Comp), out var above))
        {
            result.Add(above.Value.Owner);
            currentMap = above.Value;
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

        while (TryMapDown((currentMap.Owner, currentMap.Comp), out var below))
        {
            result.Add(below.Value.Owner);
            currentMap = below.Value;
        }

        return result;
    }

    [PublicAPI]
    public bool TryGetDepthBounds(Entity<CMUZLevelsNetworkComponent> network, out int minDepth, out int maxDepth)
    {
        minDepth = int.MaxValue;
        maxDepth = int.MinValue;
        var found = false;

        foreach (var entry in network.Comp.ZLevels)
        {
            if (!TryGetMapAtDepth(network, entry.Key, out _))
                continue;

            found = true;
            minDepth = Math.Min(minDepth, entry.Key);
            maxDepth = Math.Max(maxDepth, entry.Key);
        }

        return found;
    }

    [PublicAPI]
    public bool TryGetMapAtDepth(Entity<CMUZLevelsNetworkComponent> network, int depth, out EntityUid map)
    {
        map = default;

        if (TerminatingOrDeleted(network.Owner) ||
            network.Comp.LifeStage > ComponentLifeStage.Running ||
            !network.Comp.ZLevels.TryGetValue(depth, out var mapUid) ||
            mapUid is not { } resolved ||
            !TryGetZNetwork(resolved, out var memberNetwork) ||
            memberNetwork.Value.Owner != network.Owner)
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
