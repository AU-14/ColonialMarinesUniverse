/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.StatusEffectNew;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

public sealed partial class Cardiopeutic : OrganPeuticEffect<HeartComponent>
{
    private static readonly EntProtoId Arrhythmia = "StatusEffectCMUArrhythmia";

    protected override string OrganName => "heart";
    protected override ProtoId<DamageTypePrototype> OrganDamageType => "Shock";
    protected override string PlantEffect => "Suppresses forced chemical-production mutations in plants.";
    protected override bool RestartHeart => true;

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Heals [color=green]{PotencyPerSecond * 2}[/color] heart health and restarts a viable heart. " +
           "Suppresses forced chemical-production mutations in plants.\n" +
           $"Overdoses cause arrhythmia and [color=red]{PotencyPerSecond}[/color] heart damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] additional heart damage and " +
           $"[color=red]{PotencyPerSecond * 5}[/color] chest pain.";

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        base.TickOverdose(system, damageable, potency, args);
        system.StatusEffects
            .TrySetStatusEffectDuration(args.TargetEntity, Arrhythmia, TimeSpan.FromSeconds(3));
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency,
        RMCReagentEffectArgs args)
    {
        base.TickCriticalOverdose(system, damageable, potency, args);
        system.PainShock.AddPainPulse(args.TargetEntity, potency * 5f);
    }

    protected override void TickHydroTray(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.RaiseHydroTick<Cardiopeutic>(args.TargetEntity, potency, args.Context.Quantity);
    }
}
