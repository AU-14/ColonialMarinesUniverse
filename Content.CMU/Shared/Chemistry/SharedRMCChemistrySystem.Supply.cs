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

        // Separate grids sharing a map (such as a docked dropship) keep their own supply.
        return storageTransform.MapUid is { } storageMap
               && dispenserTransform.MapUid is { } dispenserMap
               && storageMap != dispenserMap && _cmuZLevels.IsSameZNetwork(storageMap, dispenserMap);
    }
}
