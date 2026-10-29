using Robust.Shared.Map;
using Robust.Shared.Map.Enumerators;

namespace Content.Shared.Storage.EntitySystems;

public abstract partial class SharedStorageSystem
{
    /// <summary>
    /// A short-lived placement snapshot for checking multiple saved positions.
    /// Rebuild for each UI update so moves and storage-specific shapes stay live.
    /// </summary>
    public CMUPlacementSnapshot CMUGetPlacementSnapshot(Entity<StorageComponent> storage, EntityUid ignored)
    {
        return new CMUPlacementSnapshot(storage.Comp.Grid.GetBoundingBox(), BuildPlacementOccupancy(storage, ignored));
    }

    public readonly struct CMUPlacementSnapshot
    {
        private readonly Box2i _bounds;
        private readonly Dictionary<Vector2i, ulong> _occupied;

        internal CMUPlacementSnapshot(Box2i bounds, Dictionary<Vector2i, ulong> occupied)
        {
            _bounds = bounds;
            _occupied = occupied;
        }

        /// <param name="shape">The item shape already transformed to the candidate location.</param>
        /// <param name="origin">The candidate location, also checked against the grid bounds.</param>
        public bool Fits(IReadOnlyList<Box2i> shape, Vector2i origin)
        {
            return _bounds.Contains(origin) && FitsPlacementOccupancy(_occupied, shape, Vector2i.Zero);
        }
    }

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
