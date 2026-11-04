using Content.Shared._RMC14.Attachable.Systems;

namespace Content.Shared.Inventory;

public abstract partial class InventorySystem
{
    [Dependency] private AttachableHolderSystem _cmuAttachments = default!;

    private bool CMUHasIndependentSuitStorage(EntityUid item, string slot)
        => slot == "suitstorage" && _cmuAttachments.HasCMUSling(item);
}
