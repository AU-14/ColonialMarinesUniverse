/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Yautjahemogenic : RMCChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (!args.EntityManager.TryGetComponent<BloodstreamComponent>(args.TargetEntity, out var bloodstream))
            return;

        // CMSS13's Yautja hemogenic property restores blood without the
        // hunger drain used by the generic Hemogenic property.
        var bloodstreamSystem = args.EntityManager.System<SharedBloodstreamSystem>();
        bloodstreamSystem.TryModifyBloodLevel((args.TargetEntity, bloodstream), potency);
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Restores [color=green]{PotencyPerSecond}[/color]cl of Yautja blood without draining nutrition.";
    }
}
