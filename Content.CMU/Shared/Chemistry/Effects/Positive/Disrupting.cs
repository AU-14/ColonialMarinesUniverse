/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Positive;

/// <summary>
/// Disrupts neurological communication: shuts off xenonid pheromones and garbles speech in everything else.
/// </summary>
public sealed partial class Disrupting : RMCChemicalEffect
{
    private static readonly EntProtoId Muted = "StatusEffectMuted";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Deactivates xenonid pheromones and causes [color=yellow]{PotencyPerSecond * 2}[/color] seconds of stuttering per tick.\n" +
           $"Overdoses mute the subject.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond}[/color] brain damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.DeactivatePheromones(args.TargetEntity);
        system.StatusEffects.TryAddStatusEffectDuration(args.TargetEntity, StutteringSystem.StutterEffect, TimeSpan.FromSeconds((float)potency * 2f));
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Muted, TimeSpan.FromSeconds(3));

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency, ShockType);
}
