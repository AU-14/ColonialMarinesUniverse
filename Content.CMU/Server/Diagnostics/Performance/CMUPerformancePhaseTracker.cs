using Robust.Shared.ContentPack;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Measures existing module boundaries without changing or instrumenting the engine.</summary>
internal sealed class CMUPerformancePhaseTracker
{
    private readonly Dictionary<string, Phase> _phases = new();
    private Phase _frameWorst;
    public Phase LastFrameWorst { get; private set; }
    private string? _previous;
    private TimeSpan _started;
    private uint _tick;

    public void Mark(ModUpdateLevel level, TimeSpan now, uint tick)
    {
        string? next = level switch
        {
            ModUpdateLevel.InputPostEngine => "before-tick",
            ModUpdateLevel.PreEngine => "timers-tasks-and-simulation",
            ModUpdateLevel.PostEngine => "post-tick-and-state-send",
            ModUpdateLevel.FramePreEngine => "engine-frame-work",
            ModUpdateLevel.FramePostEngine => "content-frame-callbacks",
            _ => null,
        };
        if (next == null)
            return;

        Mark(next, now, tick);
        if (level == ModUpdateLevel.InputPostEngine)
        {
            LastFrameWorst = _frameWorst;
            _frameWorst = default;
        }
    }

    public void EndFrameCallbacks(TimeSpan now, uint tick) => Mark("frame-tail-wait-and-input", now, tick);

    private void Mark(string next, TimeSpan now, uint tick)
    {

        if (_previous != null && now >= _started)
        {
            double ms = (now - _started).TotalMilliseconds;
            _phases.TryGetValue(_previous, out var row);
            _phases[_previous] = new Phase(_previous, row.Count + 1, row.TotalMs + ms,
                Math.Max(row.MaxMs, ms), ms >= row.MaxMs ? _tick : row.WorstTick);
            if (ms >= _frameWorst.MaxMs)
                _frameWorst = new Phase(_previous, 1, ms, ms, _tick);
        }
        _previous = next;
        _started = now;
        _tick = tick;
    }

    public List<Phase> Drain()
    {
        var result = new List<Phase>(_phases.Values);
        _phases.Clear();
        return result;
    }

    public void Clear()
    {
        _phases.Clear();
        _previous = null;
        _frameWorst = default;
        LastFrameWorst = default;
    }

    internal readonly record struct Phase(string Name, long Count, double TotalMs, double MaxMs, uint WorstTick);
}
