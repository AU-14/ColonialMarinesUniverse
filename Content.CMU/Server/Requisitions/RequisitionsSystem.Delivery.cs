using Content.Server.Storage.Components;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Robust.Shared.Map;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server._RMC14.Requisitions;

public sealed partial class RequisitionsSystem
{
    // Shared across elevators in one update. The count cap also bounds very cheap spawns.
    private int _deliverySteps;
    private long _deliveryStarted;

    private bool PrepareDelivery(Entity<RequisitionsElevatorComponent> elevator)
    {
        using var profile = _profiler.Group("CMU Requisitions Prepare");
        using var operation = _performance.MeasureOperation("requisitions-prepare");
        TryGiveRoundStartFreeCrate(elevator);
        var delivery = EnsureComp<RequisitionsDeliveryComponent>(elevator);
        while (delivery.Remaining.Count > 0 || delivery.Roots.Count < elevator.Comp.Orders.Count)
        {
            if (_deliverySteps >= 4 ||
                _deliverySteps > 0 && Stopwatch.GetElapsedTime(_deliveryStarted).TotalMilliseconds >= 2)
                return false;
            if (_deliverySteps++ == 0)
                _deliveryStarted = Stopwatch.GetTimestamp();

            if (delivery.Remaining.TryDequeue(out var item))
            {
                var uid = Spawn(item, MapCoordinates.Nullspace);
                if (!_entityStorage.Insert(uid, delivery.CurrentRoot))
                {
                    // Retain unexpected overflow and deliver it rather than leaking it in nullspace.
                    // Crate capacity is validated by the catalog; a failure is a content error.
                    _transform.SetParent(uid, delivery.CurrentRoot);
                    Log.Error($"Shipment item {item} did not fit in {ToPrettyString(delivery.CurrentRoot)}.");
                }
                continue;
            }

            var order = elevator.Comp.Orders[delivery.Roots.Count];
            var root = EntityManager.CreateEntityUninitialized(order.Crate, MapCoordinates.Nullspace);
            delivery.Roots.Add(root); // Own the entity before initialization, including exceptional cleanup.
            delivery.CurrentRoot = root;
            if (HasComp<EntityStorageComponent>(root) && !HasComp<StorageComponent>(root))
                AddComp<DeferredEntityStorageFillComponent>(root);
            EntityManager.InitializeAndStartEntity(root, true);
            if (TryComp<DeferredEntityStorageFillComponent>(root, out var fill))
            {
                foreach (var prototype in fill.Prototypes)
                    delivery.Remaining.Enqueue(prototype);
                RemComp<DeferredEntityStorageFillComponent>(root);
            }
            foreach (var prototype in order.Entities)
                delivery.Remaining.Enqueue(prototype);
        }
        return true;
    }

    private void OnDeliveryShutdown(Entity<RequisitionsDeliveryComponent> ent, ref ComponentShutdown args)
    {
        foreach (var root in ent.Comp.Roots)
        {
            if (!TerminatingOrDeleted(root))
                QueueDel(root);
        }
    }
}
