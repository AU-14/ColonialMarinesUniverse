using Content.Shared._RMC14.Storage;
using Content.Shared.Item;
using Content.Shared.Storage;

namespace Content.Shared.Station;

public abstract partial class SharedStationSpawningSystem
{
    private void RaiseCMStorageItemFill(EntityUid storageUid, EntityUid itemUid, StorageComponent storage)
    {
        if (!TryComp(itemUid, out ItemComponent? item))
            return;

        var ev = new CMStorageItemFillEvent((itemUid, item), storage);
        RaiseLocalEvent(storageUid, ref ev);
    }
}
