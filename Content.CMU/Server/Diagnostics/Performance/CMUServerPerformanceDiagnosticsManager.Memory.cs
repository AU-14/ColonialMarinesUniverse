using Content.Server.CMU14.ZLevels.Core;

namespace Content.Server.CMU14.Diagnostics.Performance;

public sealed partial class CMUServerPerformanceDiagnosticsManager
{
    private readonly CMURuntimeMemorySampler _memory = new();

    private string DescribeMemory()
    {
        if (_memory.Last is not { } memory)
            return "memorySample=unavailable";

        return Invariant(
            $"memorySampleAgeSeconds={Math.Max(0, (_timing.RealTime - memory.At).TotalSeconds):F2} ",
            $"processRssBytes={memory.RssBytes} processPrivateBytes={memory.PrivateBytes} ",
            $"managedBytes={memory.ManagedBytes} heapBytesAtLastGc={memory.HeapBytesAtLastGc} ",
            $"fragmentedBytesAtLastGc={memory.FragmentedBytesAtLastGc} committedBytesAtLastGc={memory.CommittedBytesAtLastGc} ",
            $"memoryLoadBytesAtLastGc={memory.MemoryLoadBytesAtLastGc} availableMemoryBytes={memory.AvailableMemoryBytes} ",
            $"gcIndex={memory.GcIndex} totalAllocatedBytes={memory.TotalAllocatedBytes} ",
            $"processAllocatedBytesPerSecond={memory.AllocatedBytesPerSecond:F0} ",
            $"gen0Collections={memory.Gen0Collections} gen1Collections={memory.Gen1Collections} gen2Collections={memory.Gen2Collections} ",
            $"threadPoolThreads={memory.ThreadPoolThreads} threadPoolPending={memory.ThreadPoolPending}");
    }

    private string DescribePvsRetention()
    {
        var usage = _entitySystemManager.GetEntitySystem<CMUZLevelsSystem>().GetPvsStorageUsage();
        return Invariant($"overheadActiveCells={usage.ActiveCells} overheadActiveViews={usage.ActiveViews} ",
            $"overheadActiveSessions={usage.ActiveSessions} overheadPooledBuffers={usage.PooledBuffers} ",
            $"overheadPooledMemberCapacity={usage.PooledMemberCapacity} overheadDictionaryCapacity={usage.DictionaryCapacity}");
    }
}
