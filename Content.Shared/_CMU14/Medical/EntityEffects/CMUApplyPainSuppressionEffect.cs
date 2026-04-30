using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared.EntityEffects;
using Content.Shared.StatusEffectNew;
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
    public float SuppressionPercent = 0.5f;

    [DataField]
    public float DurationPerUnit = 60f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagent)
            return;
        var entMan = args.EntityManager;
        var status = entMan.System<SharedStatusEffectsSystem>();

        var duration = TimeSpan.FromSeconds(DurationPerUnit * (float)reagent.Quantity);
        if (!status.TryAddStatusEffectDuration(reagent.TargetEntity,
                "StatusEffectCMUPainSuppression", out var effect, duration))
        {
            return;
        }

        var sup = entMan.EnsureComponent<PainSuppressionComponent>(effect.Value);
        if (SuppressionPercent > sup.Percent)
        {
            sup.Percent = SuppressionPercent;
            entMan.Dirty(effect.Value, sup);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-pain-suppression-guidebook",
            ("percent", (int)(SuppressionPercent * 100f)),
            ("seconds", DurationPerUnit));
}
