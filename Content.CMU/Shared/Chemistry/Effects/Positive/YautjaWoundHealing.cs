using Content.Shared.Damage.Systems;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Damage;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

/// <summary>
///     Thwei is the Yautja wound-healing reagent. Besides restoring the
///     damage pools, it closes active wound entries so their surface bleeding
///     stops and the normal wound-healing tick can finish the injury.
/// </summary>
public sealed partial class YautjaWoundHealing : RMCChemicalEffect
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var rmcDamageable = system.RMCDamageable;
        var healing = rmcDamageable.DistributeHealingCached(args.TargetEntity, BruteGroup, potency);
        healing = rmcDamageable.DistributeHealingCached(args.TargetEntity, BurnGroup, potency, healing);
        damageable.TryChangeDamage(args.TargetEntity, healing, true, interruptsDoAfters: false);

        var medicalIndex = system.MedicalBodyIndex;
        var wounds = system.Wounds;
        var maxWounds = Math.Max(1, (int)MathF.Ceiling(potency.Float()));
        foreach (var part in medicalIndex.GetBodyParts(args.TargetEntity))
        {
            wounds.TryTreatWounds(
                part.Owner,
                WoundType.Brute,
                maxWounds,
                out _,
                quality: WoundTreatmentQuality.Adequate,
                stopArterialBleeding: true);
            wounds.TryTreatWounds(
                part.Owner,
                WoundType.Burn,
                maxWounds,
                out _,
                quality: WoundTreatmentQuality.Adequate,
                stopArterialBleeding: true);
        }
    }

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Heals [color=green]{PotencyPerSecond}[/color] brute and burn damage and closes active wounds.";
    }
}
