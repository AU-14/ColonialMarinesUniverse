/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared._CMU14.Chemistry.Effects;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Chemistry.Effects.Special;

public sealed partial class Hyperdensificating : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Reduces trauma-driven bone integrity loss by [color=green]{MathF.Min(95f, LinearLevel * 75f)}%[/color].\n" +
           $"Overdoses cause rigidity, [color=red]{PotencyPerSecond}[/color] pain, and a 15% movement slowdown.\n" +
           $"Critical overdoses add [color=red]{PotencyPerSecond * 4}[/color] pain and strip bone integrity.";

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
        => args.EntityManager.System<ChemicalPropertyStatusSystem>()
            .ApplyHyperdensity(args.TargetEntity,
                MathF.Min(0.95f, LinearLevel * 0.75f),
                args.Reagent!.ID);

    protected override void TickOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<SharedStunSystem>()
            .TrySlowdown(args.TargetEntity, TimeSpan.FromSeconds(2), true, 0.85f, 0.85f);
        args.EntityManager.System<SharedPainShockSystem>().AddPainPulse(args.TargetEntity, potency);
    }

    protected override void TickCriticalOverdose(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        args.EntityManager.System<SharedPainShockSystem>().AddPainPulse(args.TargetEntity, potency * 4f);
        args.EntityManager.System<SharedBoneSystem>()
            .DamageWeakestBone(args.TargetEntity, potency * 4f, fracture: true);
    }
}
