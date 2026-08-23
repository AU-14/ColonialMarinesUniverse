/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Traits.DrugAllergy;
using Content.Shared.EntityEffects;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Aiding : RMCChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        // The CMSS13 property clears genetic disabilities. The current
        // disability layer is represented by reversible trait reactions, so
        // clear the equivalent drug-allergy disability when present.
        args.EntityManager.System<AllergicReactionSystem>().CureReaction(args.TargetEntity);
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Removes reversible medical disabilities.";
    }
}
