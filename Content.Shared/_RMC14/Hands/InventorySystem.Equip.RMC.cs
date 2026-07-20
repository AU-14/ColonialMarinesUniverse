using Content.Shared._RMC14.Hands;

namespace Content.Shared.Inventory;

public abstract partial class InventorySystem
{
    [Dependency] private RMCHandsSystem _rmcHandsEquip = default!;

    private bool TryRMCStorageEjectHand(EntityUid user, EntityUid item)
    {
        return _rmcHandsEquip.TryStorageEjectHand(user, item);
    }
}
