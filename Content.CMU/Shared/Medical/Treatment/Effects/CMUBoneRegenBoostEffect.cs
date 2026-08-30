using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.EntityEffects;
using Content.Shared.StatusEffectNew;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

[UsedImplicitly]
public sealed partial class CMUBoneRegenBoostEffect : EntityEffectBase<CMUBoneRegenBoostEffect>
{
    [DataField]
    public float Multiplier = 1.5f;

    [DataField]
    public float DurationSeconds = 15f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-medical-bone-regen-boost-guidebook", ("multiplier", Multiplier));
}

public sealed partial class CMUBoneRegenBoostEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, CMUBoneRegenBoostEffect>
{
    [Dependency] private readonly StatusEffectsSystem _status = default!;

    protected override void Effect(
        Entity<MetaDataComponent> entity,
        ref EntityEffectEvent<CMUBoneRegenBoostEffect> args)
    {
        if (args.ReagentContext == null ||
            !_status.TryAddStatusEffectDuration(
                entity,
                "StatusEffectCMUBoneRegenBoost",
                out var status,
                TimeSpan.FromSeconds(args.Effect.DurationSeconds)))
        {
            return;
        }

        var boost = EnsureComp<BoneRegenBoostComponent>(status.Value);
        if (args.Effect.Multiplier <= boost.Multiplier)
            return;

        boost.Multiplier = args.Effect.Multiplier;
        Dirty(status.Value, boost);
    }
}
