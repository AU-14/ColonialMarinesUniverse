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

    /// <summary>
    /// Gets an item's effective shape for an RMC fixed-size storage, adjusted for rotation and position.
    /// </summary>
    public IReadOnlyList<Box2i> GetAdjustedItemShape(
        Entity<StorageComponent?> storage,
        Entity<ItemComponent?> item,
        ItemStorageLocation location)
    {
        return GetAdjustedItemShape(storage, item, location.Rotation, location.Position);
    }

    /// <summary>
    /// Gets an item's effective shape for an RMC fixed-size storage, adjusted for rotation and position.
    /// </summary>
    public IReadOnlyList<Box2i> GetAdjustedItemShape(
        Entity<StorageComponent?> storage,
        Entity<ItemComponent?> item,
        Angle rotation,
        Vector2i position)
    {
        var adjustedShapes = new List<Box2i>();
        GetAdjustedItemShape(adjustedShapes, storage, item, rotation, position);
        return adjustedShapes;
    }

    /// <summary>
    /// Fills a reusable list with an item's effective shape for an RMC fixed-size storage.
    /// </summary>
    public void GetAdjustedItemShape(
        List<Box2i> adjustedShapes,
        Entity<StorageComponent?> storage,
        Entity<ItemComponent?> item,
        Angle rotation,
        Vector2i position)
    {
        if (!_fixedItemSizeStorageQuery.HasComp(storage))
        {
            GetAdjustedItemShape(adjustedShapes, item, rotation, position);
            return;
        }

        var shapes = GetItemShape(storage, item);
        if (shapes.Count == 0)
            return;

        var boundingShape = shapes.GetBoundingBox();
        var boundingCenter = ((Box2) boundingShape).Center;
        var transform = Matrix3Helpers.CreateTransform(boundingCenter, rotation);
        var drift = boundingShape.BottomLeft - transform.TransformBox(boundingShape).BottomLeft;

        foreach (var shape in shapes)
        {
            var transformed = transform.TransformBox(shape).Translated(drift);
            var floored = new Box2i(transformed.BottomLeft.Floored(), transformed.TopRight.Floored());
            adjustedShapes.Add(floored.Translated(position));
        }
    }
}
