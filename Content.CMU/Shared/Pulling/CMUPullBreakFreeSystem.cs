using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Pulling.Events;

namespace Content.Shared.CMU14.Pulling;

public sealed partial class CMUPullBreakFreeSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PullableComponent, AttemptStopPullingEvent>(OnAttemptStopPulling);
    }

    private void OnAttemptStopPulling(Entity<PullableComponent> ent, ref AttemptStopPullingEvent args)
    {
        // walking off a pull already needs CanMove, but the being-pulled alert skipped it,
        // so a downed marine could click it and slip out of a xeno's drag
        if (args.Cancelled || args.User != ent.Owner || _blocker.CanMove(ent))
            return;

        args.Cancelled = true;
    }
}
