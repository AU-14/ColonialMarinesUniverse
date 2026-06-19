using Content.Shared._CMU14.Medical.TemporaryBlurryVision;
using Content.Shared.EntityEffects;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.EntityEffects;

[UsedImplicitly]
public sealed partial class TemporaryBlurryVision : EntityEffect
{
    [DataField]
    public float Blur = 2f;

    [DataField]
    public float Time = 4f;

    public override void Effect(EntityEffectBaseArgs args)
    {
        var scale = (args as EntityEffectReagentArgs)?.Scale.Float() ?? 1f;
        args.EntityManager.System<CMUTemporaryBlurryVisionSystem>()
            .AddTemporaryBlurModifier(args.TargetEntity, TimeSpan.FromSeconds(Time * scale), Blur);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => null;
}
