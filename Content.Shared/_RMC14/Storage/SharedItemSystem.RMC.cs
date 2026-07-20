using Content.Shared._RMC14.Item;
using Content.Shared.Storage;

namespace Content.Shared.Item;

public abstract partial class SharedItemSystem
{
    [Dependency] private EntityQuery<FixedItemSizeStorageComponent> _fixedItemSizeStorageQuery;

    /// <summary>
    /// Gets an item's effective shape for an RMC fixed-size storage.
    /// </summary>
    public IReadOnlyList<Box2i> GetItemShape(
        Entity<StorageComponent?> storage,
        Entity<ItemComponent?> item)
    {
        if (!Resolve(item, ref item.Comp))
            return [];

        if (_fixedItemSizeStorageQuery.TryComp(storage, out var fixedSize))
        {
            fixedSize.CachedSize ??=
                [Box2i.FromDimensions(Vector2i.Zero, fixedSize.Size - Vector2i.One)];
            return fixedSize.CachedSize;
        }

        return GetItemShape(item.Comp);
    }
}
