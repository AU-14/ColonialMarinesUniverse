/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Painkilling : RMCChemicalEffect
{
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Suppresses [color=green]{MathF.Min(90f, LinearLevel * 40f)}%[/color] of new pain and up to " +
           $"[color=green]{Math.Max(1, (int)MathF.Ceiling(LinearLevel))}[/color] pain tiers while metabolizing.\n" +
           "Overdoses cause sedation and a 15% movement slowdown.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] asphyxiation, " +
           $"[color=red]{PotencyPerSecond * 5}[/color] liver damage, [color=red]{PotencyPerSecond * 2}[/color] brain damage, and unconsciousness.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var suppression = MathF.Min(0.9f, LinearLevel * 0.4f);
        var tiers = Math.Max(1, (int)MathF.Ceiling(LinearLevel));
        args.EntityManager.System<SharedPainShockSystem>().AddPainSuppressionProfile(
            args.TargetEntity, suppression, tiers, ActualPotency * 0.5f, TimeSpan.FromSeconds(3), 0.2f);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<SharedStunSystem>()
            .TrySlowdown(args.TargetEntity, TimeSpan.FromSeconds(2), true, 0.85f, 0.85f);
        args.EntityManager.System<SharedStatusEffectsSystem>()
            .TrySetStatusEffectDuration(args.TargetEntity, Drowsiness, TimeSpan.FromSeconds(3));
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[AsphyxiationType] = potency * 4f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        var medical = args.EntityManager.System<CMUChemicalMedicalSystem>();
        medical.DamageOrgan<LiverComponent>(args.TargetEntity, potency * 5f, PoisonType);
        medical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 2f, ShockType);
        args.EntityManager.System<SharedStatusEffectsSystem>()
            .TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }
}
