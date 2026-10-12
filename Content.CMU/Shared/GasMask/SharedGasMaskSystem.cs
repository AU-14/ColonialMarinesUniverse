using Content.Shared._RMC14.Inventory;
using Content.Shared.Atmos.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Examine;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Mycotoxin;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.GasMask;

public sealed partial class SharedGasMaskSystem : EntitySystem
{
    private readonly float _epsilon = 0.001f;

    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedInternalsSystem _internals = default!;
    [Dependency] private IGameTiming _timing = default!;

    /// <summary>How long internals keep out neurotoxin per continuous exposure.</summary>
    public static readonly TimeSpan InternalsNeurotoxinProtection = TimeSpan.FromSeconds(9);

    /// <summary>Time out of the gas after which the next exposure starts the protection timer over.</summary>
    public static readonly TimeSpan NeurotoxinExposureGap = TimeSpan.FromSeconds(1.5);

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
    /// Records that neurotoxin reached the entity and returns true if their internals still keep it out.
    /// Internals hold for <see cref="InternalsNeurotoxinProtection"/> of continuous exposure; stepping out of the gas
    /// for longer than <see cref="NeurotoxinExposureGap"/> starts a fresh exposure.
    /// </summary>
    public bool TryBlockNeurotoxinWithInternals(EntityUid uid)
    {
        if (!IsBreathingInternals(uid))
            return false;

        var now = _timing.CurTime;
        if (!TryComp<InternalsNeurotoxinComponent>(uid, out var exposure))
        {
            exposure = AddComp<InternalsNeurotoxinComponent>(uid);
            exposure.ExposureStart = now;
        }
        else if (now - exposure.LastExposure > NeurotoxinExposureGap)
        {
            exposure.ExposureStart = now;
        }

        exposure.LastExposure = now;
        return now - exposure.ExposureStart < InternalsNeurotoxinProtection;
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
