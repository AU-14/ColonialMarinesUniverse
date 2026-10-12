using Content.Shared.Rejuvenate;
using Content.Shared.Traits.Assorted;

namespace Content.Server.CMU14.Medical.Treatment.Recovery;

public sealed partial class CMUUnrevivableRejuvenateSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<UnrevivableComponent, RejuvenateEvent>(OnRejuvenate);
    }

    // perma used to survive an aheal, so the next death skipped the defib timer and went straight to perma.
    // RMCUnrevivableSystem.MakeUnrevivable always clears Analyzable; the DNR trait keeps it, so that one stays
    private void OnRejuvenate(Entity<UnrevivableComponent> ent, ref RejuvenateEvent args)
    {
        if (ent.Comp.Analyzable)
            return;

        RemComp<UnrevivableComponent>(ent);
    }
}
