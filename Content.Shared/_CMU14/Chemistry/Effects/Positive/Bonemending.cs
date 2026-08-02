/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared.EntityEffects;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Bonemending : RMCChemicalEffect
{
    protected override void Tick(Content.Shared.Damage.DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var status = args.EntityManager.System<SharedStatusEffectsSystem>();
        if (!status.TryAddStatusEffectDuration(
                args.TargetEntity,
                "StatusEffectCMUBoneRegenBoost",
                out var effect,
                TimeSpan.FromSeconds(10)))
        {
            return;
        }

        var boost = args.EntityManager.EnsureComponent<BoneRegenBoostComponent>(effect.Value);
        var multiplier = MathF.Max(1f, 1f + PotencyPerSecond * 0.5f);
        if (boost.Multiplier < multiplier)
        {
            boost.Multiplier = multiplier;
            args.EntityManager.Dirty(effect.Value, boost);
        }
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Accelerates recovery of correctly positioned fractures.";
    }
}
