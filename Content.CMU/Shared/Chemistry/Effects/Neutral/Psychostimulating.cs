/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Brain;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Psychostimulating : RMCChemicalEffect
{
    private static readonly EntProtoId Drowsiness = "StatusEffectDrowsiness";
    private static readonly EntProtoId SeeingRainbows = "StatusEffectSeeingRainbow";
    private static readonly ProtoId<DamageTypePrototype> ShockType = "Shock";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Removes [color=green]{PotencyPerSecond * 2}[/color] seconds of drowsiness and hallucination per tick.\n" +
           $"Overdoses cause [color=red]{PotencyPerSecond * 2}[/color] seconds of jitters and raise pain sensitivity by 20%.\n" +
           $"Critical overdoses deal [color=red]{PotencyPerSecond * 2}[/color] brain damage.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var reduction = TimeSpan.FromSeconds((float)potency * 2f);
        system.StatusEffects.TryAddTime(args.TargetEntity, Drowsiness, -reduction);
        system.StatusEffects.TryAddTime(args.TargetEntity, SeeingRainbows, -reduction);
    }

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        system.StatusEffectQuery.TryAddStatusEffect(args.TargetEntity, "Jitter", TimeSpan.FromSeconds((float)potency * 2f), true);
        system.ChemicalPropertyStatus.ApplyPainSensitivity(args.TargetEntity, 1.2f, args.Reagent.ID);
    }

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalMedical.DamageOrgan<CMUBrainComponent>(args.TargetEntity, potency * 2f, ShockType);
}
