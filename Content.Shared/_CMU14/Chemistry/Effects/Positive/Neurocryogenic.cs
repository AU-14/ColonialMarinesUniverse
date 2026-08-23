/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Events;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Medical.Unrevivable;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Neurocryogenic : RMCChemicalEffect
{
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";
    private static readonly ProtoId<DamageTypePrototype> ColdType = "Cold";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";

    protected override bool ProcessOnDead => true;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Freezes neurological activity, preventing new brain damage while maintaining unconsciousness. " +
           $"In a corpse, extends the revival window by [color=green]{PotencyPerSecond * 5}[/color] seconds per metabolism tick.\n" +
           $"Overdoses cause [color=red]{PotencyPerSecond}[/color] cold damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] direct brain damage.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (args.EntityManager.TryGetComponent<MobStateComponent>(args.TargetEntity, out var mobState) &&
            mobState.CurrentState == MobState.Dead)
        {
            args.EntityManager.System<RMCUnrevivableSystem>()
                .AddRevivableTime(args.TargetEntity, TimeSpan.FromSeconds((float)potency * 5f));
            return;
        }

        args.EntityManager.System<ChemicalPropertyStatusSystem>().ApplyNeurocryogenic(args.TargetEntity);
        args.EntityManager.System<SharedStatusEffectsSystem>()
            .TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[ColdType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 4f, ShockType, OrganDamageSource.Direct);
}
