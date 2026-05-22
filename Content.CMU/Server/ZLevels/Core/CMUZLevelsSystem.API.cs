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
    /// Attempts to add the specified map to the zNetwork network at the specified depth
    /// </summary>
    private bool TryAddMapIntoZNetwork(Entity<CMUZLevelsNetworkComponent> network, EntityUid mapUid, int depth)
    {
        if (network.Comp.ZLevels.ContainsKey(depth))
        {
            Log.Error($"Failed to add map {mapUid} to ZLevelNetwork {network}: This depth is already occupied.");
            return false;
        }

        if (TryGetZNetwork(mapUid, out var otherNetwork))
        {
            Log.Error($"Failed attempt to add map {mapUid} to ZLevelNetwork {network}: This map is already in another network {otherNetwork}.");
            return false;
        }

        if (network.Comp.ZLevelByEntity.ContainsKey(mapUid))
        {
            Log.Error($"Failed attempt to add map {mapUid} to ZLevelNetwork {network} at depth {depth}: This map is already in this network.");
            return false;
        }

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

        return true;
    }

    public bool TryAddMapsIntoZNetwork(Entity<CMUZLevelsNetworkComponent> network, Dictionary<EntityUid, int> maps)
    {
        var success = true;
        foreach (var (ent, depth) in maps)
        {
            if (!TryAddMapIntoZNetwork(network, ent, depth))
                success = false;
        }

        RaiseLocalEvent(network, new CMUZLevelNetworkUpdatedEvent());

        return success;
    }
}

/// <summary>
/// Called on ZLevel Network Entity, when maps added or removed from network
/// </summary>
public sealed class CMUZLevelNetworkUpdatedEvent : EntityEventArgs;
