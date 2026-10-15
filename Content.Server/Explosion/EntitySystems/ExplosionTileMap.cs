using System.Collections;
using System.Collections.Immutable;

namespace Content.Server.Explosion.EntitySystems;

/// <summary>
/// Tile data with constant-time snapshots. A write after publication copies at most one
/// 16x16 chunk, rather than copying a whole grid or restarting a running explosion.
/// Values must themselves be immutable after insertion.
/// </summary>
public sealed class ExplosionTileMap<T>
{
    private ImmutableDictionary<Vector2i, Chunk> _chunks = ImmutableDictionary<Vector2i, Chunk>.Empty;
    private ulong _generation;
    public int Count { get; private set; }
    internal int CopiedChunks { get; private set; }

    public T this[Vector2i tile]
    {
        get => TryGetValue(tile, out var value) ? value : throw new KeyNotFoundException();
        set
        {
            var chunk = WritableChunk(tile);
            if (chunk.Tiles.TryAdd(tile, value)) Count++;
            else chunk.Tiles[tile] = value;
        }
    }

    public void Add(Vector2i tile, T value)
    {
        WritableChunk(tile).Tiles.Add(tile, value);
        Count++;
    }

    public bool TryGetValue(Vector2i tile, out T value)
    {
        if (_chunks.TryGetValue(ChunkIndex(tile), out var chunk))
            return chunk.Tiles.TryGetValue(tile, out value!);
        value = default!;
        return false;
    }

    public T? GetValueOrDefault(Vector2i tile) => TryGetValue(tile, out var value) ? value : default;
    public bool Remove(Vector2i tile) => Remove(tile, out _);

    public bool Remove(Vector2i tile, out T value)
    {
        if (!TryGetValue(tile, out value!)) return false;
        var chunk = WritableChunk(tile);
        chunk.Tiles.Remove(tile);
        Count--;
        if (chunk.Tiles.Count == 0) _chunks = _chunks.Remove(ChunkIndex(tile));
        return true;
    }

    public Snapshot Capture()
    {
        var snapshot = new Snapshot(_chunks, Count);
        _generation++;
        return snapshot;
    }

    private Chunk WritableChunk(Vector2i tile)
    {
        var index = ChunkIndex(tile);
        if (!_chunks.TryGetValue(index, out var chunk))
        {
            chunk = new Chunk(_generation, new());
            _chunks = _chunks.Add(index, chunk);
        }
        else if (chunk.Generation != _generation)
        {
            chunk = new Chunk(_generation, new(chunk.Tiles));
            _chunks = _chunks.SetItem(index, chunk);
            CopiedChunks++;
        }
        return chunk;
    }

    private static Vector2i ChunkIndex(Vector2i tile) => new(tile.X >> 4, tile.Y >> 4);

    internal sealed record Chunk(ulong Generation, Dictionary<Vector2i, T> Tiles);

    public sealed class Snapshot : IReadOnlyDictionary<Vector2i, T>
    {
        public static readonly Snapshot Empty = new(ImmutableDictionary<Vector2i, Chunk>.Empty, 0);
        private readonly ImmutableDictionary<Vector2i, Chunk> _chunks;
        public int Count { get; }

        internal Snapshot(ImmutableDictionary<Vector2i, Chunk> chunks, int count)
        {
            _chunks = chunks;
            Count = count;
        }

        public T this[Vector2i tile] => TryGetValue(tile, out var value) ? value : throw new KeyNotFoundException();
        public bool ContainsKey(Vector2i tile) => TryGetValue(tile, out _);
        public bool TryGetValue(Vector2i tile, out T value)
        {
            if (_chunks.TryGetValue(ChunkIndex(tile), out var chunk))
                return chunk.Tiles.TryGetValue(tile, out value!);
            value = default!;
            return false;
        }

        public IEnumerable<Vector2i> Keys { get { foreach (var entry in this) yield return entry.Key; } }
        public IEnumerable<T> Values { get { foreach (var entry in this) yield return entry.Value; } }
        public IEnumerator<KeyValuePair<Vector2i, T>> GetEnumerator()
        {
            foreach (var chunk in _chunks.Values)
            foreach (var entry in chunk.Tiles)
                yield return entry;
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
