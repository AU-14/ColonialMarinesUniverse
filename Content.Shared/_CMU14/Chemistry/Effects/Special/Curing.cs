/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Threats.Mobs.Abomination;
using Content.Shared._CMU14.Traits.DrugAllergy;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Special;

public sealed partial class Curing : RMCChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<AllergicReactionSystem>().CureReaction(args.TargetEntity);
        if (potency < 1f || !args.EntityManager.HasComponent<AbominationInfectionComponent>(args.TargetEntity))
            return;

        args.EntityManager.RemoveComponent<AbominationInfectionComponent>(args.TargetEntity);
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Cures active infections and microbiological agents.";
    }
}
