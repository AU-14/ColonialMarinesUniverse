using System;
using Content.Shared._CMU14.Item.Stain;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Item;

namespace Content.Server._CMU14.Item.Stain;

/// <summary>
/// Converts reagent touch reactions into stains on items and exposed equipment.
/// </summary>
public sealed partial class CMUItemStainReactionSystem : EntitySystem
{
    [Dependency] private CMUItemStainSystem _stains = default!;
    [Dependency] private RMCReagentSystem _reagents = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ReactiveComponent, ReactionEntityEvent>(OnReaction);
    }

    private void OnReaction(Entity<ReactiveComponent> ent, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || !TrySelectReagent(args, out var reagent))
            return;

        if (reagent.CleansItemStains)
        {
            if (HasComp<ItemComponent>(ent))
                _stains.TryClean(ent);
            else if (HasComp<InventoryComponent>(ent))
                _stains.CleanExposedEquipment(ent);
            return;
        }

        if (reagent.ItemStain is not { } kind)
            return;

        var color = reagent.ItemStainColor ?? reagent.SubstanceColor;
        if (HasComp<ItemComponent>(ent))
            _stains.TryStain(ent, kind, color);
        else if (HasComp<InventoryComponent>(ent))
            _stains.StainExposedEquipment(ent, kind, color);
    }

    /// <summary>
    /// Cleaning reagents win mixed contacts. Otherwise the greatest stain-capable quantity wins,
    /// with prototype ID as a stable tie breaker.
    /// </summary>
    private bool TrySelectReagent(ReactionEntityEvent args, out ReagentPrototype reagent)
    {
        reagent = args.Reagent;
        if (args.Source == null)
            return reagent.CleansItemStains || reagent.ItemStain != null;

        ReagentPrototype? selectedCleaner = null;
        ReagentPrototype? selectedStain = null;
        var selectedQuantity = FixedPoint2.Zero;

        foreach (var quantity in args.Source.Contents)
        {
            if (!_reagents.TryIndex(quantity.Reagent.Prototype, out var candidate))
                continue;

            if (candidate.CleansItemStains)
            {
                if (selectedCleaner == null ||
                    string.CompareOrdinal(candidate.ID, selectedCleaner.ID) < 0)
                {
                    selectedCleaner = candidate;
                }
                continue;
            }

            if (candidate.ItemStain == null)
                continue;

            if (selectedStain == null ||
                quantity.Quantity > selectedQuantity ||
                quantity.Quantity == selectedQuantity &&
                string.CompareOrdinal(candidate.ID, selectedStain.ID) < 0)
            {
                selectedStain = candidate;
                selectedQuantity = quantity.Quantity;
            }
        }

        reagent = selectedCleaner ?? selectedStain ?? args.Reagent;
        return (reagent.CleansItemStains || reagent.ItemStain != null) && reagent.ID == args.Reagent.ID;
    }
}
