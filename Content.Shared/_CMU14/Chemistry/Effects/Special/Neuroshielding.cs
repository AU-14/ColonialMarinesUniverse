/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Movement;
using Content.Shared._RMC14.Stun;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Special;

public sealed partial class Neuroshielding : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Reduces reagent-sourced brain damage by [color=green]{MathF.Min(95f, LinearLevel * 80f)}%[/color] and clears daze.\n" +
           $"Overdoses cause brain fog, a 10% movement slowdown, and [color=red]{PotencyPerSecond}[/color] liver damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] toxin and direct brain damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalPropertyStatus
            .ApplyNeuroshield(args.TargetEntity,
                MathF.Min(0.95f, args.LinearLevel * 0.8f),
                args.Reagent!.ID);
        system.StatusEffects
            .TryRemoveStatusEffect(args.TargetEntity, RMCDazedSystem.StatusEffectDazed);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var modifiers = new List<TemporarySpeedModifierSet>(1);
        modifiers.Add(new(TimeSpan.FromSeconds(2), 0.9f, 0.9f));
        system.TemporarySpeedModifiers.ModifySpeed(args.TargetEntity, modifiers);
        system.ChemicalMedical
            .DamageOrgan<LiverComponent>(args.TargetEntity, potency, PoisonType);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = potency * 4f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        system.ChemicalMedical
            .DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 4f, ShockType);
    }
}
