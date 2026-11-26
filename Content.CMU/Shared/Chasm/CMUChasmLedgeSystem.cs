using Content.Shared.ActionBlocker;
using Content.Shared.Movement.Events;

namespace Content.Shared.CMU14.Chasm;

public sealed partial class CMUChasmLedgeSystem : EntitySystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUChasmLedgeGripComponent, UpdateCanMoveEvent>((_, grip, args) =>
        {
            if (grip.LifeStage <= ComponentLifeStage.Running)
                args.Cancel();
        });
        SubscribeLocalEvent<CMUChasmLedgeGripComponent, ComponentStartup>((uid, _, _) => _blocker.UpdateCanMove(uid));
        SubscribeLocalEvent<CMUChasmLedgeGripComponent, ComponentShutdown>((uid, _, _) => _blocker.UpdateCanMove(uid));
    }
}
