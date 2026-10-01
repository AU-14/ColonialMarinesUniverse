namespace Content.Shared.Timing;

/// <summary>
/// An indexed deadline heap. Updating or cancelling an item does not leave stale entries
/// or allocate another heap node. Equal deadlines retain insertion order.
/// </summary>
public sealed class DeadlineQueue<T> where T : notnull
{
    private readonly List<(T Item, TimeSpan Due, ulong Order)> _heap = new();
    private readonly Dictionary<T, int> _indices = new();
    private ulong _order;

    public int Count => _heap.Count;

    public void Schedule(T item, TimeSpan due)
    {
        if (_indices.TryGetValue(item, out var index))
        {
            var entry = _heap[index];
            _heap[index] = (item, due, entry.Order);
            if (due < entry.Due) Up(index);
            else if (due > entry.Due) Down(index);
            return;
        }

        _indices.Add(item, _heap.Count);
        _heap.Add((item, due, _order++));
        Up(_heap.Count - 1);
    }

    public bool Remove(T item)
    {
        if (!_indices.Remove(item, out var index)) return false;
        var last = _heap.Count - 1;
        if (index == last)
        {
            _heap.RemoveAt(last);
            return true;
        }

        _heap[index] = _heap[last];
        _heap.RemoveAt(last);
        _indices[_heap[index].Item] = index;
        if (index > 0 && Less(index, (index - 1) / 2)) Up(index);
        else Down(index);
        return true;
    }

    public bool TryTakeDue(TimeSpan now, out T item)
    {
        if (_heap.Count == 0 || _heap[0].Due > now)
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
        _heap.Clear();
        _indices.Clear();
        _order = 0;
    }

    private bool Less(int left, int right)
    {
        var a = _heap[left];
        var b = _heap[right];
        return a.Due < b.Due || a.Due == b.Due && a.Order < b.Order;
    }

    private void Swap(int a, int b)
    {
        (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
        _indices[_heap[a].Item] = a;
        _indices[_heap[b].Item] = b;
    }

    private void Up(int index)
    {
        while (index > 0)
        {
            var parent = (index - 1) / 2;
            if (!Less(index, parent)) break;
            Swap(index, parent);
            index = parent;
        }
    }

    private void Down(int index)
    {
        while (index * 2 + 1 < _heap.Count)
        {
            var child = index * 2 + 1;
            if (child + 1 < _heap.Count && Less(child + 1, child)) child++;
            if (!Less(child, index)) break;
            Swap(child, index);
            index = child;
        }
    }
}
