/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Antiparasitic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private const float CureThreshold = 5f;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Slows parasite incubation by up to [color=green]100%[/color] and adds [color=green]{PotencyPerSecond}[/color] treatment progress. " +
           $"At {CureThreshold} progress, it destroys and expels an infection at any stage. Fighting the parasite causes " +
           $"[color=red]{PotencyPerSecond * 0.5f}[/color] burn damage per second.\n" +
           $"Overdoses cause [color=red]{PotencyPerSecond}[/color] toxin damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] additional toxin damage.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (!args.EntityManager.HasComponent<VictimInfectedComponent>(args.TargetEntity))
            return;

        var status = args.EntityManager.System<ChemicalPropertyStatusSystem>();
        var treatment = status.ApplyAntiparasitic(args.TargetEntity,
            ActualPotency,
            (float)potency,
            args.Reagent!.ID);
        var parasites = args.EntityManager.System<SharedXenoParasiteSystem>();
        parasites.RefreshIncubationMultipliers(args.TargetEntity);
        ApplyDamage(damageable, HeatType, potency * 0.5f, args);
        if (treatment.TreatmentProgress >= CureThreshold)
            parasites.TryChemicallyExpelInfection(args.TargetEntity);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => ApplyDamage(damageable, PoisonType, potency, args);

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => ApplyDamage(damageable, PoisonType, potency * 4f, args);

    private static void ApplyDamage(DamageableSystem damageable, ProtoId<DamageTypePrototype> type,
        FixedPoint2 amount, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
