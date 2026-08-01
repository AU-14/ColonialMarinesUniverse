using System;
using Content.Shared._CMU14.Item.Stain;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.Chemistry;
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
        SubscribeLocalEvent<ItemComponent, ReactionEntityEvent>(OnItemReaction);
        SubscribeLocalEvent<InventoryComponent, ReactionEntityEvent>(OnInventoryReaction);
    }

    private void OnItemReaction(Entity<ItemComponent> ent, ref ReactionEntityEvent args)
    {
        OnReaction(ent.Owner, ref args);
    }

    private void OnInventoryReaction(Entity<InventoryComponent> ent, ref ReactionEntityEvent args)
    {
        OnReaction(ent.Owner, ref args);
    }

    private void OnReaction(EntityUid uid, ref ReactionEntityEvent args)
    {
        if (args.Method != ReactionMethod.Touch || !TrySelectReagent(args, out var reagent))
            return;

        if (reagent.CleansItemStains)
        {
            if (HasComp<ItemComponent>(uid))
                _stains.TryClean(uid);
            else if (HasComp<InventoryComponent>(uid))
                _stains.CleanExposedEquipment(uid);
            return;
        }

        if (reagent.ItemStain is not { } kind)
            return;

        var color = reagent.ItemStainColor ?? reagent.SubstanceColor;
        if (HasComp<ItemComponent>(uid))
            _stains.TryStain(uid, kind, color);
        else if (HasComp<InventoryComponent>(uid))
            _stains.StainExposedEquipment(uid, kind, color);
    }

    /// <summary>
    /// Cleaning reagents win mixed contacts. Otherwise the greatest stain-capable quantity wins,
    /// with prototype ID as a stable tie breaker.
    /// </summary>
    private bool TrySelectReagent(ReactionEntityEvent args, out ReagentPrototype reagent)
    {
        reagent = args.Reagent;
        return reagent.CleansItemStains || reagent.ItemStain != null;
    }
}
