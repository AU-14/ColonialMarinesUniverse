/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Positive;

public sealed partial class Hemostatic : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> BluntType = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private static readonly ProtoId<DamageTypePrototype> PoisonType = "Poison";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => "Stops all current surface bleeding and suppresses current internal bleeding without treating the wounds.\n" +
           $"Overdoses cause [color=red]{PotencyPerSecond * 2}[/color] clotting damage.\n" +
           $"Critical overdoses cause [color=red]{PotencyPerSecond * 4}[/color] heart damage and " +
           $"[color=red]{PotencyPerSecond * 9}[/color] each of brute, burn, and toxin damage.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var index = args.EntityManager.System<CMUMedicalBodyIndexSystem>();
        var wounds = args.EntityManager.System<SharedCMUWoundsSystem>();
        foreach (var (part, _) in index.GetBodyParts(args.TargetEntity))
        {
            wounds.StopSurfaceBleedingOnPart(part);
            wounds.SuppressInternalBleed(part);
        }
    }

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency * 2f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<CMUChemicalMedicalSystem>()
            .DamageOrgan<HeartComponent>(args.TargetEntity, potency * 4f, BluntType);
        var damage = new DamageSpecifier();
        damage.DamageDict[BluntType] = potency * 9f;
        damage.DamageDict[HeatType] = potency * 9f;
        damage.DamageDict[PoisonType] = potency * 9f;
        damageable.TryChangeDamage(args.TargetEntity, damage, true, interruptsDoAfters: false);
    }
}
