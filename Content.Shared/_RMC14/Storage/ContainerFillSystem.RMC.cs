using Content.Shared._RMC14.Storage;
using Content.Shared.Item;
using Content.Shared.Storage;

namespace Content.Shared.Containers;

public sealed partial class ContainerFillSystem
{
    private void RaiseCMStorageItemFill(EntityUid storageUid, EntityUid itemUid)
    {
        if (!TryComp(storageUid, out StorageComponent? storage) ||
            !TryComp(itemUid, out ItemComponent? item))
        {
            return;
        }

        var ev = new CMStorageItemFillEvent((itemUid, item), storage);
        RaiseLocalEvent(storageUid, ref ev);
    }
}
