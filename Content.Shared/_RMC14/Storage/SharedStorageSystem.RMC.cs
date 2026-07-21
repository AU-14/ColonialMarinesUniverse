using Content.Shared._RMC14.Hands;
using Content.Shared.Storage;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    [Dependency] private RMCHandsSystem _rmcHandsStorage = default!;

    public bool CanInteractRMC(EntityUid user, Entity<StorageComponent> storage, bool silent = true)
    {
        return CanInteract(user, storage, silent: silent);
    }

    public void UpdateOccupiedRMC(Entity<StorageComponent> storage)
    {
        UpdateOccupied(storage);
    }

    private bool TryRMCStorageEjectHand(EntityUid user, EntityUid item)
    {
        return _rmcHandsStorage.TryStorageEjectHand(user, item);
    }
}
