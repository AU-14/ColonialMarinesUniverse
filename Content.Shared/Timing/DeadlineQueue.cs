namespace Content.Shared.Timing;

/// <summary>
/// An indexed deadline heap. Updating or cancelling an item does not leave stale entries
/// or allocate another heap node. Equal deadlines retain insertion order.
/// </summary>
public sealed class DeadlineQueue<T> where T : notnull // CMU14
{
    private (T Item, TimeSpan Due, ulong Order)[] _heap = [];
    private readonly Dictionary<T, int> _indices = new();
    private ulong _order;

    public int Count { get; private set; }

    public void Schedule(T item, TimeSpan due)
    {
        if (_indices.TryGetValue(item, out var index))
        {
            var previous = _heap[index];
            var replacement = (item, due, previous.Order);
            if (due < previous.Due)
                SiftUp(index, replacement);
            else if (due > previous.Due)
                SiftDown(index, replacement);
            else
                _heap[index] = replacement;
            return;
        }

        var entry = (item, due, _order++);
        if (Count == _heap.Length)
            Array.Resize(ref _heap, Count == 0 ? 4 : checked(Count * 2));
        var insertionIndex = Count++;
        SiftUp(insertionIndex, entry);
    }

    public bool Remove(T item)
    {
        if (!_indices.Remove(item, out var index))
            return false;

        var last = --Count;
        var replacement = _heap[last];
        _heap[last] = default;
        if (index < last)
        {
            if (index > 0 && Less(replacement, _heap[(index - 1) / 2]))
                SiftUp(index, replacement);
            else
                SiftDown(index, replacement);
        }
        return true;
    }

    public bool TryTakeDue(TimeSpan now, out T item)
    {
        if (Count == 0 || _heap[0].Due > now)
        {
            item = default!;
            return false;
        }

        item = _heap[0].Item;
        Remove(item);
        return true;
    }

    public void Clear()
    {
        Array.Clear(_heap, 0, Count);
        Count = 0;
        _indices.Clear();
        _order = 0;
    }

    private static bool Less(in (T Item, TimeSpan Due, ulong Order) a, in (T Item, TimeSpan Due, ulong Order) b)
        => a.Due < b.Due || a.Due == b.Due && a.Order < b.Order;

    private void Place(int index, (T Item, TimeSpan Due, ulong Order) entry)
    {
        _heap[index] = entry;
        _indices[entry.Item] = index;
    }

    // Shift each displaced entry once and write the moving item's index at its final position.
    private void SiftUp(int index, (T Item, TimeSpan Due, ulong Order) entry)
    {
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            var parentEntry = _heap[parent];
            if (!Less(entry, parentEntry))
                break;

            Place(index, parentEntry);
            index = parent;
        }
        Place(index, entry);
    }

    private void SiftDown(int index, (T Item, TimeSpan Due, ulong Order) entry)
    {
        var firstLeaf = Count / 2;
        while (index < firstLeaf)
        {
            var child = index * 2 + 1;
            if (child + 1 < Count && Less(_heap[child + 1], _heap[child]))
                child++;

            var childEntry = _heap[child];
            if (!Less(childEntry, entry))
                break;

            Place(index, childEntry);
            index = child;
        }
        Place(index, entry);
    }
}
