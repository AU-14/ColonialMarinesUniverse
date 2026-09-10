using System.Linq;
using Robust.Shared.Profiling;

namespace Content.Server.CMU14.Diagnostics.Performance;

internal readonly record struct CMUPerformanceProfileFrame(
    long IndexOffset,
    long? Frame,
    double TimeSeconds,
    long AllocatedBytes,
    int TickCount,
    bool Partial = false,
    long? FirstTick = null,
    long? LastTick = null);

internal readonly record struct CMUPerformanceProfileSample(
    string Kind,
    string Name,
    bool EntitySystem,
    int Count,
    double TotalSeconds,
    double MaxSeconds,
    long TotalAllocatedBytes,
    long MaxAllocatedBytes)
{
    public double AverageSeconds => Count == 0 ? 0 : TotalSeconds / Count;
}

internal readonly record struct CMUPerformanceProfileCounter(
    string Name,
    int Count,
    long Total,
    long Max,
    long Last);

internal sealed record CMUPerformanceProfileReport(
    IReadOnlyList<CMUPerformanceProfileFrame> Frames,
    IReadOnlyList<CMUPerformanceProfileSample> Samples,
    IReadOnlyList<CMUPerformanceProfileCounter> Counters,
    int EventsRead,
    bool Truncated,
    CMUPerformanceProfileCoverage Coverage);

internal readonly record struct CMUPerformanceProfileCoverage(
    string Status,
    int EventCapacity,
    int IndexCapacity,
    int IndexedFrames,
    int OverwrittenFrames,
    int PartialFrames,
    long UnindexedEvents);

internal readonly record struct CMUPerformanceProfileCandidate(
    long Offset,
    double TimeSeconds,
    long AllocatedBytes,
    int TickCount,
    long EventCount);

/// <summary>
///     Reads a bounded number of completed frames directly from the profiler ring on the server main thread.
/// </summary>
internal static class CMUPerformanceProfilerReader
{
    private const string ProfTextStartFrame = "Start Frame";

    public static bool TryGetFrameWindow(
        ProfManager profiler,
        long sinceIndexOffset,
        out CMUPerformanceProfileFrame frame,
        out long nextIndexOffset)
    {
        frame = default;
        nextIndexOffset = profiler.Buffer.IndexWriteOffset;
        if (!profiler.IsEnabled)
            return false;

        ProfBuffer buffer = profiler.Buffer;
        nextIndexOffset = buffer.IndexWriteOffset;
        long validLogStart = buffer.LogWriteOffset - buffer.LogBuffer.LongLength;
        long validIndexStart = Math.Max(0, buffer.IndexWriteOffset - buffer.IndexBuffer.LongLength);
        long start = Math.Clamp(sinceIndexOffset, validIndexStart, buffer.IndexWriteOffset);
        bool found = false;

        for (long offset = start; offset < buffer.IndexWriteOffset; offset++)
        {
            ref ProfIndex index = ref buffer.Index(offset);
            if (!TryGetRetainedRange(buffer, index, validLogStart, out var startPos))
                continue;

            TimeAndAllocSample timing = GetFrameTiming(buffer, index);
            if (found && timing.Alloc < frame.AllocatedBytes)
                continue;

            int tickCount = GetTickCount(profiler, buffer, index, startPos);
            bool partial = startPos != index.StartPos;
            frame = new(offset, partial ? null : TryGetFrameNumber(profiler, buffer, index), timing.Time, timing.Alloc, tickCount, partial);
            found = true;
        }

        return found;
    }

    public static CMUPerformanceProfileReport Capture(
        ProfManager profiler,
        IReadOnlySet<string> entitySystemNames,
        int frameLimit,
        int eventLimit)
    {
        if (!profiler.IsEnabled)
            return new([], [], [], 0, false, new("disabled", 0, 0, 0, 0, 0, 0));

        ProfBuffer buffer = profiler.Buffer;
        int framesToRead = Math.Clamp(frameLimit, 1, 128);
        int eventsToRead = Math.Clamp(eventLimit, 128, 100000);
        long validLogStart = buffer.LogWriteOffset - buffer.LogBuffer.LongLength;
        long validIndexStart = Math.Max(0, buffer.IndexWriteOffset - buffer.IndexBuffer.LongLength);
        var candidates = new List<CMUPerformanceProfileCandidate>((int) Math.Min(int.MaxValue,
            buffer.IndexWriteOffset - validIndexStart));
        int indexed = 0;
        int overwritten = 0;
        int partialFrames = 0;
        long lastEnd = 0;
        for (long offset = validIndexStart; offset < buffer.IndexWriteOffset; offset++)
        {
            ref ProfIndex index = ref buffer.Index(offset);
            if (index.Type != ProfIndexType.Frame)
                continue;
            indexed++;
            lastEnd = Math.Max(lastEnd, index.EndPos);
            if (!TryGetRetainedRange(buffer, index, validLogStart, out var startPos))
            {
                if (index.StartPos < validLogStart)
                    overwritten++;
                continue;
            }
            if (startPos != index.StartPos)
                partialFrames++;

            TimeAndAllocSample timing = GetFrameTiming(buffer, index);
            candidates.Add(new(
                offset,
                timing.Time,
                timing.Alloc,
                GetTickCount(profiler, buffer, index, startPos),
                index.EndPos - startPos));
        }

        var coverage = new CMUPerformanceProfileCoverage(
            candidates.Count > 0 ? (partialFrames > 0 ? "partial-history" : "available") :
            indexed == 0 ? "no-completed-frames" : overwritten > 0 ? "history-overwritten" : "invalid-frame-data",
            buffer.LogBuffer.Length, buffer.IndexBuffer.Length, indexed, overwritten, partialFrames,
            lastEnd > 0 ? Math.Max(0, buffer.LogWriteOffset - lastEnd) : 0);

        IReadOnlyList<long> selectedOffsets = SelectFrameOffsets(
            candidates,
            framesToRead,
            eventsToRead,
            out bool truncated);
        if (selectedOffsets.Count == 0)
            return new([], [], [], 0, false, coverage);

        var indices = selectedOffsets.ToList();
        var frames = new List<CMUPerformanceProfileFrame>(indices.Count);
        var samples = new Dictionary<string, SampleAccumulator>(StringComparer.Ordinal);
        var counters = new Dictionary<string, CounterAccumulator>(StringComparer.Ordinal);
        int eventsRead = 0;

        foreach (long offset in indices)
        {
            ref ProfIndex index = ref buffer.Index(offset);
            TimeAndAllocSample frameTiming = GetFrameTiming(buffer, index);
            long start = Math.Max(index.StartPos, validLogStart);
            long remaining = eventsToRead - eventsRead;
            if (index.EndPos - start > remaining)
            {
                start = index.EndPos - remaining;
                truncated = true;
            }

            bool partial = start != index.StartPos;
            long? firstTick = null;
            long? lastTick = null;

            for (long logOffset = start; logOffset < index.EndPos && eventsRead < eventsToRead; logOffset++)
            {
                eventsRead++;
                ref ProfLog log = ref buffer.Log(logOffset);
                switch (log.Type)
                {
                    case ProfLogType.Value:
                        if (log.Value.Value.Type == ProfValueType.Int64 && profiler.GetString(log.Value.StringId) == "Tick")
                        {
                            firstTick ??= log.Value.Value.Int64;
                            lastTick = log.Value.Value.Int64;
                        }
                        AddValue(profiler, entitySystemNames, samples, counters, log.Value);
                        break;
                    case ProfLogType.GroupEnd:
                        AddSample(
                            profiler,
                            entitySystemNames,
                            samples,
                            "group",
                            log.GroupEnd.StringId,
                            log.GroupEnd.Value);
                        break;
                }
            }

            frames.Add(new(offset, partial ? null : TryGetFrameNumber(profiler, buffer, index),
                frameTiming.Time, frameTiming.Alloc, GetTickCount(profiler, buffer, index, start),
                partial, firstTick, lastTick));
        }

        return new(
            frames,
            samples.Values.Select(sample => sample.ToRow()).ToArray(),
            counters.Values.Select(counter => counter.ToRow()).ToArray(),
            eventsRead,
            truncated,
            coverage);
    }

    internal static IReadOnlyList<long> SelectFrameOffsets(
        IReadOnlyList<CMUPerformanceProfileCandidate> candidates,
        int frameLimit,
        int eventLimit,
        out bool truncated)
    {
        int framesToRead = Math.Clamp(frameLimit, 1, 128);
        int eventsToRead = Math.Clamp(eventLimit, 128, 100000);
        int rankedCount = Math.Max(1, framesToRead / 3);
        var prioritized = new List<CMUPerformanceProfileCandidate>(framesToRead);
        var selected = new HashSet<long>();

        AddCandidates(candidates.OrderByDescending(candidate => candidate.TimeSeconds).Take(rankedCount));
        AddCandidates(candidates.OrderByDescending(candidate => candidate.AllocatedBytes).Take(rankedCount));
        AddCandidates(candidates
            .Where(candidate => candidate.TickCount > 0)
            .OrderByDescending(candidate => candidate.Offset));
        AddCandidates(candidates.OrderByDescending(candidate => candidate.Offset));

        truncated = false;
        long expectedEvents = 0;
        var offsets = new List<long>(prioritized.Count);
        foreach (CMUPerformanceProfileCandidate candidate in prioritized)
        {
            if (offsets.Count > 0 && expectedEvents + candidate.EventCount > eventsToRead)
            {
                truncated = true;
                continue;
            }

            offsets.Add(candidate.Offset);
            expectedEvents += candidate.EventCount;
        }

        offsets.Sort();
        return offsets;

        void AddCandidates(IEnumerable<CMUPerformanceProfileCandidate> source)
        {
            foreach (CMUPerformanceProfileCandidate candidate in source)
            {
                if (prioritized.Count >= framesToRead)
                    return;
                if (selected.Add(candidate.Offset))
                    prioritized.Add(candidate);
            }
        }
    }

    private static void AddValue(
        ProfManager profiler,
        IReadOnlySet<string> entitySystemNames,
        Dictionary<string, SampleAccumulator> samples,
        Dictionary<string, CounterAccumulator> counters,
        ProfLogValue log)
    {
        string name = profiler.GetString(log.StringId);
        if (name == ProfTextStartFrame)
            return;

        switch (log.Value.Type)
        {
            case ProfValueType.TimeAllocSample:
                AddSample(entitySystemNames, samples, "sample", name, log.Value.TimeAllocSample);
                break;
            case ProfValueType.Int32:
                AddCounter(counters, name, log.Value.Int32);
                break;
            case ProfValueType.Int64:
                AddCounter(counters, name, log.Value.Int64);
                break;
        }
    }

    private static void AddSample(
        ProfManager profiler,
        IReadOnlySet<string> entitySystemNames,
        Dictionary<string, SampleAccumulator> samples,
        string kind,
        int stringId,
        ProfValue value)
    {
        if (value.Type != ProfValueType.TimeAllocSample)
            return;

        string name = profiler.GetString(stringId);
        if (kind == "group" && name == "Frame")
            return;

        AddSample(entitySystemNames, samples, kind, name, value.TimeAllocSample);
    }

    private static void AddSample(
        IReadOnlySet<string> entitySystemNames,
        Dictionary<string, SampleAccumulator> samples,
        string kind,
        string name,
        TimeAndAllocSample value)
    {
        string key = $"{kind}:{name}";
        if (!samples.TryGetValue(key, out SampleAccumulator? sample))
        {
            sample = new(kind, name, kind == "sample" && entitySystemNames.Contains(name));
            samples.Add(key, sample);
        }

        sample.Add(value);
    }

    private static void AddCounter(Dictionary<string, CounterAccumulator> counters, string name, long value)
    {
        if (!counters.TryGetValue(name, out CounterAccumulator? counter))
        {
            counter = new(name);
            counters.Add(name, counter);
        }

        counter.Add(value);
    }

    internal static bool TryGetRetainedRange(ProfBuffer buffer, ProfIndex index, long validLogStart, out long start)
    {
        start = Math.Max(index.StartPos, validLogStart);
        if (index.Type != ProfIndexType.Frame || index.StartPos < 0 || index.EndPos <= start ||
            index.EndPos > buffer.LogWriteOffset)
            return false;

        // A frame can overwrite its own beginning. Its retained end still contains the full frame's
        // timing/allocation and finished scopes; report that tail explicitly as partial evidence.
        ref ProfLog end = ref buffer.Log(index.EndPos - 1);
        return end.Type == ProfLogType.GroupEnd && end.GroupEnd.Value.Type == ProfValueType.TimeAllocSample;
    }

    private static long? TryGetFrameNumber(ProfManager profiler, ProfBuffer buffer, ProfIndex index)
    {
        ref ProfLog start = ref buffer.Log(index.StartPos);
        if (start.Type != ProfLogType.Value ||
            start.Value.Value.Type != ProfValueType.Int64 ||
            profiler.GetString(start.Value.StringId) != ProfTextStartFrame)
            return null;

        return start.Value.Value.Int64;
    }

    private static TimeAndAllocSample GetFrameTiming(ProfBuffer buffer, ProfIndex index)
    {
        ref ProfLog end = ref buffer.Log(index.EndPos - 1);
        if (end.Type != ProfLogType.GroupEnd ||
            end.GroupEnd.Value.Type != ProfValueType.TimeAllocSample)
            return default;

        return end.GroupEnd.Value.TimeAllocSample;
    }

    private static int GetTickCount(ProfManager profiler, ProfBuffer buffer, ProfIndex index, long start)
    {
        for (long offset = index.EndPos - 1; offset >= start; offset--)
        {
            ref ProfLog log = ref buffer.Log(offset);
            if (log.Type != ProfLogType.Value ||
                log.Value.Value.Type != ProfValueType.Int32 ||
                profiler.GetString(log.Value.StringId) != "Tick count")
                continue;

            return Math.Max(0, log.Value.Value.Int32);
        }

        return -1;
    }

    private sealed class SampleAccumulator(string kind, string name, bool entitySystem)
    {
        private int _count;
        private double _totalSeconds;
        private double _maxSeconds;
        private long _totalAllocatedBytes;
        private long _maxAllocatedBytes;

        public void Add(TimeAndAllocSample sample)
        {
            _count++;
            _totalSeconds += sample.Time;
            _maxSeconds = Math.Max(_maxSeconds, sample.Time);
            _totalAllocatedBytes += sample.Alloc;
            _maxAllocatedBytes = Math.Max(_maxAllocatedBytes, sample.Alloc);
        }

        public CMUPerformanceProfileSample ToRow()
        {
            return new(
                kind,
                name,
                entitySystem,
                _count,
                _totalSeconds,
                _maxSeconds,
                _totalAllocatedBytes,
                _maxAllocatedBytes);
        }
    }

    private sealed class CounterAccumulator(string name)
    {
        private int _count;
        private long _total;
        private long _max;
        private long _last;

        public void Add(long value)
        {
            _count++;
            _total += value;
            _max = Math.Max(_max, value);
            _last = value;
        }

        public CMUPerformanceProfileCounter ToRow()
        {
            return new(name, _count, _total, _max, _last);
        }
    }

}
