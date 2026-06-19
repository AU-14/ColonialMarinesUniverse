using Content.Shared.Body.Components;
using Content.Shared.DragDrop;

namespace Content.Shared._CMU14.Medical.Surgery;

public sealed class SharedCMUMedicalPodSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUAutodocPodComponent, CanDropTargetEvent>(OnCanDropOnAutodocPod);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, CanDropTargetEvent>(OnCanDropOnBodyScannerPod);
    }

    private void OnCanDropOnAutodocPod(Entity<CMUAutodocPodComponent> ent, ref CanDropTargetEvent args)
    {
        SetCanDropPatient(ref args);
    }

    private void OnCanDropOnBodyScannerPod(Entity<CMUBodyScannerPodComponent> ent, ref CanDropTargetEvent args)
    {
        SetCanDropPatient(ref args);
    }

    private void SetCanDropPatient(ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop |= HasComp<BodyComponent>(args.Dragged);
    }
}
