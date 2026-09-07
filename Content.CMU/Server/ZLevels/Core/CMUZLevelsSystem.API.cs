using Content.Server.CMU14.ZLevels.PVS;
using Content.Shared.CMU14.ZLevels.Core.Components;
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server.CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    /// <summary>
    /// Creates a new entity zLevelNetwork
    /// </summary>
    [PublicAPI]
    public Entity<CMUZLevelsNetworkComponent> CreateZNetwork()
    {
        var ent = Spawn();

        var zLevel = EnsureComp<CMUZLevelsNetworkComponent>(ent);
        EnsureComp<CMUPvsOverrideComponent>(ent);

        return (ent, zLevel);
    }

    /// <summary>
    /// Adds the specified map to the zNetwork network at the specified depth after batch validation.
    /// </summary>
    private void AddMapIntoZNetwork(Entity<CMUZLevelsNetworkComponent> network, EntityUid mapUid, int depth)
    {
        network.Comp.ZLevels[depth] = mapUid;
        network.Comp.ZLevelByEntity[mapUid] = depth;

        var levelMapComponent = EnsureComp<CMUZLevelMapComponent>(mapUid);
        levelMapComponent.Depth = depth;
        levelMapComponent.NetworkUid = network;

        if (depth < int.MaxValue && network.Comp.ZLevels.TryGetValue(depth + 1, out var aboveMapUid) &&
            aboveMapUid is { } aboveMap)
        {
            levelMapComponent.MapAbove = aboveMap;

            if (TryComp<CMUZLevelMapComponent>(aboveMap, out var aboveMapComp))
            {
                aboveMapComp.MapBelow = mapUid;
                Dirty(aboveMap, aboveMapComp);
            }
        }

        if (depth > int.MinValue && network.Comp.ZLevels.TryGetValue(depth - 1, out var belowMapUid) &&
            belowMapUid is { } belowMap)
        {
            levelMapComponent.MapBelow = belowMap;

            if (TryComp<CMUZLevelMapComponent>(belowMap, out var belowMapComp))
            {
                belowMapComp.MapAbove = mapUid;
                Dirty(belowMap, belowMapComp);
            }
        }

        Dirty(mapUid, levelMapComponent);
        Dirty(network);
    }

    public bool TryAddMapsIntoZNetwork(Entity<CMUZLevelsNetworkComponent> network, Dictionary<EntityUid, int> maps)
    {
        if (!CanAddMapsIntoZNetwork(network, maps))
            return false;

        foreach (var (ent, depth) in maps)
        {
            AddMapIntoZNetwork(network, ent, depth);
        }

        if (maps.Count > 0)
        {
            NotifyTopologyChanged(network);
            ActivateAttachedMaps(maps);
        }

        return true;
    }

    private void InitializeTopology()
    {
        SubscribeLocalEvent<CMUZLevelMapComponent, ComponentShutdown>(OnZMapShutdown);
        SubscribeLocalEvent<CMUZLevelsNetworkComponent, ComponentShutdown>(OnZNetworkShutdown);
    }

    /// <summary>
    /// Detaches a map without deleting it. Neighbor fields are derived compatibility
    /// data; depth membership remains the authority for traversal and enumeration.
    /// </summary>
    public bool TryRemoveMapFromZNetwork(EntityUid map)
    {
        if (!TryComp<CMUZLevelMapComponent>(map, out var component))
            return false;

        var removed = RemoveZMapMembership((map, component));
        if (removed && component.NetworkUid == EntityUid.Invalid)
            RemComp<CMUZLevelMapComponent>(map);
        return removed;
    }

    private void OnZMapShutdown(Entity<CMUZLevelMapComponent> ent, ref ComponentShutdown args)
    {
        RemoveZMapMembership(ent);
    }

    private bool RemoveZMapMembership(Entity<CMUZLevelMapComponent> ent)
    {
        if (!TryComp<CMUZLevelsNetworkComponent>(ent.Comp.NetworkUid, out var network) ||
            !network.ZLevels.TryGetValue(ent.Comp.Depth, out var member) || member != ent.Owner)
            return false;

        var networkUid = ent.Comp.NetworkUid;
        network.ZLevels.Remove(ent.Comp.Depth);
        network.ZLevelByEntity.Remove(ent.Owner);
        ent.Comp.NetworkUid = EntityUid.Invalid;
        ent.Comp.MapAbove = null;
        ent.Comp.MapBelow = null;
        RebuildZMapNeighbors((networkUid, network));
        if (network.LifeStage <= ComponentLifeStage.Running && !TerminatingOrDeleted(networkUid))
        {
            Dirty(networkUid, network);
            NotifyTopologyChanged((networkUid, network));
        }
        return true;
    }

    private void RebuildZMapNeighbors(Entity<CMUZLevelsNetworkComponent> network)
    {
        foreach (var (depth, map) in network.Comp.ZLevels)
        {
            if (map is not { } uid || TerminatingOrDeleted(uid) ||
                !TryComp<CMUZLevelMapComponent>(uid, out var comp))
                continue;

            comp.MapAbove = depth < int.MaxValue && TryGetMapAtDepth(network, depth + 1, out var above) ? above : null;
            comp.MapBelow = depth > int.MinValue && TryGetMapAtDepth(network, depth - 1, out var below) ? below : null;
            Dirty(uid, comp);
        }
    }

    private void OnZNetworkShutdown(Entity<CMUZLevelsNetworkComponent> ent, ref ComponentShutdown args)
    {
        // Clear membership first: component removal invokes the map shutdown handler.
        var maps = new List<EntityUid>(ent.Comp.ZLevelByEntity.Keys);
        ent.Comp.ZLevels.Clear();
        ent.Comp.ZLevelByEntity.Clear();
        foreach (var map in maps)
        {
            if (!TerminatingOrDeleted(map) &&
                TryComp<CMUZLevelMapComponent>(map, out var comp) && comp.NetworkUid == ent.Owner)
                RemComp<CMUZLevelMapComponent>(map);
        }
        NotifyTopologyChanged(ent);
    }

    private void NotifyTopologyChanged(Entity<CMUZLevelsNetworkComponent> network)
    {
        var ev = new CMUZLevelNetworkUpdatedEvent(network);
        RaiseLocalEvent(ref ev);
        // Topology can change inside transform/component teardown. Probe creation and
        // deletion must wait until hierarchy traversal has finished, including detaches.
        _nextZLevelViewerUpdate = TimeSpan.Zero;
    }

    // A map may belong to at most 1 z-network: this is what IsSameZNetwork's membership check and
    // GetAllNetworkMaps' no-dup guarantee rely on. Overlapping networks would silently change both.
    private bool CanAddMapsIntoZNetwork(Entity<CMUZLevelsNetworkComponent> network, Dictionary<EntityUid, int> maps)
    {
        if (TerminatingOrDeleted(network.Owner) || network.Comp.LifeStage > ComponentLifeStage.Running)
            return false;

        var seenMaps = new HashSet<EntityUid>();
        var seenDepths = new HashSet<int>();

        foreach (var (mapUid, depth) in maps)
        {
            if (TerminatingOrDeleted(mapUid) || !HasComp<MapComponent>(mapUid))
                return false;

            if (!seenMaps.Add(mapUid))
            {
                Log.Warning($"Failed attempt to add maps to ZLevelNetwork {network}: Map {mapUid} appears more than once in the request.");
                return false;
            }

            if (!seenDepths.Add(depth))
            {
                Log.Warning($"Failed attempt to add maps to ZLevelNetwork {network}: Depth {depth} appears more than once in the request.");
                return false;
            }

            if (network.Comp.ZLevels.ContainsKey(depth))
            {
                Log.Warning($"Failed to add map {mapUid} to ZLevelNetwork {network}: This depth is already occupied.");
                return false;
            }

            if (network.Comp.ZLevelByEntity.ContainsKey(mapUid))
            {
                Log.Warning($"Failed attempt to add map {mapUid} to ZLevelNetwork {network} at depth {depth}: This map is already in this network.");
                return false;
            }

            if (!TryGetZNetwork(mapUid, out var otherNetwork))
                continue;

            if (otherNetwork.Value.Owner == network.Owner)
            {
                Log.Warning($"Failed attempt to add map {mapUid} to ZLevelNetwork {network} at depth {depth}: This map is already in this network.");
                return false;
            }

            Log.Warning($"Failed attempt to add map {mapUid} to ZLevelNetwork {network}: This map is already in another network {otherNetwork}.");
            return false;
        }

        return true;
    }
}

/// <summary>
/// Raised when maps are added to or removed from a Z-level network.
/// </summary>
[ByRefEvent]
public readonly record struct CMUZLevelNetworkUpdatedEvent(Entity<CMUZLevelsNetworkComponent> Network);
