using System.Linq;

namespace Content.Server._CMU14.Diagnostics.Performance;

internal readonly record struct CMUPerformanceCounterPoint(
    double Tick,
    int EntityCount,
    long EntitiesCreated,
    long EntitiesDeleted,
    int ComponentCount,
    long ComponentsAdded,
    long ComponentsRemoved,
    long EntitiesDirtied);

internal readonly record struct CMUPerformanceRates(
    double SpanSeconds,
    double TicksPerSecond,
    double EntityGrowthPerSecond,
    double EntityCreatesPerSecond,
    double EntityDeletesPerSecond,
    double ComponentGrowthPerSecond,
    double ComponentAddsPerSecond,
    double ComponentRemovesPerSecond,
    double EntityDirtiesPerSecond)
{
    public double EntityChurnPerSecond => EntityCreatesPerSecond + EntityDeletesPerSecond;
    public double ComponentChurnPerSecond => ComponentAddsPerSecond + ComponentRemovesPerSecond;
}

/// <summary>
///     Fixed-time rolling rate window. It stores only scalar points and applies a hard point cap.
/// </summary>
internal sealed class CMUPerformanceRollingWindow
{
    private readonly Queue<TimedPoint> _points = new();
    private readonly double _windowSeconds;
    private readonly int _maxPoints;

    public CMUPerformanceRollingWindow(double windowSeconds, int maxPoints = 2048)
    {
        _windowSeconds = Math.Max(0.1, windowSeconds);
        _maxPoints = Math.Max(2, maxPoints);
    }

    public void Add(double timeSeconds, in CMUPerformanceCounterPoint point)
    {
        if (_points.TryPeek(out TimedPoint first) && timeSeconds <= first.TimeSeconds)
            _points.Clear();

        if (_points.Count > 0)
        {
            TimedPoint last = _points.Last();
            if (timeSeconds <= last.TimeSeconds ||
                point.Tick < last.Point.Tick ||
                point.EntitiesCreated < last.Point.EntitiesCreated ||
                point.EntitiesDeleted < last.Point.EntitiesDeleted ||
                point.ComponentsAdded < last.Point.ComponentsAdded ||
                point.ComponentsRemoved < last.Point.ComponentsRemoved ||
                point.EntitiesDirtied < last.Point.EntitiesDirtied)
            {
                _points.Clear();
            }
        }

        _points.Enqueue(new(timeSeconds, point));

        double cutoff = timeSeconds - _windowSeconds;
        while (_points.Count > 2 && _points.Peek().TimeSeconds < cutoff)
            _points.Dequeue();
        while (_points.Count > _maxPoints)
            _points.Dequeue();
    }

    public bool TryGetRates(double minimumSpanSeconds, out CMUPerformanceRates rates)
    {
        rates = default;
        if (_points.Count < 2)
            return false;

        TimedPoint first = _points.Peek();
        TimedPoint last = _points.Last();
        double span = last.TimeSeconds - first.TimeSeconds;
        if (span < Math.Max(0.001, minimumSpanSeconds))
            return false;

        rates = new(
            span,
            (last.Point.Tick - first.Point.Tick) / span,
            (last.Point.EntityCount - first.Point.EntityCount) / span,
            NonNegativeDelta(last.Point.EntitiesCreated, first.Point.EntitiesCreated) / span,
            NonNegativeDelta(last.Point.EntitiesDeleted, first.Point.EntitiesDeleted) / span,
            (last.Point.ComponentCount - first.Point.ComponentCount) / span,
            NonNegativeDelta(last.Point.ComponentsAdded, first.Point.ComponentsAdded) / span,
            NonNegativeDelta(last.Point.ComponentsRemoved, first.Point.ComponentsRemoved) / span,
            NonNegativeDelta(last.Point.EntitiesDirtied, first.Point.EntitiesDirtied) / span);
        return true;
    }

    public void Reset()
    {
        _points.Clear();
    }

    private static long NonNegativeDelta(long current, long previous)
    {
        return Math.Max(0, current - previous);
    }

    private readonly record struct TimedPoint(double TimeSeconds, CMUPerformanceCounterPoint Point);
}
