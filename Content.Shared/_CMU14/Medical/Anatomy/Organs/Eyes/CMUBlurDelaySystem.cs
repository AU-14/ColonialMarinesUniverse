using Content.Shared.Eye.Blinding.Components;
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

        // Permanent floor blur (e.g. the Nearsighted trait's MinDamage) shouldn't be delayed away by the
        // injury threshold below - only damage accrued above that floor is subject to the delay.
        var floor = 0f;
        if (TryComp(ent.Owner, out BlindableComponent? blindable))
            floor = MathF.Min(blindable.MinDamage, baseBlur);

        var injury = MathF.Max(0f, baseBlur - floor);
        args.Blur = floor + MathF.Max(0f, injury - ent.Comp.Threshold) + extraBlur;
    }
}
