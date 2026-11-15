using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Process-wide memory evidence, sampled at most once every five seconds without forcing a GC.</summary>
internal sealed class CMURuntimeMemorySampler : IDisposable
{
    private Process? _process;
    public CMUPerformanceDiagnosticsCost Cost { get; } = new();
    public CMURuntimeMemorySample? Last { get; private set; }

    public CMURuntimeMemorySample Sample(TimeSpan now)
    {
        if (Last is { } previous && now >= previous.At && now - previous.At < TimeSpan.FromSeconds(5))
            return previous;

        using var cost = Cost.Measure();
        var gc = GC.GetGCMemoryInfo();
        var allocated = GC.GetTotalAllocatedBytes(false);
        var mainThreadAllocated = GC.GetAllocatedBytesForCurrentThread();
        var sampleThread = Environment.CurrentManagedThreadId;
        double? allocationRate = null;
        double? mainThreadAllocationRate = null;
        var allocationRateStatus = "initial-sample";
        if (Last is { } last)
        {
            (allocationRate, allocationRateStatus) = MeasureAllocationRate(
                last.TotalAllocatedBytes, allocated, now - last.At);
            if (now > last.At && sampleThread == last.SampleThreadId &&
                mainThreadAllocated >= last.MainThreadAllocatedBytes)
            {
                mainThreadAllocationRate = (mainThreadAllocated - last.MainThreadAllocatedBytes) /
                    (now - last.At).TotalSeconds;
            }
        }
        long rss = -1;
        long privateBytes = -1;
        try
        {
            _process ??= Process.GetCurrentProcess();
            _process.Refresh();
            rss = _process.WorkingSet64;
            privateBytes = _process.PrivateMemorySize64;
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException or NotSupportedException)
        {
            // OS counters are optional. Keep GC evidence available and mark missing counters explicitly.
        }

        var sample = new CMURuntimeMemorySample(now, rss, privateBytes, GC.GetTotalMemory(false),
            gc.HeapSizeBytes, gc.FragmentedBytes, gc.TotalCommittedBytes, gc.MemoryLoadBytes,
            gc.TotalAvailableMemoryBytes, gc.Index, allocated, allocationRate, allocationRateStatus,
            mainThreadAllocated, mainThreadAllocationRate, sampleThread,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount);
        Last = sample;
        return sample;
    }

    internal static (double? Rate, string Status) MeasureAllocationRate(long before, long after, TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
            return (null, "clock-reset");

        // The approximate runtime counter can remain at a high-water mark while
        // allocations and collections continue. No progress is not a zero rate.
        if (after <= before)
            return (null, after < before ? "counter-reset" : "no-counter-progress");

        return ((after - before) / interval.TotalSeconds, "observed");
    }

    public void Dispose()
    {
        _process?.Dispose();
        _process = null;
        Last = null;
    }
}

internal readonly record struct CMURuntimeMemorySample(TimeSpan At, long RssBytes, long PrivateBytes,
    long ManagedBytes, long HeapBytesAtLastGc, long FragmentedBytesAtLastGc, long CommittedBytesAtLastGc,
    long MemoryLoadBytesAtLastGc, long AvailableMemoryBytes, long GcIndex, long TotalAllocatedBytes,
    double? AllocatedBytesPerSecond, string AllocationRateStatus, long MainThreadAllocatedBytes,
    double? MainThreadAllocatedBytesPerSecond, int SampleThreadId,
    int Gen0Collections, int Gen1Collections, int Gen2Collections,
    int ThreadPoolThreads, long ThreadPoolPending);
