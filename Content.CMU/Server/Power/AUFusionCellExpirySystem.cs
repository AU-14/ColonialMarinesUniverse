using Content.Shared._RMC14.Power;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.AU14.Power;

public sealed partial class AUFusionCellExpirySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AUFusionCellExpiryComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<AUFusionCellExpiryComponent, EntGotRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(EntityUid uid, AUFusionCellExpiryComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (!HasComp<RMCFusionReactorComponent>(args.Container.Owner))
            return;

        comp.InsertedAt = _timing.CurTime;
    }

    private void OnRemoved(EntityUid uid, AUFusionCellExpiryComponent comp, EntGotRemovedFromContainerMessage args)
    {
        comp.InsertedAt = null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<AUFusionCellExpiryComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.InsertedAt is not { } insertedAt)
                continue;

            if (now - insertedAt < comp.Lifetime)
                continue;

            // Expired — eject from reactor and delete
            if (_container.TryGetContainingContainer(uid, out var container))
            {
                if (TryComp<RMCFusionReactorComponent>(container.Owner, out _))
                {
                    _popup.PopupEntity("The fusion cell sputters and dies — it has expired.", container.Owner, PopupType.MediumCaution);
                    _container.Remove(uid, container, destination: Transform(container.Owner).Coordinates);
                }
            }

            QueueDel(uid);
        }
    }
}
