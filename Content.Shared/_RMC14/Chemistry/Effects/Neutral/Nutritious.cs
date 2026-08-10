using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects.Neutral;

public sealed partial class Nutritious : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var updatedFactor = NutrimentFactor + Potency;
        return $"Restores [color=green]{updatedFactor * ActualPotency}[/color] nutrients to the body and satiates hunger";
    }

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, RMCChemicalEffectArgs args)
    {
        var mobState = args.EntityManager.System<MobStateSystem>();
        if (mobState.IsDead(args.TargetEntity))
            return;

        var updatedFactor = NutrimentFactor + Potency;
        if (args.EntityManager.TryGetComponent<SatiationComponent>(args.TargetEntity, out var satiation))
        {
            args.EntityManager.System<SatiationSystem>()
                .ModifyValue((args.TargetEntity, satiation), SatiationSystem.Hunger, updatedFactor * ActualPotency);
        }
    }
}
