using Content.Shared.Item;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;

namespace Content.Shared._RMC14.Inventory;

public static class CMInventoryExtensions
{
    // CMU14 method: preserve unrotated compaction while building occupancy once per search.
    public static bool TryGetFirst(EntityUid storageId, EntityUid itemId, out ItemStorageLocation location)
    {
        location = default;

        var entities = IoCManager.Resolve<IEntityManager>();
        var storageSystem = entities.System<SharedStorageSystem>();

        if (!entities.TryGetComponent(storageId, out StorageComponent? storage) ||
            !entities.TryGetComponent(itemId, out ItemComponent? item))
        {
            return false;
        }

        return storageSystem.TryGetFirstUnrotatedStorageLocation((storageId, storage), (itemId, item), out location);
    }
}
