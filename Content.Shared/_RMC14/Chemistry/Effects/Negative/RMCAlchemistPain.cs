using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._RMC14.Synth;
using Content.Shared.Damage;
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

    protected override void Tick(DamageableSystem damageable, FixedPoint2 potency, EntityEffectReagentArgs args)
    {
        var entMan = args.EntityManager;
        var target = args.TargetEntity;

        if (entMan.HasComponent<SynthComponent>(target) ||
            !entMan.TryGetComponent<PainShockComponent>(target, out var pain))
        {
            return;
        }

        pain.Pain = FixedPoint2.Min(pain.PainMax, pain.Pain + potency);
        pain.NextUpdate = TimeSpan.Zero;
        entMan.Dirty(target, pain);
        entMan.System<SharedPainShockSystem>().RefreshTier(target);
    }
}
