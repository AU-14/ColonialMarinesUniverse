using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Traits.Asthmatic;

public sealed partial class ClearRespiratoryStrain : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Clears respiratory strain in asthmatic entities.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<RespiratoryStrainSystem>().ClearStrain(args.TargetEntity);
    }
}
