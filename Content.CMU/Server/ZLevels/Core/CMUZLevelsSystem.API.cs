using Content.Server._CMU14.ZLevels.PVS;
using Content.Shared._CMU14.ZLevels.Core.Components;
using JetBrains.Annotations;

namespace Content.Server._CMU14.ZLevels.Core;

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

        if (network.Comp.ZLevels.TryGetValue(depth + 1, out var aboveMapUid) &&
            aboveMapUid is { } aboveMap)
        {
            levelMapComponent.MapAbove = aboveMap;

            if (TryComp<CMUZLevelMapComponent>(aboveMap, out var aboveMapComp))
            {
                aboveMapComp.MapBelow = mapUid;
                Dirty(aboveMap, aboveMapComp);
            }
        }

        if (network.Comp.ZLevels.TryGetValue(depth - 1, out var belowMapUid) &&
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
            RaiseLocalEvent(network, new CMUZLevelNetworkUpdatedEvent());

        return true;
    }

    private bool CanAddMapsIntoZNetwork(Entity<CMUZLevelsNetworkComponent> network, Dictionary<EntityUid, int> maps)
    {
        var seenMaps = new HashSet<EntityUid>();
        var seenDepths = new HashSet<int>();

        foreach (var (mapUid, depth) in maps)
        {
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
/// Called on ZLevel Network Entity, when maps added or removed from network
/// </summary>
public sealed class CMUZLevelNetworkUpdatedEvent : EntityEventArgs;
