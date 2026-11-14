using Content.Shared.DragDrop;
using Content.Shared.Mobs.Systems;

namespace Content.Shared.CMU14.Yautja.BadBlood;

// the client only lets a drag through that the target answers for, so this half has to run on both sides.
// the server decides whether the corpse is really cooked
public sealed partial class BadBloodCampCookSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<BadBloodCampCookComponent, CanDropTargetEvent>(OnCanDropTarget);
    }

    private void OnCanDropTarget(Entity<BadBloodCampCookComponent> ent, ref CanDropTargetEvent args)
    {
        if (args.Handled ||
            args.Dragged == args.User ||
            !HasComp<YautjaBadBloodComponent>(args.User) ||
            !_mobState.IsDead(args.Dragged))
        {
            return;
        }

        args.CanDrop = true;
        args.Handled = true;
    }
}
