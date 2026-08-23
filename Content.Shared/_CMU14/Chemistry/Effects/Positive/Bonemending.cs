/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Bonemending : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Restores [color=green]{PotencyPerSecond * 4}[/color] bone integrity to splinted or cast non-shattered fractures.\n" +
           "Overdoses cause malunion in an existing fracture.\n" +
           "Critical overdoses worsen an existing fracture by one severity.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<SharedBoneSystem>()
            .ChemicallyMendFractures(args.TargetEntity, potency * 4f);

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<SharedBoneSystem>().ApplyChemicalMalunion(args.TargetEntity);

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<SharedBoneSystem>().WorsenChemicalFracture(args.TargetEntity);
}
