/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Special;

/// <summary>
/// Runs the brain far above normal speed: strong nerve stimulation and stun shortening. There is no
/// language system in this fork to grant universal comprehension, so that half of the CM13 property is
/// not modelled.
/// </summary>
public sealed partial class Hyperthrottling : RMCChemicalEffect
{
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Doubles nerve stimulation: improves movement by [color=green]{MathF.Min(30f, ActualPotency * 20f)}%[/color], shortens new stuns by [color=green]{MathF.Min(50f, ActualPotency * 30f)}%[/color], and removes [color=green]{ActualPotency * 2}[/color] seconds from existing stuns per tick.\n" +
           $"Overdoses deal [color=red]{PotencyPerSecond}[/color] brain damage.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 3}[/color] brain damage and cause unconsciousness.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalPropertyStatus.ApplyNerveStimulation(args.TargetEntity, args.ActualPotency * 2f, args.Reagent.ID);
        system.Stun.TryRemoveStunAndKnockdownTime(args.TargetEntity, TimeSpan.FromSeconds(args.ActualPotency * 2f * args.Scale));
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 3f, ShockType);
        system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }
}
