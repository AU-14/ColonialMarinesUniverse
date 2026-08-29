using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._RMC14.Chemistry.Effects.Negative;

public sealed partial class RMCAlchemistPain : RMCChemicalEffect
{
    protected override string ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return $"Increases pain by [color=red]{PotencyPerSecond}[/color] per second.";
    }

    protected override void Tick(RMCChemicalEffectSystem system, DamageableSystem damageable, FixedPoint2 potency, RMCReagentEffectArgs args)
    {
        var target = args.TargetEntity;

        if (system.HasSynth(target) || !system.TryGetPainShock(target, out var pain))
        {
            return;
        }

        pain.Pain = FixedPoint2.Min(pain.PainMax, pain.Pain + potency);
        pain.NextUpdate = TimeSpan.Zero;
        system.DirtyPainShock(target, pain);
        system.PainShock.RefreshTier(target);
    }
}
