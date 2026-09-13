using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.CMU14.Diagnostics.Performance;

public sealed partial class CMUServerPerformanceDiagnosticsManager
{
    private readonly Queue<SlowOperation> _operations = new();
    private TimeSpan _lastRuntimeSample;
    private TimeSpan _lastGcPause;
    private double _runtimeWindowMs;
    private double _gcPauseMs;

    public CMUPerformanceOperationScope MeasureOperation(string name, string? prototype = null) =>
        _enabled ? new(this, name, prototype, _timing.CurTick.Value) : default;

    internal void CompleteOperation(string name, string? prototype, uint tick, double milliseconds, long bytes)
    {
        if (milliseconds < 10 && bytes < 1024 * 1024)
            return;
        if (_operations.Count == 32)
            _operations.Dequeue();
        _operations.Enqueue(new(name, prototype, tick, _timing.RealTime, milliseconds, bytes));
    }

    private void ObserveRuntime(TimeSpan now)
    {
        var pause = GC.GetTotalPauseDuration();
        _runtimeWindowMs = _lastRuntimeSample == default ? 0 : (now - _lastRuntimeSample).TotalMilliseconds;
        _gcPauseMs = _lastRuntimeSample == default ? 0 : Math.Max(0, (pause - _lastGcPause).TotalMilliseconds);
        _lastRuntimeSample = now;
        _lastGcPause = pause;
    }

    private void LogOperations()
    {
        // Manual reports can reuse an older scalar observation. Age operations at emission time.
        var now = _timing.RealTime;
        while (_operations.TryDequeue(out var operation))
        {
            if (now - operation.Completed > TimeSpan.FromSeconds(2))
                continue;
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] operation incidentId={_activeIncidentId} name={SanitizeName(operation.Name)} ",
                $"prototype={SanitizeName(operation.Prototype ?? "none")} tick={operation.Tick} ",
                $"timeMs={operation.Milliseconds:F3} allocatedBytes={operation.Bytes} ",
                $"ageMs={(now - operation.Completed).TotalMilliseconds:F3} inclusive=true source=content-scope"));
        }
    }

    private readonly record struct SlowOperation(string Name, string? Prototype, uint Tick, TimeSpan Completed,
        double Milliseconds, long Bytes);
}

/// <summary>Allocation-free content timing retained independently of the engine profiler ring.</summary>
public readonly struct CMUPerformanceOperationScope : IDisposable
{
    private readonly CMUServerPerformanceDiagnosticsManager? _owner;
    private readonly string _name;
    private readonly string? _prototype;
    private readonly uint _tick;
    private readonly long _started;
    private readonly long _allocated;

    internal CMUPerformanceOperationScope(CMUServerPerformanceDiagnosticsManager owner, string name, string? prototype, uint tick)
    {
        _owner = owner;
        _name = name;
        _prototype = prototype;
        _tick = tick;
        _started = Stopwatch.GetTimestamp();
        _allocated = GC.GetAllocatedBytesForCurrentThread();
    }

    public void Dispose()
    {
        _owner?.CompleteOperation(_name, _prototype, _tick, Stopwatch.GetElapsedTime(_started).TotalMilliseconds,
            GC.GetAllocatedBytesForCurrentThread() - _allocated);
    }
}
