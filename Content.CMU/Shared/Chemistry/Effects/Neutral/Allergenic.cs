/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Traits.DrugAllergy;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Neutral;

public sealed partial class Allergenic : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Triggers an ongoing allergic reaction in entities allergic to this reagent, until treated with naloxone.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.Reagent is not { } reagent)
            return;

        if (!args.EntityManager.TryGetComponent(args.TargetEntity, out DrugAllergyComponent? allergy) ||
            allergy.Allergen != reagent.ID)
        {
            return;
        }

        args.EntityManager.System<AllergicReactionSystem>().TriggerReaction(args.TargetEntity);
    }
}
