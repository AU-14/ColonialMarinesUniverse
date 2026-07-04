using Content.Shared.Eye.Blinding.Systems;

namespace Content.Shared._CMU14.Medical.Anatomy.Organs.Eyes;

public sealed partial class CMUBlurDelaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBlurDelayComponent, GetBlurEvent>(OnGetBlur);
    }

    private void OnGetBlur(Entity<CMUBlurDelayComponent> ent, ref GetBlurEvent args)
    {
        var baseBlur = MathF.Min(args.Blur, args.BaseBlur);
        var extraBlur = args.Blur - baseBlur;
        args.Blur = MathF.Max(0f, baseBlur - ent.Comp.Threshold) + extraBlur;
    }
}
