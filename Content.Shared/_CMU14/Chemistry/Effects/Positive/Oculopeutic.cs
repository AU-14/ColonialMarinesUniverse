/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Eye.Blinding.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Oculopeutic : RMCChemicalEffect
{
    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var medicalIndex = args.EntityManager.System<CMUMedicalBodyIndexSystem>();
        var organHealth = args.EntityManager.System<SharedOrganHealthSystem>();
        foreach (var organ in medicalIndex.GetOrgans(args.TargetEntity))
        {
            if (!args.EntityManager.HasComponent<EyesComponent>(organ.Owner))
                continue;

            organHealth.HealOrgan((organ.Owner, null), args.TargetEntity, potency);
        }

        var blindable = args.EntityManager.System<BlindableSystem>();
        blindable.AdjustEyeDamage(args.TargetEntity, -(int)MathF.Max(1f, potency.Float() * 2f));
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Heals eye damage and reduces blindness.";
    }
}
