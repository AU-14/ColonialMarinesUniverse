using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared.EntityEffects;
using Content.Shared.StatusEffectNew;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.EntityEffects;

[UsedImplicitly]
public sealed partial class CMUBoneRegenBoostEffect : EntityEffect
{
    [DataField]
    public float Multiplier = 1.5f;

    [DataField]
    public float DurationSeconds = 15f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagent)
            return;
        var entMan = args.EntityManager;
        var status = entMan.System<SharedStatusEffectsSystem>();

        if (!status.TryAddStatusEffectDuration(reagent.TargetEntity,
                "StatusEffectCMUBoneRegenBoost", out var effect,
                TimeSpan.FromSeconds(DurationSeconds)))
        {
            return;
        }

        var boost = entMan.EnsureComponent<BoneRegenBoostComponent>(effect.Value);
        if (Multiplier > boost.Multiplier)
        {
            boost.Multiplier = Multiplier;
            entMan.Dirty(effect.Value, boost);
        }
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-bone-regen-boost-guidebook", ("multiplier", Multiplier));
}
