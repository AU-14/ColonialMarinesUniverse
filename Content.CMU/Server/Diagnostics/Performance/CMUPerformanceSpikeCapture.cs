namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Bounds detail work without hiding new spikes behind unrelated incident cooldowns.</summary>
internal sealed class CMUPerformanceSpikeCapture
{
    private TimeSpan? _lastCapture;
    private double _milliseconds;
    private long _bytes;

    public bool ShouldCapture(TimeSpan now, double milliseconds, long bytes, double stallThreshold, double allocationThreshold)
    {
        if (!(stallThreshold > 0 && milliseconds >= stallThreshold) &&
            !(allocationThreshold > 0 && bytes >= allocationThreshold))
            return false;

        if (_lastCapture is not { } last || now - last >= TimeSpan.FromSeconds(10))
            return true;

        // A substantially worse spike gets its own capture, but never more than once per second.
        return now - last >= TimeSpan.FromSeconds(1) &&
               (stallThreshold > 0 && milliseconds >= Math.Max(stallThreshold, _milliseconds * 2) ||
                allocationThreshold > 0 && bytes >= Math.Max(allocationThreshold, _bytes * 2d));
    }

    public void Record(TimeSpan now, double milliseconds, long bytes)
    {
        _lastCapture = now;
        _milliseconds = milliseconds;
        _bytes = bytes;
    }

    public void Clear() => _lastCapture = null;
}
