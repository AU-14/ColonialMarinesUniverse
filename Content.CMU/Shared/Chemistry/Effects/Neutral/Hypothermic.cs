/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Hypothermic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> ColdType = "Cold";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Lowers body temperature by [color=yellow]{PotencyPerSecond * 2}[/color] K per tick, down to [color=yellow]{LinearLevel * 5}[/color] K below normal.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond}[/color] cold damage.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 3}[/color] cold damage and cause hypothermic unconsciousness.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var sys = system.Temperature;
        var current = sys.GetTemperature(args.TargetEntity);
        var floor = TemperatureHelpers.CelsiusToKelvin(Atmospherics.NormalBodyTemperature) - args.LinearLevel * 5f;
        if (current <= floor)
            return;
        sys.ForceChangeTemperature(args.TargetEntity, Math.Max(floor, current - potency.Float() * 2f));
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[ColdType] = potency;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[ColdType] = potency * 3f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
        system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }
}
