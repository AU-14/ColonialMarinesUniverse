using System.Numerics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

/// <summary>
///     Variable-height scrollback. Unvisited rows use estimated heights; measuring a new window
///     preserves its first visible message (or the bottom) as those estimates become exact.
/// </summary>
internal sealed class ChatLogList : Control
{
    private const float Overscan = 120;
    private readonly List<ChatLogEntry> _entries = new();
    private readonly List<ChatLogEntry> _materialized = new();
    private readonly float[] _offsets = new float[ChatLogPanel.MaxEntries + 1];
    private bool _offsetsDirty;
    private float _scroll;
    private float _page;
    private bool _following = true;
    private int _visit;

    public int EntryCount => _entries.Count;
    public float AnchoredScroll { get; private set; }

    public float Add(ChatLogEntry entry)
    {
        var removedHeight = 0f;
        if (_entries.Count == ChatLogPanel.MaxEntries)
        {
            var removed = _entries[0];
            removedHeight = removed.Height;
            Release(removed);
            _entries.RemoveAt(0);
        }

        _entries.Add(entry);
        _offsetsDirty = true;
        InvalidateMeasure();
        return removedHeight;
    }

    public void Clear()
    {
        for (var i = _materialized.Count - 1; i >= 0; i--)
            Release(_materialized[i]);
        _entries.Clear();
        _offsetsDirty = true;
        InvalidateMeasure();
    }

    private void Release(ChatLogEntry entry)
    {
        if (entry.Row == null)
            return;
        RemoveChild(entry.Row);
        entry.Row.Dispose();
        entry.Row = null;
        _materialized.Remove(entry);
    }

    public void SetViewport(float scroll, float page, bool following)
    {
        page = float.IsFinite(page) ? MathF.Max(0, page) : _page;
        if (_scroll == scroll && _page == page && _following == following)
            return;
        // Following the bottom selects the same rows regardless of a stale scrollbar maximum.
        var changed = _page != page || _following != following || !following && _scroll != scroll;
        _scroll = scroll;
        _page = page;
        _following = following;
        if (changed)
            InvalidateMeasure();
    }

    private void UpdateOffsets()
    {
        if (!_offsetsDirty)
            return;
        _offsets[0] = 0;
        for (var i = 0; i < _entries.Count; i++)
        {
            _entries[i].Offset = _offsets[i];
            _offsets[i + 1] = _offsets[i] + _entries[i].Height;
        }
        _offsetsDirty = false;
    }

    private int FindEntry(float offset)
    {
        var low = 0;
        var high = _entries.Count - 1;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (_offsets[mid + 1] <= offset)
                low = mid + 1;
            else
                high = mid;
        }
        return low;
    }

    private float MeasureEntry(int index, float width)
    {
        var entry = _entries[index];
        if (entry.Row == null)
        {
            entry.Row = entry.CreateRow();
            _materialized.Add(entry);
            AddChild(entry.Row);
        }
        entry.Visit = _visit;
        entry.Row.Measure(new Vector2(width, float.PositiveInfinity));
        var height = MathF.Max(1, entry.Row.DesiredSize.Y);
        if (entry.Height != height)
        {
            entry.Height = height;
            _offsetsDirty = true;
        }
        return height;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        UpdateOffsets();
        _visit++;
        var anchor = _entries.Count == 0 ? 0 : FindEntry(_scroll);
        var within = MathF.Max(0, _scroll - _offsets[anchor]);
        if (_following)
        {
            var height = 0f;
            for (var i = _entries.Count - 1; i >= 0 && height < _page + Overscan; i--)
                height += MeasureEntry(i, availableSize.X);
        }
        else
        {
            var before = 0f;
            for (var i = anchor - 1; i >= 0 && before < Overscan; i--)
                before += MeasureEntry(i, availableSize.X);
            var after = 0f;
            for (var i = anchor; i < _entries.Count && after < within + _page + Overscan; i++)
                after += MeasureEntry(i, availableSize.X);
        }

        for (var i = _materialized.Count - 1; i >= 0; i--)
        {
            if (_materialized[i].Visit != _visit)
                Release(_materialized[i]);
        }

        UpdateOffsets();
        var total = _offsets[_entries.Count];
        AnchoredScroll = _following
            ? MathF.Max(0, total - _page)
            : Math.Clamp(_offsets[anchor] + within, 0, MathF.Max(0, total - _page));
        return new Vector2(0, total);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var bottomPadding = MathF.Max(0, finalSize.Y - _offsets[_entries.Count]);
        foreach (var entry in _materialized)
        {
            entry.Row!.Arrange(UIBox2.FromDimensions(
                new Vector2(0, bottomPadding + entry.Offset), new Vector2(finalSize.X, entry.Height)));
        }
        return finalSize;
    }
}
