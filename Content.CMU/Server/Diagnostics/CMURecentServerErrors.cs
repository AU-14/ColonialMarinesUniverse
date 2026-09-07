using System.Globalization;
using Serilog.Events;

namespace Content.Server.CMU14.Diagnostics;

/// <summary>
/// Retains four existing error events, not new stack traces. The logger can call this from worker threads;
/// rendering and logging are deliberately left to the rate-limited game-thread reporter.
/// </summary>
internal sealed class CMURecentServerErrors : ILogHandler
{
    internal const int Capacity = 4;
    internal const int MaxTextLength = 8192;
    private readonly object _lock = new();
    private readonly Error[] _recent = new Error[Capacity];
    private long _sequence;
    private int _count;
    private int _next;

    public void Log(string sawmillName, LogEvent message)
    {
        if (message.Level < LogEventLevel.Error || sawmillName == CMUClientStateDiagnosticsSystem.SawmillName)
            return;

        lock (_lock)
        {
            _recent[_next] = new Error(++_sequence, sawmillName, message);
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
        }
    }

    public List<Error> Snapshot(DateTimeOffset since, long afterId)
    {
        var result = new List<Error>(Capacity);
        lock (_lock)
        {
            for (var i = 0; i < _count; i++)
            {
                var entry = _recent[(_next - _count + i + Capacity) % Capacity];
                if (entry.Id > afterId && entry.Message.Timestamp >= since)
                    result.Add(entry);
            }
        }

        return result;
    }

    public void Clear()
    {
        lock (_lock)
        {
            Array.Clear(_recent);
            _count = 0;
            _next = 0;
        }
    }

    public static string Format(Error error)
    {
        var text = error.Message.RenderMessage(CultureInfo.InvariantCulture);
        if (error.Message.Exception is { } exception)
            text += $"\n{exception}";
        return text.Length <= MaxTextLength ? text : text[..MaxTextLength] + "\n[truncated; see original server error]";
    }

    internal readonly record struct Error(long Id, string Sawmill, LogEvent Message);
}
