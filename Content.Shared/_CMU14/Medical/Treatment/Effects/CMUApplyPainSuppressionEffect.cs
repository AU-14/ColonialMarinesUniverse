using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Treatment.Effects;

/// <summary>
///     Stacking painkillers takes the strongest, not a sum.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUApplyPainSuppressionEffect : EntityEffectBase<CMUApplyPainSuppressionEffect>
{
    [DataField]
    public float AccumulationSuppression = 0.5f;

    [DataField]
    public int TierSuppression = 2;

    [DataField]
    public float DecayBonus = 0.75f;

    [DataField]
    public float ReductionDecreaseRate = 0.25f;

    [DataField]
    public bool Additive;

    [DataField]
    public float DurationPerUnit = 60f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-pain-suppression-guidebook",
            ("percent", (int)(AccumulationSuppression * 100f)),
            ("tiers", TierSuppression),
            ("decay", DecayBonus),
            ("decrease", ReductionDecreaseRate),
            ("seconds", DurationPerUnit));
}

public sealed partial class CMUApplyPainSuppressionEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, CMUApplyPainSuppressionEffect>
{
    [Dependency] private readonly SharedPainShockSystem _pain = default!;

    protected override void Effect(
        Entity<MetaDataComponent> entity,
        ref EntityEffectEvent<CMUApplyPainSuppressionEffect> args)
    {
        if (args.ReagentContext is not { } context)
            return;

        var effect = args.Effect;
        var duration = TimeSpan.FromSeconds(effect.DurationPerUnit * (float) context.Quantity.Quantity);
        if (effect.Additive)
        {
            _pain.AddAdditivePainSuppressionProfile(
                entity,
                effect.AccumulationSuppression,
                effect.TierSuppression,
                effect.DecayBonus,
                duration);
        }
        else
        {
            _pain.AddPainSuppressionProfile(
                entity,
                effect.AccumulationSuppression,
                effect.TierSuppression,
                effect.DecayBonus,
                duration,
                effect.ReductionDecreaseRate);
        }
    }
}
