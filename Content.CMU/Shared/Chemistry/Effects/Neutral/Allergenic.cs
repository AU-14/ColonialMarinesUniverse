/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Allergenic : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Triggers an ongoing allergic reaction, until treated with naloxone, in anyone allergic to this reagent, " +
               "to a reagent of its class, or to a drug that shares one of its properties.";
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        // Generated chemicals never match a pre-rolled allergen by ID, so the allergy is resolved by the
        // reagent's class and properties as well (AllergicReactionSystem.IsAllergen).
        if (!system.IsAllergen(args.TargetEntity, args.Reagent))
            return;

        system.AllergicReaction.TriggerReaction(args.TargetEntity);
    }
}
