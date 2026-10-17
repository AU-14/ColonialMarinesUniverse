using System.Linq;
using Robust.Shared.Profiling;

namespace Content.Client.CMU14.Diagnostics.Performance;

/// <summary>
/// Reads completed engine frames on the main thread. Paths are interned once per capture;
/// the steady-state reader reuses its storage instead of allocating a tree every frame.
/// </summary>
internal sealed class CMUClientProfileReader
{
    internal const int MaxEventsPerFrame = 50000;
    internal const int MaxScopes = 4096;
    private const int MaxDepth = 128;

    private readonly Dictionary<(int Parent, int Name, bool Group), int> _keys = new();
    private readonly List<Scope> _scopes = new();
    private readonly List<int> _touched = new();
    private readonly int[] _parents = new int[MaxDepth];
    private readonly long[] _starts = new long[MaxDepth];

    public long Cursor;
    public long LostFrames;
    public int OversizedFrames;
    public int InvalidFrames;
    public int DroppedScopeEvents;
    public int ExcludedFrames;
    public int Frames;
    public double TotalWorkMs;
    public long TotalAllocatedBytes;
    public Frame? WorstWork;
    public Frame? WorstAllocation;
    public Frame? WorstWall;
    public IEnumerable<Scope> Scopes => _scopes;

    public CMUClientProfileReader(long cursor)
    {
        Cursor = cursor;
    }

    public void Read(ProfManager profiler, long excludeThroughFrame, long worstWallFrame = -1)
    {
        // This shallow copy intentionally reads the live arrays without cloning the profiler ring.
        var buffer = profiler.Buffer;
        if (!profiler.IsEnabled || buffer.IndexBuffer.Length == 0 || buffer.LogBuffer.Length == 0)
            return;

        var end = buffer.IndexWriteOffset;
        if (Cursor > end) // The user resized/reset the profiler index buffer.
            Cursor = 0;
        var oldest = Math.Max(0, end - buffer.IndexBuffer.LongLength);
        if (Cursor < oldest)
        {
            LostFrames += oldest - Cursor;
            Cursor = oldest;
        }

        // Normally exactly one completed frame is available. Bound recovery after interrupted updates.
        if (end - Cursor > 8)
        {
            LostFrames += end - Cursor - 8;
            Cursor = end - 8;
        }

        for (; Cursor < end; Cursor++)
        {
            var index = buffer.Index(Cursor);
            if (index.Type != ProfIndexType.Frame || index.StartPos < buffer.LogWriteOffset - buffer.LogBuffer.LongLength ||
                index.StartPos < 0 || index.EndPos > buffer.LogWriteOffset || index.EndPos - index.StartPos < 3)
            {
                LostFrames++;
                continue;
            }

            var first = buffer.Log(index.StartPos);
            var last = buffer.Log(index.EndPos - 1);
            if (first.Type != ProfLogType.Value || first.Value.Value.Type != ProfValueType.Int64 ||
                profiler.GetString(first.Value.StringId) != "Start Frame" ||
                last.Type != ProfLogType.GroupEnd || last.GroupEnd.Value.Type != ProfValueType.TimeAllocSample ||
                profiler.GetString(last.GroupEnd.StringId) != "Frame")
            {
                InvalidFrames++;
                continue;
            }

            var frame = first.Value.Value.Int64;
            if (frame <= excludeThroughFrame)
            {
                ExcludedFrames++;
                continue;
            }

            var timing = last.GroupEnd.Value.TimeAllocSample;
            // Retain the root timing even when a pathological frame cannot be parsed within the budget.
            Frames++;
            TotalWorkMs += timing.Time * 1000;
            TotalAllocatedBytes += timing.Alloc;
            var detailed = index.EndPos - index.StartPos <= MaxEventsPerFrame;
            if (!detailed)
                OversizedFrames++;
            else if (!ReadFrame(profiler, buffer, index))
            {
                InvalidFrames++;
                detailed = false;
            }

            var workMs = timing.Time * 1000d;
            var worstWork = WorstWork == null || workMs > WorstWork.WorkMs;
            var worstAllocation = WorstAllocation == null || timing.Alloc > WorstAllocation.AllocatedBytes;
            if (worstWork || worstAllocation || frame == worstWallFrame)
            {
                var rows = detailed
                    ? _touched.Select(id => new Row(_scopes[id].Path, _scopes[id].Current)).ToArray()
                    : Array.Empty<Row>();
                var snapshot = new Frame(frame, workMs, timing.Alloc, detailed, rows);
                if (worstWork)
                    WorstWork = snapshot;
                if (worstAllocation)
                    WorstAllocation = snapshot;
                if (frame == worstWallFrame)
                    WorstWall = snapshot;
            }

            foreach (var id in _touched)
            {
                var scope = _scopes[id];
                if (detailed)
                    scope.Window.Merge(scope.Current);
                scope.Current = default;
            }
            _touched.Clear();
        }
    }

    private bool ReadFrame(ProfManager profiler, ProfBuffer buffer, ProfIndex index)
    {
        var depth = 0;
        // Names live on GroupEnd, so walk backwards to learn parents before visiting their children.
        for (var pos = index.EndPos - 1; pos > index.StartPos; pos--)
        {
            var log = buffer.Log(pos);
            var parent = depth == 0 ? -1 : _parents[depth - 1];
            switch (log.Type)
            {
                case ProfLogType.GroupEnd:
                    if (depth == MaxDepth || log.GroupEnd.StartIndex < index.StartPos || log.GroupEnd.StartIndex >= pos)
                        return false;
                    var group = GetScope(profiler, parent, log.GroupEnd.StringId, true);
                    Add(group, log.GroupEnd.Value);
                    _parents[depth] = group;
                    _starts[depth++] = log.GroupEnd.StartIndex;
                    break;
                case ProfLogType.GroupStart:
                    if (depth == 0 || _starts[depth - 1] != pos)
                        return false;
                    depth--;
                    break;
                case ProfLogType.Value:
                    Add(GetScope(profiler, parent, log.Value.StringId, false), log.Value.Value);
                    break;
                default:
                    return false;
            }
        }
        return depth == 0;
    }

    private int GetScope(ProfManager profiler, int parent, int name, bool group)
    {
        if (parent == -2)
        {
            DroppedScopeEvents++;
            return -2;
        }
        var key = (parent, name, group);
        if (_keys.TryGetValue(key, out var id))
            return id;
        if (_scopes.Count >= MaxScopes)
        {
            DroppedScopeEvents++;
            return -2;
        }

        var text = profiler.GetString(name);
        var path = parent < 0 ? text : $"{_scopes[parent].Path} / {text}";
        id = _scopes.Count;
        _keys.Add(key, id);
        _scopes.Add(new Scope(path));
        return id;
    }

    private void Add(int id, ProfValue value)
    {
        if (id < 0 || value.Type == ProfValueType.Invalid)
            return;
        var scope = _scopes[id];
        if (scope.Current.Count == 0)
            _touched.Add(id);
        scope.Current.AddReverse(value);
    }

    public void ResetWindow()
    {
        foreach (var scope in _scopes)
            scope.Window = default;
        Frames = 0;
        TotalWorkMs = 0;
        TotalAllocatedBytes = 0;
        WorstWork = null;
        WorstAllocation = null;
        WorstWall = null;
    }

    internal sealed class Scope(string path)
    {
        public readonly string Path = path;
        public Sample Current;
        public Sample Window;
    }

    internal struct Sample
    {
        public int Count;
        public bool Timing;
        public double TotalMs;
        public double MaxMs;
        public long Bytes;
        public long MaxBytes;
        public long CounterTotal;
        public long CounterMax;
        public long CounterLast;

        public void AddReverse(ProfValue value)
        {
            if (value.Type == ProfValueType.TimeAllocSample)
            {
                Timing = true;
                var ms = value.TimeAllocSample.Time * 1000d;
                TotalMs += ms;
                MaxMs = Math.Max(MaxMs, ms);
                Bytes += value.TimeAllocSample.Alloc;
                MaxBytes = Math.Max(MaxBytes, value.TimeAllocSample.Alloc);
            }
            else
            {
                var number = value.Type == ProfValueType.Int32 ? value.Int32 : value.Int64;
                CounterTotal += number;
                CounterMax = Count == 0 ? number : Math.Max(CounterMax, number);
                if (Count == 0)
                    CounterLast = number;
            }
            Count++;
        }

        public void Merge(Sample sample)
        {
            Timing = sample.Timing;
            CounterMax = Count == 0 ? sample.CounterMax : Math.Max(CounterMax, sample.CounterMax);
            Count += sample.Count;
            TotalMs += sample.TotalMs;
            MaxMs = Math.Max(MaxMs, sample.MaxMs);
            Bytes += sample.Bytes;
            MaxBytes = Math.Max(MaxBytes, sample.MaxBytes);
            CounterTotal += sample.CounterTotal;
            CounterLast = sample.CounterLast;
        }
    }

    internal sealed record Row(string Path, Sample Sample);
    internal sealed record Frame(long Number, double WorkMs, long AllocatedBytes, bool Detailed, Row[] Rows);
}
