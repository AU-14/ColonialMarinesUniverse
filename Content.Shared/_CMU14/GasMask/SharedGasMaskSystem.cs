using Content.Shared._RMC14.Inventory;
using Content.Shared.Examine;
using Content.Shared._CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;

namespace Content.Shared._CMU14.GasMask;

public sealed partial class SharedGasMaskSystem : EntitySystem
{
    private readonly float _epsilon = 0.001f;

    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasMaskFilterComponent, ComponentStartup>(OnComponentInit);
        SubscribeLocalEvent<GasMaskFilterComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MycotoxinProtectionComponent, ExaminedEvent>(OnMaskExamined);
    }

    private void OnComponentInit(Entity<GasMaskFilterComponent> ent, ref ComponentStartup args)
    {
        ent.Comp.Integrity = ent.Comp.BaseIntegrity;
    }

    public bool IsFilterBroken(Entity<GasMaskFilterComponent> ent)
    {
        return ent.Comp.Integrity == 0f || ent.Comp.Integrity <= _epsilon;
    }

    public void DamageFilter(EntityUid uid, GasMaskFilterComponent comp, GasMaskFilterDamageComponent dam)
    {
        float newhp = comp.Integrity;
        newhp -= dam.Neurotoxin
            ? dam.Damage * comp.NeurotoxinDamageMultiplier
            : dam.Damage;
        if (newhp <= _epsilon) newhp = 0f;
        comp.Integrity = newhp;
        Dirty(uid, comp);
    }

    // Float overload for mycotoxin system
    public void DamageFilter(EntityUid uid, GasMaskFilterComponent comp, float damage)
    {
        var newhp = comp.Integrity - damage;
        if (newhp <= _epsilon) newhp = 0f;
        comp.Integrity = newhp;
        Dirty(uid, comp);
    }

    private void OnExamined(Entity<GasMaskFilterComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(GasMaskFilterComponent)))
        {
            if (IsFilterBroken(ent))
            {
                args.PushMarkup(Loc.GetString("gas-mask-filter-broken"));
            }
            else
            {
                args.PushMarkup(Loc.GetString("gas-mask-filter-integrity-percentage",
                    ("percent", (ent.Comp.Integrity / ent.Comp.BaseIntegrity) * 100f)));
                if (ent.Comp.NeurotoxinResist)
                    args.PushMarkup(Loc.GetString("gas-mask-filter-super"));
            }
        }
    }

    private void OnMaskExamined(Entity<MycotoxinProtectionComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<ItemSlotsComponent>(ent.Owner, out var slots) ||
            !_itemSlots.TryGetSlot(ent.Owner, "filter", out var slot, slots) ||
            slot.ContainerSlot?.ContainedEntity is not { } filterEnt ||
            !TryComp(filterEnt, out GasMaskFilterComponent? filter))
        {
            args.PushMarkup(Loc.GetString("cmu-gasmask-no-filter"));
            return;
        }

        if (IsFilterBroken((filterEnt, filter)))
        {
            args.PushMarkup(Loc.GetString("cmu-gasmask-filter-broken"));
            return;
        }

        var pct = (int)(filter.Integrity / filter.BaseIntegrity * 100f);
        var color = pct > 50 ? "green" : pct > 20 ? "yellow" : "red";
        args.PushMarkup(Loc.GetString("cmu-gasmask-filter-integrity",
            ("percent", pct), ("color", color)));
    }
}