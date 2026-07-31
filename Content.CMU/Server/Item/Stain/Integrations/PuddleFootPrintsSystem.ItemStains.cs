// ReSharper disable CheckNamespace

using Content.Shared._CMU14.Item.Stain;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;

namespace Content.Server.FootPrint;

public sealed partial class PuddleFootPrintsSystem
{
    [Dependency] private InventorySystem _cmuInventory = default!;
    [Dependency] private RMCReagentSystem _cmuReagents = default!;
    [Dependency] private CMUItemStainSystem _cmuStains = default!;

    private void CMUUpdateShoeStain(EntityUid wearer, Solution solution)
    {
        if (!_cmuInventory.TryGetSlotEntity(wearer, CMUItemStainSystem.ShoesSlot, out var shoes) ||
            shoes is not { } shoesUid ||
            !CMUTryGetDominantReagent(solution, out var reagent))
        {
            return;
        }

        if (reagent.CleansItemStains)
        {
            _cmuStains.TryClean(shoesUid);
            return;
        }

        if (reagent.ItemStain is { } kind)
            _cmuStains.TryStain(shoesUid, kind, reagent.ItemStainColor ?? reagent.SubstanceColor);
    }

    private bool CMUTryGetDominantReagent(Solution solution, out ReagentPrototype reagent)
    {
        reagent = default!;
        var largestQuantity = FixedPoint2.Zero;

        foreach (var quantity in solution.Contents)
        {
            if (!_cmuReagents.TryIndex(quantity.Reagent.Prototype, out var candidate))
                continue;

            if (quantity.Quantity < largestQuantity ||
                quantity.Quantity == largestQuantity && reagent != null &&
                string.CompareOrdinal(candidate.ID, reagent.ID) >= 0)
            {
                continue;
            }

            reagent = candidate;
            largestQuantity = quantity.Quantity;
        }

        return reagent != null;
    }
}
