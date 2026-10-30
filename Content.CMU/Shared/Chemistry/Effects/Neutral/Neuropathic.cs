/// THIS FILE IS LICENSED UNDER THE MIT LICENSE ///
/// reason: Because I, (MACMAN2003), the initial coder of this specific file disagree with the AGPL's copyleft approach to
/// free software and would prefer this code be shared freely without restrictions.
using Content.Shared._RMC14.Chemistry.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared.CMU14.Chemistry.Effects.Neutral;

public sealed partial class Neuropathic : RMCChemicalEffect
{
    private static readonly EntProtoId Unconscious = "StatusEffectCMUUnconscious";

    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => $"Adds [color=red]{PotencyPerSecond * 2}[/color] perceptive pain without tissue damage.\n" +
           $"Overdoses raise pain sensitivity by [color=red]{MathF.Min(50f, ActualPotency * 10f)}%[/color].\n" +
           $"Critical overdoses have a [color=red]10%[/color] chance to cause unconsciousness from pain.";

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.PainShock.AddPainPulse(args.TargetEntity, potency * 2f);

    protected override void TickOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
        => system.ChemicalPropertyStatus.ApplyPainSensitivity(args.TargetEntity, 1f + MathF.Min(0.5f, args.ActualPotency * 0.1f), args.Reagent.ID);

    protected override void TickCriticalOverdose(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        if (system.Random.Prob(0.1f))
            system.StatusEffects.TrySetStatusEffectDuration(args.TargetEntity, Unconscious, TimeSpan.FromSeconds(3));
    }
}
