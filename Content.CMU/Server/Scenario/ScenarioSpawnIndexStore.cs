namespace Content.Server.AU14.Scenario;

/// <summary>
/// Inverted index for semantic spawn-marker tags.
/// </summary>
internal sealed class ScenarioSpawnIndexStore
{
    private const string AllMarkersBucket = "\0all-markers";

    private readonly Dictionary<string, HashSet<EntityUid>> _buckets =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<EntityUid, HashSet<string>> _tagsByMarker = new();

    public void AddMarker(EntityUid uid)
    {
        AddTag(uid, AllMarkersBucket);
    }

    public void AddTag(EntityUid uid, string tag)
    {
        if (!_buckets.TryGetValue(tag, out var entities))
        {
            entities = new HashSet<EntityUid>();
            _buckets.Add(tag, entities);
        }

        entities.Add(uid);
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
        HashSet<EntityUid>? smallest = null;
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

        destination.EnsureCapacity(smallest.Count);
        foreach (var uid in smallest)
        {
            destination.Add(uid);
        }

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
}
