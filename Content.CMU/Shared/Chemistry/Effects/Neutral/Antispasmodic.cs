/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Antispasmodic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";
    private static readonly EntProtoId CardiacArrest = "StatusEffectCMUCardiacArrest";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Removes [color=green]{PotencyPerSecond * 2}[/color] seconds of jitter, stutter and arrhythmia per tick.\n" +
           $"Overdoses depress breathing for [color=red]{PotencyPerSecond}[/color] asphyxiation damage and cause arrhythmia.\n" +
           $"Critical overdoses have a [color=red]5%[/color] chance of cardiac arrest.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;
        var reduction = TimeSpan.FromSeconds((float)potency * 2f);
        system.StatusEffectQuery.TryRemoveTime(target, "Jitter", reduction);
        system.StatusEffects.TryRemoveTime(target, StutteringSystem.StutterEffect, reduction);
        system.StatusEffects.TryRemoveTime(target, Arrhythmia, reduction);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[AsphyxiationType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Arrhythmia, TimeSpan.FromSeconds(3));
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(0.05f))
            system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, CardiacArrest, TimeSpan.FromSeconds(5));
    }
}
