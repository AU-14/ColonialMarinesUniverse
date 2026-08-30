/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Medical.Defibrillator;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.StatusEffectNew;
using Content.Shared.Traits.Assorted;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

public sealed partial class Defibrillating : RMCChemicalEffect
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageGroupPrototype> ToxinGroup = "Toxin";
    private static readonly ProtoId<DamageGroupPrototype> GeneticGroup = "Genetic";
    private static readonly ProtoId<DamageGroupPrototype> AirlossGroup = "Airloss";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    private static readonly ProtoId<DamageTypePrototype> AsphyxiationType = "Asphyxiation";
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";

    protected override bool ProcessOnDead => true;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Chemically paces an existing heart. In a revivable corpse it heals [color=green]{PotencyPerSecond * 2}[/color] " +
           "brute, burn, toxin, and genetic damage per second, clears airloss, and revives once total damage is viable. " +
           "Electrogenetic in the bloodstream is triggered and consumes one unit.\n" +
           $"Overdoses cause arrhythmia, [color=red]{PotencyPerSecond}[/color] heart damage, " +
           $"[color=red]{PotencyPerSecond * 2}[/color] airloss, and chest pain.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] additional heart damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.TryGetMobState(args.TargetEntity, out var mobState) &&
            mobState.CurrentState == MobState.Dead)
        {
            TickDead(system, damageable, potency, args, mobState);
            return;
        }

        var index = system.MedicalBodyIndex;
        if (!index.TryGetOrgan<HeartComponent>(args.TargetEntity, out var heart) ||
            !system.TryGetHeart(heart, out var heartComp))
        {
            return;
        }

        system.ChemicalPropertyStatus
            .ApplyCardiacPacing(args.TargetEntity, args.ActualPotency, args.Reagent.ID);
        system.Heart.TryRestartHeart((heart, heartComp));
    }

    private static void TickDead(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args,
        MobStateComponent mobState)
    {
        var target = args.TargetEntity;
        if (system.HasUnrevivable(target) ||
            !system.TryGetMobThresholds(target, out var thresholds) ||
            !system.TryGetDamageable(target, out var damageComponent))
        {
            return;
        }

        // A corpse can remain in the heart system's "beating" transition state until its next
        // periodic pulse update. Chemical pacing must not silently fail during that window.
        if (!system.CanChemicallyDefibrillate(target))
            return;

        var rmcDamage = system.RMCDamageable;
        var perGroup = potency * 2f;
        var heal = rmcDamage.DistributeHealingCached(target, BruteGroup, perGroup);
        heal = rmcDamage.DistributeHealingCached(target, BurnGroup, perGroup, heal);
        heal = rmcDamage.DistributeHealingCached(target, ToxinGroup, perGroup, heal);
        heal = rmcDamage.DistributeHealingCached(target, GeneticGroup, perGroup, heal);
        heal = rmcDamage.DistributeHealingCached(target, AirlossGroup, FixedPoint2.Max(perGroup, 200), heal);
        system.Defibrillator.TryApplyElectrogenetic(target, ref heal);
        damageable.TryChangeDamage(target, heal, true, interruptsDoAfters: false);

        var thresholdSystem = system.MobThreshold;
        if (!thresholdSystem.TryGetThresholdForState(target, MobState.Dead, out var deadThreshold) ||
            damageable.GetTotalDamage((target, damageComponent)) >= deadThreshold)
        {
            return;
        }

        system.MobState.ChangeMobState(target, MobState.Critical, mobState, target);
        thresholdSystem.VerifyThresholds(target, thresholds, mobState, damageComponent);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical
            .DamageOrgan<HeartComponent>(args.TargetEntity, potency, ShockType);
        system.StatusEffects
            .TrySetStatusEffectDuration(args.TargetEntity, Arrhythmia, TimeSpan.FromSeconds(3));
        system.PainShock.AddPainPulse(args.TargetEntity, potency * 2f);
        var damage = new DamageSpecifier();
        damage.DamageDict[AsphyxiationType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical
            .DamageOrgan<HeartComponent>(args.TargetEntity, potency * 4f, ShockType);
}
