/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Medical.Anatomy.Organs.Liver;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Hepatopeutic : OrganPeuticEffect<LiverComponent>
{
    protected override string OrganName => "liver";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Poison";
    protected override string PlantEffect => "Forces cancer or gluttony mutations in plants.";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => base.ReagentEffectGuidebookText(prototype, entSys) +
           $" Critical overdoses additionally cause [color=red]{PotencyPerSecond * 2.5f}[/color] systemic toxin damage.";

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency,
        EntityEffectReagentArgs args)
    {
        base.TickCriticalOverdose(damageable, potency, args);
        var damage = new DamageSpecifier();
        damage.DamageDict[OrganDamageType] = potency * 2.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickHydroTray(DamageableSystem damageable, FixedPoint2 potency, EntityEffectHydroArgs args)
    {
        var ev = new HydroTickEvent<Hepatopeutic>(potency, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ev);
    }
}
