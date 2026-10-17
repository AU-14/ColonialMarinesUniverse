using Content.Shared._RMC14.Inventory;
using Content.Shared.Item;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    /// <summary>Defers compaction while container membership and occupied cells still update immediately.</summary>
    public CMUStorageRepackScope DeferRepacking(Entity<StorageComponent> storage)
    {
        storage.Comp.CMURepackDepth++;
        return new CMUStorageRepackScope(this, storage);
    }

    public readonly struct CMUStorageRepackScope(SharedStorageSystem system, Entity<StorageComponent> storage) : IDisposable
    {
        public void Dispose()
        {
            if (--storage.Comp.CMURepackDepth == 0 && storage.Comp.CMURepackPending &&
                !storage.Comp.Deleted && !system.TerminatingOrDeleted(storage))
            {
                storage.Comp.CMURepackPending = false;
                system.RepackStorage(storage);
            }
        }
    }

    private void RepackStorage(Entity<StorageComponent> storage)
    {
        // Recursive deletion removes each child in turn. Rearranging the doomed siblings
        // is quadratic work and cannot produce a placement that any player can use.
        if (storage.Comp.Deleted || TerminatingOrDeleted(storage))
            return;

        if (storage.Comp.CMURepackDepth > 0)
        {
            storage.Comp.CMURepackPending = true;
            return;
        }

        var items = new List<(EntityUid Id, ItemStorageLocation Location)>();
        foreach (var (item, location) in storage.Comp.StoredItems)
            items.Add((item, location));

        items.Sort(static (a, b) =>
        {
            var y = a.Location.Position.Y.CompareTo(b.Location.Position.Y);
            return y != 0 ? y : a.Location.Position.X.CompareTo(b.Location.Position.X);
        });

        foreach (var (item, location) in items)
        {
            if (CMInventoryExtensions.TryGetFirst(storage, item, out var newLocation) && location != newLocation)
                TrySetItemStorageLocation(item, (storage.Owner, storage.Comp), newLocation);
        }
    }

    /// <summary>Finds the original unrotated, row-first compaction position using one occupancy snapshot.</summary>
    public bool TryGetFirstUnrotatedStorageLocation(Entity<StorageComponent?> storage, Entity<ItemComponent?> item,
        out ItemStorageLocation location)
    {
        location = default;
        if (!Resolve(storage, ref storage.Comp) || !Resolve(item, ref item.Comp))
            return false;

        var bounds = storage.Comp.Grid.GetBoundingBox();
        var occupied = BuildPlacementOccupancy((storage.Owner, storage.Comp), item.Owner);
        var shape = ItemSystem.GetAdjustedItemShape(storage, item, Angle.Zero, Vector2i.Zero);
        for (var y = bounds.Bottom; y <= bounds.Top; y++)
        for (var x = bounds.Left; x <= bounds.Right; x++)
        {
            if (!FitsPlacementOccupancy(occupied, shape, (x, y)))
                continue;
            location = new ItemStorageLocation(Angle.Zero, (x, y));
            return true;
        }

        return false;
    }
}
