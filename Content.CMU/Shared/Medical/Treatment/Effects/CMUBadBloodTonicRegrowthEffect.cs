using Content.Shared.CMU14.Yautja.BadBlood;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.CMU14.Medical.Treatment.Effects;

/// <summary>
/// CMU14 ECS-711: the Bad Blood regeneration tonic. While it metabolises, limbs the body loses regrow after
/// <see cref="Multiplier"/> of their usual delay (<see cref="BadBloodTonicRegrowthComponent"/>, read by
/// BadBloodSurvivalSystem.GetLimbRegrowDelayMultiplier). Each metabolism tick keeps it up for
/// <see cref="DurationSeconds"/>.
/// </summary>
[UsedImplicitly]
public sealed partial class CMUBadBloodTonicRegrowthEffect : EntityEffectBase<CMUBadBloodTonicRegrowthEffect>
{
    [DataField]
    public float Multiplier = 0.5f;

    [DataField]
    public float DurationSeconds = 5f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("cmu-bad-blood-tonic-regrowth-guidebook", ("multiplier", Multiplier));
}

public sealed partial class CMUBadBloodTonicRegrowthEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, CMUBadBloodTonicRegrowthEffect>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(
        Entity<MetaDataComponent> entity,
        ref EntityEffectEvent<CMUBadBloodTonicRegrowthEffect> args)
    {
        if (args.ReagentContext == null)
            return;

        var tonic = EnsureComp<BadBloodTonicRegrowthComponent>(entity);
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.DurationSeconds);
        if (until > tonic.ExpiresAt)
            tonic.ExpiresAt = until;

        tonic.Multiplier = args.Effect.Multiplier;
    }
}
