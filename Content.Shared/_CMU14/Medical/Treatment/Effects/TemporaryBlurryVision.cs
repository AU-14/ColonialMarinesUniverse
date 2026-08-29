using Content.Shared._CMU14.Medical.Injuries.Vision;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Treatment.Effects;

[UsedImplicitly]
public sealed partial class TemporaryBlurryVision : EntityEffectBase<TemporaryBlurryVision>
{
    [DataField]
    public float Blur = 2f;

    [DataField]
    public float Time = 4f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}

public sealed partial class TemporaryBlurryVisionEntityEffectSystem
    : EntityEffectSystem<MetaDataComponent, TemporaryBlurryVision>
{
    [Dependency] private readonly CMUTemporaryBlurryVisionSystem _blurryVision = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<TemporaryBlurryVision> args)
    {
        _blurryVision.AddTemporaryBlurModifier(
            entity,
            TimeSpan.FromSeconds(args.Effect.Time * args.Scale),
            args.Effect.Blur);
    }
}
