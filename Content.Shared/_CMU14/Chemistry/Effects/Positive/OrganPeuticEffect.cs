using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public abstract partial class OrganPeuticEffect<TOrgan> : RMCChemicalEffect where TOrgan : IComponent
{
    protected abstract string OrganName { get; }
    protected abstract ProtoId<DamageTypePrototype> OrganDamageType { get; }
    protected abstract string PlantEffect { get; }
    protected virtual bool RestartHeart => false;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Heals [color=green]{PotencyPerSecond * 2}[/color] {OrganName} health. {PlantEffect}\n" +
           $"Overdoses cause [color=red]{PotencyPerSecond}[/color] {OrganName} damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] additional {OrganName} damage.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<CMUChemicalMedicalSystem>()
            .HealOrgan<TOrgan>(args.TargetEntity, potency * 2f, RestartHeart);

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<TOrgan>(args.TargetEntity, potency, OrganDamageType);

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<TOrgan>(args.TargetEntity, potency * 4f, OrganDamageType);
}
