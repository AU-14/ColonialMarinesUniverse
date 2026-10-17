using System.Linq;
using Serilog.Events;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>
/// Counts errors at their existing logging boundary, including PVS worker threads. Never renders messages,
/// captures stacks, or writes logs from the callback. Memory is bounded even during an exception storm.
/// </summary>
internal sealed class CMUPerformanceErrorTracker : ILogHandler
{
    internal const int SourceCapacity = 32;
    private readonly object _lock = new();
    private readonly Dictionary<string, Source> _sources = new(StringComparer.Ordinal);
    private long _total;
    private long _overflow;

    public void Log(string sawmillName, LogEvent message)
    {
        if (message.Level < LogEventLevel.Error ||
            sawmillName is "cmu.server-performance" or "cmu.client_state")
            return;

        lock (_lock)
        {
            _total++;
            if (_sources.TryGetValue(sawmillName, out var source))
            {
                _sources[sawmillName] = source with { Count = source.Count + 1, Last = message };
            }
            else if (_sources.Count < SourceCapacity)
            {
                _sources.Add(sawmillName, new Source(sawmillName, 1, message.Timestamp, message));
            }
            else
            {
                _overflow++;
            }
        }
    }

    public Snapshot Drain()
    {
        lock (_lock)
        {
            var snapshot = new Snapshot(_total, _overflow, _sources.Values.ToArray());
            _sources.Clear();
            _total = 0;
            _overflow = 0;
            return snapshot;
        }
    }

    internal readonly record struct Source(string Sawmill, long Count, DateTimeOffset FirstAt, LogEvent Last);
    internal sealed record Snapshot(long Total, long Overflow, Source[] Sources);
}
