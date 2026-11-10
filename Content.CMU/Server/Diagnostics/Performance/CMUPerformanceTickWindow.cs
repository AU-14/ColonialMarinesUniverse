namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Fixed-memory timing distributions for completed ticks, excluding frame sleep and input.</summary>
internal sealed class CMUPerformanceTickWindow
{
    public Distribution Simulation { get; } = new();
    public Distribution StateSend { get; } = new();
    public Distribution Combined { get; } = new();
    private CMUPerformancePhaseTracker.Phase? _simulation;

    public void Record(CMUPerformancePhaseTracker.Phase phase, double budgetMs)
    {
        switch (phase.Name)
        {
            case "timers-tasks-and-simulation":
                Simulation.Add(phase.TotalMs, phase.WorstTick, budgetMs);
                _simulation = phase;
                break;
            case "post-tick-and-state-send":
                StateSend.Add(phase.TotalMs, phase.WorstTick, budgetMs);
                if (_simulation is { } simulation && simulation.WorstTick == phase.WorstTick)
                    Combined.Add(simulation.TotalMs + phase.TotalMs, phase.WorstTick, budgetMs);
                _simulation = null;
                break;
        }
    }

    public void Clear()
    {
        Simulation.Clear();
        StateSend.Clear();
        Combined.Clear();
        _simulation = null;
    }

    internal sealed class Distribution
    {
        private static readonly double[] Bounds = [1, 2, 4, 8, 16, 1000d / 30, 50, 1000d / 15, 100, 250, 500, 1000, double.PositiveInfinity];
        private readonly long[] _buckets = new long[Bounds.Length];
        public long Count { get; private set; }
        public double TotalMs { get; private set; }
        public double MaxMs { get; private set; }
        public uint WorstTick { get; private set; }
        public long OverBudget { get; private set; }

        public void Add(double milliseconds, uint tick, double budgetMs)
        {
            if (!double.IsFinite(milliseconds) || milliseconds < 0)
                return;
            Count++;
            TotalMs += milliseconds;
            if (milliseconds >= MaxMs)
            {
                MaxMs = milliseconds;
                WorstTick = tick;
            }
            if (milliseconds > budgetMs)
                OverBudget++;
            for (var i = 0; i < Bounds.Length; i++)
            {
                if (milliseconds > Bounds[i])
                    continue;
                _buckets[i]++;
                break;
            }
        }

        // These are bucket upper bounds, not interpolated/exact quantiles.
        public double? PercentileUpperBound(double fraction)
        {
            if (Count == 0)
                return null;
            var rank = Math.Max(1, (long) Math.Ceiling(Count * fraction));
            long cumulative = 0;
            for (var i = 0; i < Bounds.Length; i++)
            {
                cumulative += _buckets[i];
                if (cumulative >= rank)
                    return Bounds[i];
            }
            return null;
        }

        public void Clear()
        {
            Array.Clear(_buckets);
            Count = 0;
            TotalMs = 0;
            MaxMs = 0;
            WorstTick = 0;
            OverBudget = 0;
        }
    }
}
