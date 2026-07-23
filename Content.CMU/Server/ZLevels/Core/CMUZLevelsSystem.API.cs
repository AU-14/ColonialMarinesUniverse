using Content.Server._CMU14.ZLevels.PVS;
using Content.Shared._CMU14.ZLevels.Core.Components;

namespace Content.Server._CMU14.ZLevels.Core;

public sealed partial class CMUZLevelsSystem
{
    /// <summary>
    /// Creates a new entity zLevelNetwork
    /// </summary>
    internal Entity<CMUZLevelsNetworkComponent> CreateZNetwork()
    {
        var ent = Spawn();

        try
        {
            var zLevel = EnsureComp<CMUZLevelsNetworkComponent>(ent);
            EnsureComp<CMUPvsOverrideComponent>(ent);
            return (ent, zLevel);
        }
        catch
        {
            Del(ent);
            throw;
        }
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

    internal void AttachMapsToZNetwork(
        Entity<CMUZLevelsNetworkComponent> network,
        Dictionary<EntityUid, int> maps)
    {
        foreach (var (ent, depth) in maps)
        {
            AddMapIntoZNetwork(network, ent, depth);
        }
    }

    internal void PublishZNetworkUpdated(Entity<CMUZLevelsNetworkComponent> network)
    {
        try
        {
            var ev = new CMUZLevelNetworkUpdatedEvent(network);
            RaiseLocalEvent(ref ev);
        }
        finally
        {
            RefreshViewersForNetwork(network);
        }
    }

}

/// <summary>
/// Raised when maps are added to or removed from a Z-level network.
/// </summary>
[ByRefEvent]
public readonly record struct CMUZLevelNetworkUpdatedEvent(Entity<CMUZLevelsNetworkComponent> Network);
