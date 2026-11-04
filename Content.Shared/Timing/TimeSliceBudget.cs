using System.Diagnostics;

namespace Content.Shared.Timing;

/// <summary>
/// Bounds synchronous work using wall time, which advances even when simulation time does not.
/// Check this before dequeuing work. A non-empty slice always permits its first item to make progress.
/// </summary>
public struct TimeSliceBudget(TimeSpan duration, int maxItems)
{
    private readonly long _started = Stopwatch.GetTimestamp();
    private int _processed;

    public bool TryConsume()
    {
        if (_processed >= maxItems ||
            (_processed > 0 && Stopwatch.GetElapsedTime(_started) >= duration))
            return false;

        _processed++;
        return true;
    }
}
