/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Medical.Anatomy.Organs.Kidneys;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Nephropeutic : OrganPeuticEffect<KidneysComponent>
{
    protected override string OrganName => "kidney";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Poison";
    protected override string PlantEffect => "Mutates light, weed, or toxin tolerance in plants.";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => base.ReagentEffectGuidebookText(prototype, entSys) +
           $" Critical overdoses additionally cause [color=red]{PotencyPerSecond * 2.5f}[/color] systemic toxin damage.";

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
        base.TickCriticalOverdose(system, damageable, potency, args);
        var damage = new DamageSpecifier();
        damage.DamageDict[OrganDamageType] = potency * 2.5f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickHydroTray(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.RaiseHydroTick<Nephropeutic>(args.TargetEntity, potency, args.Context.Quantity);
    }
}
