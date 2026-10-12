using Content.Shared._RMC14.Inventory;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.GasMask;

public sealed partial class SharedGasMaskSystem : EntitySystem
{
    private readonly float _epsilon = 0.001f;

    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IComponentFactory _compFactory = default!;

    /// <summary>Internals keep out neurotoxin this many times longer than the best neurotoxin filter.</summary>
    private const float InternalsNeurotoxinFilterMultiplier = 2f;

    private float? _internalsNeurotoxinBudget;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GasMaskFilterComponent, ComponentStartup>(OnComponentInit);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _internalsNeurotoxinBudget = null);
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

    /// <summary>
    /// True when the entity is breathing from a connected tank that still has air in it.
    /// A sealed air supply keeps out gases the same way a working filter does, without wearing anything down.
    /// </summary>
    public bool IsBreathingInternals(EntityUid uid)
    {
        if (!TryComp<InternalsComponent>(uid, out var internals) || !_internals.AreInternalsWorking(internals))
            return false;

        return TryComp<GasTankComponent>(internals.GasTankEntity, out var tank) && tank.Air.TotalMoles > 0;
    }

    /// <summary>
    /// True when the entity's internals still keep out neurotoxin: breathing from a tank with air whose
    /// neurotoxin protection isn't used up.
    /// </summary>
    public bool IsInternalsBlockingNeurotoxin(EntityUid uid)
    {
        if (!IsBreathingInternals(uid))
            return false;

        var tank = Comp<InternalsComponent>(uid).GasTankEntity!.Value;
        return !TryComp<InternalsNeurotoxinComponent>(tank, out var neuro) || neuro.Remaining > _epsilon;
    }

    /// <summary>
    /// Wears down the internals' neurotoxin protection like a filter. Returns true if they still kept this dose out.
    /// </summary>
    public bool TryAbsorbNeurotoxinWithInternals(EntityUid uid, float damage)
    {
        if (!IsInternalsBlockingNeurotoxin(uid))
            return false;

        var tank = Comp<InternalsComponent>(uid).GasTankEntity!.Value;
        if (!TryComp<InternalsNeurotoxinComponent>(tank, out var neuro))
        {
            neuro = AddComp<InternalsNeurotoxinComponent>(tank);
            neuro.Remaining = GetInternalsNeurotoxinBudget();
        }

        neuro.Remaining = Math.Max(0f, neuro.Remaining - damage);
        Dirty(tank, neuro);
        return true;
    }

    /// <summary>
    /// The neurotoxin a fresh tank keeps out: <see cref="InternalsNeurotoxinFilterMultiplier"/> times what the
    /// longest-lasting neurotoxin-resistant filter absorbs.
    /// </summary>
    private float GetInternalsNeurotoxinBudget()
    {
        if (_internalsNeurotoxinBudget is { } cached)
            return cached;

        var best = 0f;
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (proto.Abstract ||
                !proto.TryGetComponent<GasMaskFilterComponent>(out var filter, _compFactory) ||
                !filter.NeurotoxinResist ||
                filter.NeurotoxinDamageMultiplier <= 0)
            {
                continue;
            }

            best = Math.Max(best, filter.BaseIntegrity / filter.NeurotoxinDamageMultiplier);
        }

        _internalsNeurotoxinBudget = best * InternalsNeurotoxinFilterMultiplier;
        return _internalsNeurotoxinBudget.Value;
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
            !_itemSlots.TryGetSlot((ent.Owner, slots), "filter", out var slot) ||
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
