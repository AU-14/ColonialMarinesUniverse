namespace Content.Server.AU14.Scenario;

/// <summary>
/// Inverted index for semantic spawn-marker tags.
/// </summary>
internal sealed class RoundWorldSpawnMarkerStore
{
    private const string AllMarkersBucket = "\0all-markers";

    private readonly Dictionary<string, MarkerBucket> _buckets =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityUid, HashSet<string>> _tagsByMarker = new();

    public void AddMarker(EntityUid uid)
    {
        AddTag(uid, AllMarkersBucket);
    }

    public void AddTag(EntityUid uid, string tag)
    {
        if (!_buckets.TryGetValue(tag, out var bucket))
        {
            bucket = new MarkerBucket();
            _buckets.Add(tag, bucket);
        }

        bucket.Add(uid);
        if (!_tagsByMarker.TryGetValue(uid, out var tags))
        {
            tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _tagsByMarker.Add(uid, tags);
        }

        tags.Add(tag);
    }

    public bool TryCopyCandidates(
        IReadOnlyList<string> requiredTags,
        List<EntityUid> destination)
    {
        destination.Clear();
        MarkerBucket? smallest = null;
        if (requiredTags.Count == 0)
        {
            _buckets.TryGetValue(AllMarkersBucket, out smallest);
        }
        else
        {
            foreach (var tag in requiredTags)
            {
                if (!_buckets.TryGetValue(tag, out var entities))
                    return false;

                if (smallest == null || entities.Count < smallest.Count)
                    smallest = entities;
            }
        }

        if (smallest == null)
            return false;

        smallest.CopyTo(destination);

        return true;
    }

    public bool RemoveMarker(EntityUid uid)
    {
        if (!_tagsByMarker.Remove(uid, out var tags))
            return false;

        foreach (var tag in tags)
        {
            if (!_buckets.TryGetValue(tag, out var entities))
                continue;

            entities.Remove(uid);
            if (entities.Count == 0)
                _buckets.Remove(tag);
        }

        return true;
    }

    public void Clear()
    {
        _buckets.Clear();
        _tagsByMarker.Clear();
    }

    private sealed class MarkerBucket
    {
        private readonly List<EntityUid> _entities = [];
        private readonly Dictionary<EntityUid, int> _positions = [];
        private int _tombstones;

        public int Count => _positions.Count;

        public void Add(EntityUid uid)
        {
            if (_positions.ContainsKey(uid))
                return;

            _positions.Add(uid, _entities.Count);
            _entities.Add(uid);
        }

        public void Remove(EntityUid uid)
        {
            if (!_positions.Remove(uid, out var index))
                return;

            _entities[index] = EntityUid.Invalid;
            _tombstones++;
            if (_tombstones >= 32 && _tombstones > _positions.Count)
                Compact();
        }

        public void CopyTo(List<EntityUid> destination)
        {
            destination.EnsureCapacity(destination.Count + Count);
            foreach (var uid in _entities)
            {
                if (uid != EntityUid.Invalid)
                    destination.Add(uid);
            }
        }

        private void Compact()
        {
            var writeIndex = 0;
            for (var readIndex = 0; readIndex < _entities.Count; readIndex++)
            {
                var uid = _entities[readIndex];
                if (uid == EntityUid.Invalid)
                    continue;

                _entities[writeIndex] = uid;
                _positions[uid] = writeIndex;
                writeIndex++;
            }

            if (writeIndex < _entities.Count)
                _entities.RemoveRange(writeIndex, _entities.Count - writeIndex);

            _tombstones = 0;
        }
    }
}
