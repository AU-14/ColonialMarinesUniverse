/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Damage;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Repairing : RMCChemicalEffect
{
    private static readonly ProtoId<DamageGroupPrototype> BruteGroup = "Brute";
    private static readonly ProtoId<DamageGroupPrototype> BurnGroup = "Burn";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Heals [color=green]{PotencyPerSecond * 2}[/color] brute and burn damage on synthetics. " +
           "Repairs inorganic structures on contact.\n" +
           $"Overdoses cause [color=red]{PotencyPerSecond}[/color] toxin damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 5}[/color] additional toxin damage.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        if (!args.EntityManager.HasComponent<SynthComponent>(args.TargetEntity))
            return;

        var rmcDamage = args.EntityManager.System<SharedRMCDamageableSystem>();
        var heal = rmcDamage.DistributeHealingCached(args.TargetEntity, BruteGroup, potency * 2f);
        heal = rmcDamage.DistributeHealingCached(args.TargetEntity, BurnGroup, potency * 2f, heal);
        damageable.TryChangeDamage(args.TargetEntity, heal, true, interruptsDoAfters: false);
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => ApplyToxin(damageable, potency, args);

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => ApplyToxin(damageable, potency * 5f, args);

    private static void ApplyToxin(DamageableSystem damageable, FixedPoint2 amount, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[PoisonType] = amount;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
