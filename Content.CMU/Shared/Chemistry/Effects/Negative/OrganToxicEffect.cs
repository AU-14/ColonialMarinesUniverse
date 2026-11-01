/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Negative;

/// <summary>
/// Mirror of <see cref="Positive.OrganPeuticEffect{TOrgan}"/>: deals typed organ damage instead of healing it.
/// Baseline organ damage equals the peutic family's overdose damage, so one level of toxin and one level of
/// peutic on the same organ cancel in the long run.
/// </summary>
public abstract partial class OrganToxicEffect<TOrgan> : RMCChemicalEffect where TOrgan : IComponent
{
    protected abstract string OrganName { get; }
    protected abstract ProtoId<DamageTypePrototype> OrganDamageType { get; }

    protected virtual string ExtraText => string.Empty;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Deals [color=red]{PotencyPerSecond}[/color] {OrganName} damage.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond * 2}[/color] {OrganName} damage. {ExtraText}\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 4}[/color] additional {OrganName} damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<TOrgan>(args.TargetEntity, potency, OrganDamageType);

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical.DamageOrgan<TOrgan>(args.TargetEntity, potency * 2f, OrganDamageType);
        OverdoseExtra(system, damageable, potency, args);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical.DamageOrgan<TOrgan>(args.TargetEntity, potency * 4f, OrganDamageType);
        CriticalExtra(system, damageable, potency, args);
    }

    protected virtual void OverdoseExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args) { }
    protected virtual void CriticalExtra(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args) { }
}
