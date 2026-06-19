using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.EntityEffects;

/// <summary>
///     Stacking painkillers takes the strongest, not a sum.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUApplyPainSuppressionEffect : EntityEffect
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

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagent)
            return;
        var duration = TimeSpan.FromSeconds(DurationPerUnit * (float)reagent.Quantity);
        var pain = args.EntityManager.System<SharedPainShockSystem>();
        if (Additive)
        {
            pain.AddAdditivePainSuppressionProfile(
                reagent.TargetEntity,
                AccumulationSuppression,
                TierSuppression,
                DecayBonus,
                duration);
        }
        else
        {
            pain.AddPainSuppressionProfile(
                reagent.TargetEntity,
                AccumulationSuppression,
                TierSuppression,
                DecayBonus,
                duration,
                ReductionDecreaseRate);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-pain-suppression-guidebook",
            ("percent", (int)(AccumulationSuppression * 100f)),
            ("tiers", TierSuppression),
            ("decay", DecayBonus),
            ("decrease", ReductionDecreaseRate),
            ("seconds", DurationPerUnit));
}
