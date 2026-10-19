using Content.Shared.DragDrop;
using Content.Shared.Whitelist;

namespace Content.Shared.CMU14.DropPod;

public sealed partial class CMUSharedDropPodSystem : EntitySystem
{
    [Dependency] private EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUDropPodComponent, CanDropTargetEvent>(OnCanDropTarget);
    }

    private void OnCanDropTarget(Entity<CMUDropPodComponent> ent, ref CanDropTargetEvent args)
    {
        if (!CanAccept(ent, args.Dragged))
            return;

        args.Handled = true;
        args.CanDrop = true;
    }

    public bool CanAccept(Entity<CMUDropPodComponent> pod, EntityUid target)
    {
        return pod.Comp.State == CMUDropPodState.Ready
            && !pod.Comp.Occupied
            && _whitelist.CheckBoth(target, pod.Comp.OccupantBlacklist, pod.Comp.OccupantWhitelist);
    }
}
