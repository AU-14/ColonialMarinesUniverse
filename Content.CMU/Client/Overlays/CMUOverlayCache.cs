namespace Content.Client.CMU14.Overlays;

/// <summary>
/// Keeps recently drawn entities without clearing every visible entity when a new one enters view.
/// Evicted entries reuse their list node, so a full cache does not allocate bookkeeping on replacement.
/// Values retain their caller-owned expiry and invalidation rules.
/// </summary>
internal sealed class CMUOverlayCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _entries;
    private readonly LinkedList<(TKey Key, TValue Value)> _recency = new();

    public int Count => _entries.Count;

    public CMUOverlayCache(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
        _entries = new(capacity);
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (!_entries.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        value = node.Value.Value;
        Touch(node);
        return true;
    }

    public void Set(TKey key, TValue value)
    {
        if (_entries.TryGetValue(key, out var node))
        {
            node.Value = (key, value);
            Touch(node);
            return;
        }

        if (_entries.Count == _capacity)
        {
            node = _recency.Last!;
            _entries.Remove(node.Value.Key);
            _recency.Remove(node);
            node.Value = (key, value);
        }
        else
        {
            node = new((key, value));
        }

        _entries.Add(key, node);
        _recency.AddFirst(node);
    }

    private void Touch(LinkedListNode<(TKey Key, TValue Value)> node)
    {
        if (_recency.First == node)
            return;

        _recency.Remove(node);
        _recency.AddFirst(node);
    }
}
