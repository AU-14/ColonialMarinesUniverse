using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.CMU14.Attachable;

namespace Content.Shared.Inventory;

public abstract partial class InventorySystem
{
    [Dependency] private AttachableHolderSystem _cmuAttachments = default!;
    [Dependency] private CMUSlingSystem _cmuSlings = default!;

    private bool CMUHasIndependentSuitStorage(EntityUid item, string slot)
        => slot == "suitstorage" &&
           _cmuAttachments.HasCMUSlingCarryMode(item, CMUSlingCarryMode.SuitStorage);

    private bool CMUIsVestSling(EntityUid item)
        => _cmuAttachments.HasCMUSlingCarryMode(item, CMUSlingCarryMode.Vest);

    private bool CMUCanEquipVestSling(EntityUid item, string slot)
        => slot == "outerClothing" && CMUIsVestSling(item);

    private bool CMURejectVestSlingSuitStorage(EntityUid item, string slot)
        => slot == "suitstorage" && CMUIsVestSling(item);

    private bool CMUTryRetrieveVestSling(EntityUid user, string slot, EntityUid? item)
        => slot == "outerClothing" &&
           item is { } vest &&
           _cmuSlings.TryRetrieveFromVest(vest, user);
}
