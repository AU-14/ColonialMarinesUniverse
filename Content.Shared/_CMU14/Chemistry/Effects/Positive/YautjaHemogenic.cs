/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Yautjahemogenic : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Restores [color=green]{PotencyPerSecond * 2}[/color]cl of blood to Yautja without consuming nutrition. " +
           "It has no effect on other species.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (!system.HasYautja(args.TargetEntity) ||
            !system.TryGetBloodstream(args.TargetEntity, out var bloodstream))
        {
            return;
        }

        system.Bloodstream
            .TryModifyBloodLevel((args.TargetEntity, bloodstream), potency * 2f);
    }
}
