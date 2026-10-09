using Content.Shared._RMC14.Fireman;
using Content.Shared.Pulling.Events;
using Content.Shared.Popups;

namespace Content.Shared.CMU14.Fireman;

public sealed partial class CMUAggressiveGrabSystem : EntitySystem
{
    [Dependency] private FiremanCarrySystem _fireman = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FiremanCarriableComponent, AttemptStopPullingEvent>(OnAttemptStopPulling);
    }

    private void OnAttemptStopPulling(Entity<FiremanCarriableComponent> ent, ref AttemptStopPullingEvent args)
    {
        // the pulled alert called TryStopPull as the victim with nothing in the way, so an agrab broke on one click.
        // moving is already blocked and routed into the struggle do-after, the alert has to go the same way
        if (args.Cancelled || args.User != ent.Owner || !_fireman.IsAggressivelyGrabbed(ent))
            return;

        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("cmu-pull-aggressive-struggle-hint"), ent, ent, PopupType.SmallCaution);
    }
}
