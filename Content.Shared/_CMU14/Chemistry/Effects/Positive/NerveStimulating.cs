/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffect;
using Content.Shared.StatusEffectNew;
using Content.Shared._RMC14.Stun;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Nervestimulating : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Improves movement by [color=green]{MathF.Min(30f, ActualPotency * 10f)}%[/color], shortens newly applied stuns by " +
           $"[color=green]{MathF.Min(50f, ActualPotency * 15f)}%[/color], and removes [color=green]{ActualPotency}[/color] seconds " +
           "from existing stun, knockdown, daze, drowsiness, jitter, and stutter effects per tick.\n" +
           $"Overdoses increase pain sensitivity and add [color=red]{PotencyPerSecond * 2}[/color] pain.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] brain damage, " +
           $"[color=red]{PotencyPerSecond}[/color] brute and burn damage, and [color=red]{PotencyPerSecond * 3}[/color] toxin damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalPropertyStatus
            .ApplyNerveStimulation(args.TargetEntity, args.ActualPotency, args.Reagent.ID);

        var reduction = TimeSpan.FromSeconds(args.ActualPotency * args.Scale);
        var oldStatuses = system.StatusEffectQuery;
        var statuses = system.StatusEffects;
        system.Stun.TryRemoveStunAndKnockdownTime(args.TargetEntity, reduction);
        oldStatuses.TryRemoveTime(args.TargetEntity, "Jitter", reduction);
        statuses.TryRemoveTime(args.TargetEntity, StutteringSystem.StutterEffect, reduction);

        statuses.TryAddTime(args.TargetEntity, RMCDazedSystem.StatusEffectDazed, -reduction);
        statuses.TryAddTime(args.TargetEntity, "StatusEffectDrowsiness", -reduction);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalPropertyStatus
            .ApplyPainSensitivity(args.TargetEntity,
                1f + MathF.Min(0.75f, args.ActualPotency * 0.1f),
                args.Reagent!.ID);
        system.PainShock.AddPainPulse(args.TargetEntity, potency * 2f);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical
            .DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 4f, ShockType);
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency;
        damage.DamageDict[HeatType] = potency;
        damage.DamageDict[PoisonType] = potency * 3f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
