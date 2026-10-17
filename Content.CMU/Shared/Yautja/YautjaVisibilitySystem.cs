using Content.Shared.Eye;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaVisibilitySystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, GetVisMaskEvent>(OnGetVisMask);
    }

    private void OnGetVisMask(Entity<YautjaComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Yautja;
    }
}
