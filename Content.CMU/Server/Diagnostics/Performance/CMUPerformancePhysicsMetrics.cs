using Prometheus;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Reads existing controller timers between ticks; count and sum are not atomic.</summary>
internal sealed class CMUPerformancePhysicsMetrics
{
    private readonly List<(string Controller, string Phase, Histogram.Child Child)> _children = new();
    private readonly long[] _counts;
    private readonly double[] _sums;
    private TimeSpan _lastDrain;

    public CMUPerformancePhysicsMetrics(
        IEnumerable<(string Name, Histogram.Child Before, Histogram.Child After)> controllers, TimeSpan now)
    {
        foreach (var (name, before, after) in controllers)
        {
            _children.Add((name, "before", before));
            _children.Add((name, "after", after));
        }
        _counts = new long[_children.Count];
        _sums = new double[_children.Count];
        Reset(now);
    }

    public void Reset(TimeSpan now)
    {
        for (var i = 0; i < _children.Count; i++)
        {
            _counts[i] = _children[i].Child.Count;
            _sums[i] = _children[i].Child.Sum;
        }
        _lastDrain = now;
    }

    public CMUPerformancePhysicsWindow Drain(TimeSpan now)
    {
        var rows = new CMUPerformancePhysicsStage[_children.Count];
        for (var i = 0; i < _children.Count; i++)
        {
            var (name, phase, child) = _children[i];
            var count = child.Count;
            var sum = child.Sum;
            var calls = count - _counts[i];
            var seconds = sum - _sums[i];
            var valid = now >= _lastDrain && calls >= 0 && seconds >= 0 && double.IsFinite(seconds);
            var status = !valid ? "counter-reset" : calls == 0 ? "no-observations" : "observed";
            rows[i] = new(name, phase, valid ? calls : 0, valid && calls > 0 ? seconds * 1000 : null, status);
            _counts[i] = count;
            _sums[i] = sum;
        }
        var window = new CMUPerformancePhysicsWindow(Math.Max(0, (now - _lastDrain).TotalSeconds), rows);
        _lastDrain = now;
        return window;
    }
}

internal readonly record struct CMUPerformancePhysicsStage(
    string Controller, string Phase, long Calls, double? TotalMs, string Status)
{
    public double? AverageMs => Calls > 0 ? TotalMs / Calls : null;
}

internal readonly record struct CMUPerformancePhysicsWindow(
    double Seconds, IReadOnlyList<CMUPerformancePhysicsStage> Stages);
