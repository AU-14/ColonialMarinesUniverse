using Content.Shared._RMC14.Inventory;
using Content.Shared.CMU14.Clothing;

namespace Content.Shared.Inventory;

public abstract partial class InventorySystem
{
    public bool IsHolsterCovered(EntityUid wearer, EntityUid? viewer, CMHolsterComponent holster)
    {
        return holster.Concealable && viewer != wearer &&
            TryGetSlotEntity(wearer, "outerClothing", out var coat) &&
            HasComp<CMUConcealsHolstersComponent>(coat);
    }
}
