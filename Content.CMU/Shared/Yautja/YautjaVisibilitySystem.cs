using Content.Shared._RMC14.IdentityManagement;
using Content.Shared.Eye;
using Content.Shared.Ghost.Components;

namespace Content.Shared.CMU14.Yautja;

public sealed partial class YautjaVisibilitySystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<YautjaComponent, GetVisMaskEvent>(OnGetVisMask);
        SubscribeLocalEvent<GhostComponent, RMCGetFixedIdentityEvent>(OnGhostGetFixedIdentity);
    }

    private void OnGetVisMask(Entity<YautjaComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int) VisibilityFlags.Yautja;
    }

    // the "unknown" fixed identity matches every viewer, ghosts included. dropping it here
    // falls through to the normal identity path, where ghosts see the real name
    private void OnGhostGetFixedIdentity(Entity<GhostComponent> ent, ref RMCGetFixedIdentityEvent args)
    {
        if (HasComp<YautjaComponent>(args.Identified))
            args.Cancelled = true;
    }
}
