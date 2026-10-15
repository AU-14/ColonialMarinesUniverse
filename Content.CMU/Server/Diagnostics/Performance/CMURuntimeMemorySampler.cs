using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace Content.Server.CMU14.Diagnostics.Performance;

/// <summary>Process-wide memory evidence, sampled at most once every five seconds without forcing a GC.</summary>
internal sealed class CMURuntimeMemorySampler : IDisposable
{
    private Process? _process;
    public CMURuntimeMemorySample? Last { get; private set; }

    public CMURuntimeMemorySample Sample(TimeSpan now)
    {
        if (Last is { } previous && now >= previous.At && now - previous.At < TimeSpan.FromSeconds(5))
            return previous;

        var gc = GC.GetGCMemoryInfo();
        var allocated = GC.GetTotalAllocatedBytes(false);
        var allocationRate = Last is { } last && now > last.At
            ? Math.Max(0, allocated - last.TotalAllocatedBytes) / (now - last.At).TotalSeconds
            : 0;
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
            gc.TotalAvailableMemoryBytes, gc.Index, allocated, allocationRate,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2),
            ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount);
        Last = sample;
        return sample;
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
    double AllocatedBytesPerSecond, int Gen0Collections, int Gen1Collections, int Gen2Collections,
    int ThreadPoolThreads, long ThreadPoolPending);
