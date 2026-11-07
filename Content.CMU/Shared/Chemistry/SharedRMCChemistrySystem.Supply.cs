using Content.Shared.CMU14.ZLevels.Core.EntitySystems;

namespace Content.Shared._RMC14.Chemistry;

public abstract partial class SharedRMCChemistrySystem
{
    [Dependency] private CMUSharedZLevelsSystem _cmuZLevels = default!;

    private bool CMUShareChemicalSupply(EntityUid storage, EntityUid dispenser)
    {
        var storageTransform = Transform(storage);
        var dispenserTransform = Transform(dispenser);
        if (storageTransform.GridUid is { } grid && grid == dispenserTransform.GridUid)
            return true;

        // Only the network's map-as-grid decks share supplies. A docked grid remains independent
        // even when the dispenser being considered is on another deck's map.
        return storageTransform.MapUid is { } storageMap
               && dispenserTransform.MapUid is { } dispenserMap
               && storageTransform.GridUid == storageMap
               && dispenserTransform.GridUid == dispenserMap
               && storageMap != dispenserMap && _cmuZLevels.IsSameZNetwork(storageMap, dispenserMap);
    }
}
