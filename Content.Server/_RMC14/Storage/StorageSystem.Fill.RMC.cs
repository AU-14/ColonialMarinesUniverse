using Content.Shared._RMC14.Storage;
using Content.Shared.Item;
using Content.Shared.Storage;

namespace Content.Server.Storage.EntitySystems;

public sealed partial class StorageSystem
{
    private void RaiseCMStorageItemFill(
        EntityUid storageUid,
        Entity<ItemComponent> item,
        StorageComponent storage)
    {
        var ev = new CMStorageItemFillEvent(item, storage);
        RaiseLocalEvent(storageUid, ref ev);
    }
}
