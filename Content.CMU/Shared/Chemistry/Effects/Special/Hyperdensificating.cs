/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared._RMC14.Movement;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

public sealed partial class Hyperdensificating : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Reduces trauma-driven bone integrity loss by [color=green]{MathF.Min(95f, LinearLevel * 75f)}%[/color].\n" +
           $"Overdoses cause rigidity, [color=red]{PotencyPerSecond}[/color] pain, and a 15% movement slowdown.\n" +
           $"Critical overdoses add [color=red]{PotencyPerSecond * 4}[/color] pain and strip bone integrity.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalPropertyStatus
            .ApplyHyperdensity(args.TargetEntity,
                MathF.Min(0.95f, args.LinearLevel * 0.75f),
                args.Reagent!.ID);

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var modifiers = new List<TemporarySpeedModifierSet>(1);
        modifiers.Add(new(TimeSpan.FromSeconds(2), 0.85f, 0.85f));
        system.TemporarySpeedModifiers.ModifySpeed(args.TargetEntity, modifiers);
        system.PainShock.AddPainPulse(args.TargetEntity, potency);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.PainShock.AddPainPulse(args.TargetEntity, potency * 4f);
        system.Bone
            .DamageWeakestBone(args.TargetEntity, potency * 4f, fracture: true);
    }
}
