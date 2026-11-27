using Content.Shared._RMC14.Inventory;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory;

namespace Content.Shared.Strip;

public abstract partial class SharedStrippableSystem
{
    public bool IsStripHidden(SlotDefinition slot, EntityUid? viewer, EntityUid wearer, EntityUid item)
    {
        return IsStripHidden(slot, viewer) ||
            TryComp(item, out CMHolsterComponent? holster) &&
            _inventorySystem.IsHolsterCovered(wearer, viewer, holster) &&
            !HasComp<BypassInteractionChecksComponent>(viewer);
    }
}
