/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Alcoholic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Intoxicates for [color=yellow]{PotencyPerSecond * 8}[/color] seconds per tick.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond * 0.5f}[/color] toxin damage with a [color=red]{ActualPotency * 2.5f}%[/color] chance of vomiting.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] liver damage and cause unconsciousness.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.Drunk.TryApplyDrunkenness(args.TargetEntity, TimeSpan.FromSeconds((float)potency * 8f));

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = potency * 0.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        if (system.Random.Prob(0.025f * args.ActualPotency))
            system.RaiseVomit(args.TargetEntity);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical.DamageOrgan<LiverComponent>(args.TargetEntity, potency * 2f, PoisonType);
        system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }
}
