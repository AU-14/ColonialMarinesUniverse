using Content.Shared.Eye.Blinding.Systems;

namespace Content.Shared._CMU14.Medical.Organs.Eyes;

public sealed class CMUBlurDelaySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUBlurDelayComponent, GetBlurEvent>(OnGetBlur);
    }

    private void OnGetBlur(Entity<CMUBlurDelayComponent> ent, ref GetBlurEvent args)
    {
        args.Blur -= ent.Comp.Threshold;
    }
}
