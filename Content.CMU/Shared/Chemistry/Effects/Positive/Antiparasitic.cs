/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

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

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (!system.HasVictimInfected(args.TargetEntity))
            return;

        var status = system.ChemicalPropertyStatus;
        var treatment = status.ApplyAntiparasitic(args.TargetEntity,
            args.ActualPotency,
            (float)potency,
            args.Reagent!.ID);
        var parasites = system.Parasite;
        parasites.RefreshIncubationMultipliers(args.TargetEntity);
        ApplyDamage(damageable, HeatType, potency * 0.5f, args);
        if (treatment.TreatmentProgress >= CureThreshold)
            parasites.TryChemicallyExpelInfection(args.TargetEntity);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => ApplyDamage(damageable, PoisonType, potency, args);

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => ApplyDamage(damageable, PoisonType, potency * 4f, args);

    private static void ApplyDamage(DamageableSystem damageable, ProtoId<DamageTypePrototype> type,
        FixedPoint2 amount, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[type] = amount;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
