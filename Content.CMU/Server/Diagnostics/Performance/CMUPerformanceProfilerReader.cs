using System.Linq;
using Robust.Shared.Profiling;

namespace Content.Server._CMU14.Diagnostics.Performance;

internal readonly record struct CMUPerformanceProfileFrame(
    long IndexOffset,
    long? Frame,
    double TimeSeconds,
    long AllocatedBytes,
    int TickCount);

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
    bool Truncated);

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
            if (!IsValidFrame(buffer, index, validLogStart))
                continue;

            TimeAndAllocSample timing = GetFrameTiming(buffer, index);
            if (found && timing.Alloc < frame.AllocatedBytes)
                continue;

            int tickCount = GetTickCount(profiler, buffer, index);
            frame = new(offset, TryGetFrameNumber(profiler, buffer, index), timing.Time, timing.Alloc, tickCount);
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
            return new([], [], [], 0, false);

        ProfBuffer buffer = profiler.Buffer;
        int framesToRead = Math.Clamp(frameLimit, 1, 128);
        int eventsToRead = Math.Clamp(eventLimit, 128, 100000);
        long validLogStart = buffer.LogWriteOffset - buffer.LogBuffer.LongLength;
        long validIndexStart = Math.Max(0, buffer.IndexWriteOffset - buffer.IndexBuffer.LongLength);
        var candidates = new List<CMUPerformanceProfileCandidate>((int) Math.Min(int.MaxValue,
            buffer.IndexWriteOffset - validIndexStart));
        for (long offset = validIndexStart; offset < buffer.IndexWriteOffset; offset++)
        {
            ref ProfIndex index = ref buffer.Index(offset);
            if (!IsValidFrame(buffer, index, validLogStart))
                continue;

            TimeAndAllocSample timing = GetFrameTiming(buffer, index);
            candidates.Add(new(
                offset,
                timing.Time,
                timing.Alloc,
                GetTickCount(profiler, buffer, index),
                index.EndPos - index.StartPos));
        }

        IReadOnlyList<long> selectedOffsets = SelectFrameOffsets(
            candidates,
            framesToRead,
            eventsToRead,
            out bool truncated);
        if (selectedOffsets.Count == 0)
            return new([], [], [], 0, false);

        var indices = selectedOffsets.ToList();
        var frames = new List<CMUPerformanceProfileFrame>(indices.Count);
        var samples = new Dictionary<string, SampleAccumulator>(StringComparer.Ordinal);
        var counters = new Dictionary<string, CounterAccumulator>(StringComparer.Ordinal);
        int eventsRead = 0;

        foreach (long offset in indices)
        {
            ref ProfIndex index = ref buffer.Index(offset);
            TimeAndAllocSample frameTiming = GetFrameTiming(buffer, index);
            frames.Add(new(
                offset,
                TryGetFrameNumber(profiler, buffer, index),
                frameTiming.Time,
                frameTiming.Alloc,
                GetTickCount(profiler, buffer, index)));

            long start = index.StartPos;
            long remaining = eventsToRead - eventsRead;
            if (index.EndPos - start > remaining)
            {
                start = index.EndPos - remaining;
                truncated = true;
            }

            for (long logOffset = start; logOffset < index.EndPos && eventsRead < eventsToRead; logOffset++)
            {
                eventsRead++;
                ref ProfLog log = ref buffer.Log(logOffset);
                switch (log.Type)
                {
                    case ProfLogType.Value:
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
        }

        return new(
            frames,
            samples.Values.Select(sample => sample.ToRow()).ToArray(),
            counters.Values.Select(counter => counter.ToRow()).ToArray(),
            eventsRead,
            truncated);
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

    private static bool IsValidFrame(ProfBuffer buffer, ProfIndex index, long validLogStart)
    {
        return index.Type == ProfIndexType.Frame &&
               index.StartPos >= validLogStart &&
               index.StartPos >= 0 &&
               index.EndPos > index.StartPos &&
               index.EndPos <= buffer.LogWriteOffset;
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

    private static int GetTickCount(ProfManager profiler, ProfBuffer buffer, ProfIndex index)
    {
        for (long offset = index.StartPos; offset < index.EndPos; offset++)
        {
            ref ProfLog log = ref buffer.Log(offset);
            if (log.Type != ProfLogType.Value ||
                log.Value.Value.Type != ProfValueType.Int32 ||
                profiler.GetString(log.Value.StringId) != "Tick count")
                continue;

            return Math.Max(0, log.Value.Value.Int32);
        }

        return 0;
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
