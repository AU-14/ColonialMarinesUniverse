namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    private readonly Queue<(EntityUid Storage, EntityUid Item)> _invalidStorageInsertions = new();

    private bool TryAssignInsertedStorageLocation(Entity<StorageComponent> storage, EntityUid item)
    {
        if (!TryGetAvailableGridSpace((storage.Owner, storage.Comp), item, out var location))
            return false;

        storage.Comp.StoredItems[item] = location.Value;
        AddOccupiedEntity(storage, item, location.Value);
        Dirty(storage);
        return true;
    }

    private void QueueInvalidStorageInsertion(EntityUid storage, EntityUid item)
    {
        // Forced insertions can bypass capacity checks. Removing from an insertion callback
        // invalidates the container transaction; reconcile only after all its events return.
        _invalidStorageInsertions.Enqueue((storage, item));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var count = _invalidStorageInsertions.Count;
        for (var i = 0; i < count; i++)
        {
            var (storageUid, item) = _invalidStorageInsertions.Dequeue();
            if (TerminatingOrDeleted(storageUid) || TerminatingOrDeleted(item) ||
                !TryComp<StorageComponent>(storageUid, out var storage) ||
                !storage.Container.Contains(item) || storage.StoredItems.ContainsKey(item))
            {
                continue;
            }

            if (!TryAssignInsertedStorageLocation((storageUid, storage), item))
            {
                ContainerSystem.Remove(item, storage.Container, force: true);
                continue;
            }

            UpdateAppearance((storageUid, storage, null));
            UpdateUI((storageUid, storage));
        }
    }
}
