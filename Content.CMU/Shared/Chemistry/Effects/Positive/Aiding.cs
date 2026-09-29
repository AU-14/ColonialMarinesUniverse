using Content.Shared.Damage.Systems;
/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Traits.DrugAllergy;
using Content.Shared.EntityEffects;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Aiding : RMCChemicalEffect
{
    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        // The CMSS13 property clears genetic disabilities. The current
        // disability layer is represented by reversible trait reactions, so
        // clear the equivalent drug-allergy disability when present.
        system.AllergicReaction.CureReaction(args.TargetEntity);
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Removes reversible medical disabilities.";
    }
}
