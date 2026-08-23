/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Musclestimulating : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Increases movement by [color=green]{MathF.Min(30f, ActualPotency * 5f)}%[/color] and melee strength by " +
           $"[color=green]{MathF.Min(75f, ActualPotency * 15f)}%[/color], permits fireman carries, and removes pulling slowdown. " +
           $"Consumes [color=yellow]{PotencyPerSecond}[/color] nutrition per second.\n" +
           $"Overdoses cause arrhythmia and [color=red]{PotencyPerSecond}[/color] heart damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] additional heart damage and " +
           $"[color=red]{PotencyPerSecond}[/color] brute limb damage, risking cardiac arrest.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<ChemicalPropertyStatusSystem>()
            .ApplyMuscleStimulation(args.TargetEntity, ActualPotency, args.Reagent!.ID);
        args.EntityManager.System<HungerSystem>().ModifyHunger(args.TargetEntity, -(float)potency);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<HeartComponent>(args.TargetEntity, potency, BluntType);
        args.EntityManager.System<SharedStatusEffectsSystem>()
            .TrySetStatusEffectDuration(args.TargetEntity, Arrhythmia, TimeSpan.FromSeconds(3));
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<HeartComponent>(args.TargetEntity, potency * 4f, BluntType);
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
