/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Painkilling : RMCChemicalEffect
{
    protected override void Tick(Content.Shared.Damage.DamageableSystem damageable, Content.Shared.FixedPoint.FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var pain = args.EntityManager.System<SharedPainShockSystem>();
        pain.AddPainSuppressionProfile(
            args.TargetEntity,
            Math.Clamp(PotencyPerSecond * 0.25f, 0f, 1f),
            PotencyPerSecond >= 1f ? 1 : 0,
            0.5f,
            TimeSpan.FromSeconds(2));
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Reduces pain and pain-shock accumulation.";
    }
}
