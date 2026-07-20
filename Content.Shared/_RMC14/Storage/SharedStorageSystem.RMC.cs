using Content.Shared.Storage;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    public bool CanInteractRMC(EntityUid user, Entity<StorageComponent> storage, bool silent = true)
    {
        return CanInteract(user, storage, silent: silent);
    }
}
