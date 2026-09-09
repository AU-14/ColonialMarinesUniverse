using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nutrition.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects.Neutral;

public sealed partial class Nutritious : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        var updatedFactor = NutrimentFactor + ActualPotency;
        return $"Restores [color=green]{updatedFactor * ActualPotency}[/color] nutrients to the body and satiates hunger";
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;

        if (system.MobState.IsDead(target) ||
            !system.TryGetSatiation(target, out var satiation))
            return;

        var updatedFactor = NutrimentFactor + args.ActualPotency;
        system.Satiation.ModifyValue((target, satiation), SatiationSystem.Hunger, updatedFactor * args.ActualPotency);
    }
}
