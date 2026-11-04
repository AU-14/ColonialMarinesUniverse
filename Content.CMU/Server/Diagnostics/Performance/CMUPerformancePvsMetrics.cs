using Prometheus;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>
/// Reads the engine's process-wide state-update timers without scraping the metrics registry.
/// Call from the main thread between ticks; count and sum are not an atomic snapshot.
/// </summary>
internal sealed class CMUPerformancePvsMetrics
{
    private static readonly string[] Areas =
    [
        "Get Chunks",
        "Update Chunks & Overrides",
        "Serialize States",
        "Send States",
        "Clean Dirty",
        "Cull History",
        "Process Acks",
        "Process Leave",
    ];

    private readonly Histogram.Child[] _children = new Histogram.Child[Areas.Length];
    private readonly long[] _counts = new long[Areas.Length];
    private readonly double[] _sums = new double[Areas.Length];
    private TimeSpan _lastDrain;

    public CMUPerformancePvsMetrics(IMetricFactory factory, TimeSpan now)
    {
        // Match Robust.Server/GameStates/PvsSystem.cs. The factory returns the existing collector;
        // matching its schema also keeps initialization safe if content registers it first.
        var histogram = factory.CreateHistogram("robust_game_state_update_usage",
            "Amount of time spent processing different parts of the game state update",
            new HistogramConfiguration
            {
                LabelNames = ["area"],
                Buckets = Histogram.ExponentialBuckets(0.000_001, 1.5, 25),
            });

        for (var i = 0; i < Areas.Length; i++)
            _children[i] = histogram.WithLabels(Areas[i]);

        Reset(now);
    }

    public void Reset(TimeSpan now)
    {
        for (var i = 0; i < _children.Length; i++)
        {
            _counts[i] = _children[i].Count;
            _sums[i] = _children[i].Sum;
        }

        _lastDrain = now;
    }

    public CMUPerformancePvsWindow Drain(TimeSpan now)
    {
        var rows = new CMUPerformancePvsStage[_children.Length];
        for (var i = 0; i < _children.Length; i++)
        {
            var count = _children[i].Count;
            var sum = _children[i].Sum;
            var calls = count - _counts[i];
            var seconds = sum - _sums[i];
            var valid = now >= _lastDrain && calls >= 0 && seconds >= 0 && double.IsFinite(seconds);
            // No calls means unobserved, not free: async ACK/leave work is not timed by the engine.
            var status = !valid ? "counter-reset" : calls == 0 ? "no-observations" : "observed";
            rows[i] = new(Areas[i], valid ? calls : 0, valid && calls > 0 ? seconds * 1000 : null, status);
            _counts[i] = count;
            _sums[i] = sum;
        }

        var window = new CMUPerformancePvsWindow(Math.Max(0, (now - _lastDrain).TotalSeconds), rows);
        _lastDrain = now;
        return window;
    }
}

internal readonly record struct CMUPerformancePvsStage(string Name, long Calls, double? TotalMs, string Status)
{
    public double? AverageMs => Calls > 0 ? TotalMs / Calls : null;
}

internal readonly record struct CMUPerformancePvsWindow(double Seconds, IReadOnlyList<CMUPerformancePvsStage> Stages);
