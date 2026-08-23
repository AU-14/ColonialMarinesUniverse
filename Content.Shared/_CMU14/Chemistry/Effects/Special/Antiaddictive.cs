/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Special;

public sealed partial class Antiaddictive : RMCChemicalEffect
{
    private const float CureThreshold = 1f;
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    private static readonly EntProtoId Nausea = "StatusEffectCMUNausea";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Adds [color=green]{PotencyPerSecond}[/color] addiction-treatment progress per second and reduces new generated-chemical " +
           $"addiction chance by [color=green]{MathF.Min(100f, ActualPotency * 25f)}%[/color] while active. At {CureThreshold} progress, " +
           "it permanently clears nicotine and all generated-chemical addictions.\n" +
           $"Overdoses cause nausea, [color=red]{PotencyPerSecond}[/color] toxin damage, and " +
           $"[color=red]{PotencyPerSecond}[/color] brain damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] neurological shock damage.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var treatment = args.EntityManager.System<ChemicalPropertyStatusSystem>()
            .ApplyAddictionTreatment(args.TargetEntity,
                ActualPotency,
                (float)potency,
                args.Reagent!.ID);
        if (treatment.Progress < CureThreshold)
            return;

        var ev = new CureChemicalAddictionEvent();
        args.EntityManager.EventBus.RaiseLocalEvent(args.TargetEntity, ref ev);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        ApplyDamage(damageable, PoisonType, potency, args);
        args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);
        args.EntityManager.System<SharedStatusEffectsSystem>()
            .TrySetStatusEffectDuration(args.TargetEntity, Nausea, TimeSpan.FromSeconds(3));
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => ApplyDamage(damageable, ShockType, potency * 4f, args);

    private static void ApplyDamage(DamageableSystem system, ProtoId<DamageTypePrototype> type, FixedPoint2 amount,
        EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        system.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
