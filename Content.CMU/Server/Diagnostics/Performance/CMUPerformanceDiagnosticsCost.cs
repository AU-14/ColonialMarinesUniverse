using System.Diagnostics;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Main-thread elapsed time and allocations, including synchronous log emission.</summary>
internal sealed class CMUPerformanceDiagnosticsCost
{
    public long Calls { get; private set; }
    public double TotalMilliseconds { get; private set; }
    public double MaximumMilliseconds { get; private set; }
    public long AllocatedBytes { get; private set; }

    public Scope Measure() => new(this);

    public readonly struct Scope : IDisposable
    {
        private readonly CMUPerformanceDiagnosticsCost _owner;
        private readonly long _started;
        private readonly long _allocated;

        internal Scope(CMUPerformanceDiagnosticsCost owner)
        {
            _owner = owner;
            _started = Stopwatch.GetTimestamp();
            _allocated = GC.GetAllocatedBytesForCurrentThread();
        }

        public void Dispose()
        {
            var elapsed = Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            _owner.Calls++;
            _owner.TotalMilliseconds += elapsed;
            _owner.MaximumMilliseconds = Math.Max(_owner.MaximumMilliseconds, elapsed);
            _owner.AllocatedBytes += GC.GetAllocatedBytesForCurrentThread() - _allocated;
        }
    }
}

/// <summary>Coalesce duplicate automatic detail work, retaining the omitted scalar evidence.</summary>
internal sealed class CMUPerformanceDetailGate
{
    public TimeSpan NextAllowed { get; private set; }
    public long Coalesced { get; private set; }
    public double WorstFrameMilliseconds { get; private set; }
    public long WorstAllocatedBytes { get; private set; }

    public bool TryCapture(TimeSpan now, double frameMilliseconds, long allocatedBytes)
    {
        if (now >= NextAllowed)
        {
            NextAllowed = now + TimeSpan.FromSeconds(1);
            return true;
        }
        Coalesced++;
        WorstFrameMilliseconds = Math.Max(WorstFrameMilliseconds, frameMilliseconds);
        WorstAllocatedBytes = Math.Max(WorstAllocatedBytes, allocatedBytes);
        return false;
    }
}
