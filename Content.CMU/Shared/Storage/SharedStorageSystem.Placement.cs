using Robust.Shared.Map;
using Robust.Shared.Map.Enumerators;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    /// <summary>
    /// Builds a snapshot for one placement operation. Reusing the component's occupied
    /// mask would incorrectly ignore overlapping items when moving an existing item,
    /// and can miss changes to storage-specific item shapes.
    /// </summary>
    private Dictionary<Vector2i, ulong> BuildPlacementOccupancy(Entity<StorageComponent> storage, EntityUid ignored)
    {
        var occupied = new Dictionary<Vector2i, ulong>();
        RemoveOccupied(storage.Comp.Grid, occupied);

        foreach (var (uid, location) in storage.Comp.StoredItems)
        {
            if (uid == ignored || !_itemQuery.TryGetComponent(uid, out var item))
                continue;

            var shape = ItemSystem.GetAdjustedItemShape((storage.Owner, storage.Comp), (uid, item), location);
            // Chunks outside the grid must remain absent (and therefore unavailable).
            AddOccupied(shape, occupied, onlyExistingChunks: true);
        }

        return occupied;
    }

    private static bool FitsPlacementOccupancy(
        Dictionary<Vector2i, ulong> occupied,
        IReadOnlyList<Box2i> shape,
        Vector2i position)
    {
        for (var i = 0; i < shape.Count; i++)
        {
            var box = shape[i].Translated(position);
            var chunks = new ChunkIndicesEnumerator(box, StorageComponent.ChunkSize);
            while (chunks.MoveNext(out var chunk))
            {
                var origin = chunk.Value * StorageComponent.ChunkSize;
                if (!occupied.TryGetValue(origin, out var mask))
                    return false;

                var left = Math.Max(origin.X, box.Left);
                var bottom = Math.Max(origin.Y, box.Bottom);
                var right = Math.Min(origin.X + StorageComponent.ChunkSize - 1, box.Right);
                var top = Math.Min(origin.Y + StorageComponent.ChunkSize - 1, box.Top);

                for (var x = left; x <= right; x++)
                {
                    for (var y = bottom; y <= top; y++)
                    {
                        var relative = SharedMapSystem.GetChunkRelative(new Vector2i(x, y), StorageComponent.ChunkSize);
                        var flag = SharedMapSystem.ToBitmask(relative, StorageComponent.ChunkSize);
                        if ((mask & flag) != 0)
                            return false;
                    }
                }
            }
        }

        return true;
    }
}
