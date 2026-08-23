/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Oculopeutic : OrganPeuticEffect<EyesComponent>
{
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    protected override string OrganName => "eye";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Blunt";
    protected override string PlantEffect => "Mutates plant potency.";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => base.ReagentEffectGuidebookText(prototype, entSys) +
           $" Critical overdoses additionally cause [color=red]{PotencyPerSecond}[/color] brute, burn, toxin, and brain damage.";

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency,
        EntityEffectReagentArgs args)
    {
        base.TickCriticalOverdose(damageable, potency, args);
        var damage = new DamageSpecifier();
        damage.DamageDict[OrganDamageType] = potency;
        damage.DamageDict[HeatType] = potency;
        damage.DamageDict[PoisonType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);
    }

    protected override void TickHydroTray(DamageableSystem damageable, FixedPoint2 potency, EntityEffectHydroArgs args)
    {
        var ev = new HydroTickEvent<Oculopeutic>(potency, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ev);
    }
}
