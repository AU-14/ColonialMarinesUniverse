using Content.Shared.DragDrop;

namespace Content.Shared.CMU14.Hygiene;

public sealed partial class CMULaundryTransferSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<CMULaundryHamperComponent, CanDragEvent>(OnCanDrag);
        SubscribeLocalEvent<CMULaundryHamperComponent, CanDropDraggedEvent>(OnCanDrop);
        SubscribeLocalEvent<CMULaundryTransferTargetComponent, CanDropTargetEvent>(OnCanDropTarget);
    }

    private void OnCanDrag(Entity<CMULaundryHamperComponent> ent, ref CanDragEvent args)
    {
        // The client requires explicit permission before starting a sprite drag.
        args.Handled = true;
    }

    private void OnCanDropTarget(Entity<CMULaundryTransferTargetComponent> ent, ref CanDropTargetEvent args)
    {
        if (!HasComp<CMULaundryHamperComponent>(args.Dragged))
            return;
        args.Handled = true;
        args.CanDrop = true;
    }

    private void OnCanDrop(Entity<CMULaundryHamperComponent> ent, ref CanDropDraggedEvent args)
    {
        if (!HasComp<CMULaundryTransferTargetComponent>(args.Target))
            return;
        args.Handled = true;
        args.CanDrop = true;
    }
}
