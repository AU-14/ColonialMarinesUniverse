using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Traits.NicotineAddiction;

public sealed partial class SatisfyNicotineCraving : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Satisfies nicotine cravings in entities addicted to nicotine.";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<NicotineAddictionSystem>().Smoked(args.TargetEntity);
    }
}
