using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Profiling;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Administration.Console;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class LagProfileCommand : IConsoleCommand
{
    [Dependency] private ProfManager _prof = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IGameTiming _timing = default!;
    private const string ProfTextStartFrame = "Start Frame";
    private bool _enabledProfiler;

    private long? _startIndexOffset;
    private TimeSpan _startTime;

    public string Command => "lagprofile";
    public string Description => "Controls server lag profile capture and reporting.";

    public string Help
        => "Usage: lagprofile start | lagprofile report [frames=300] [top=25] | lagprofile stop [frames=0] [top=25]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        string mode = args.Length == 0 ? "report" : args[0].ToLowerInvariant();
        switch (mode)
        {
            case "start":
                if (args.Length != 1)
                {
                    shell.WriteError(Help);
                    return;
                }

                Start(shell);
                return;
            case "report":
                if (!TryParseReportArgs(shell, args, 300, out int frames, out int top))
                    return;

                Report(shell, frames, top, false);
                return;
            case "stop":
                if (!TryParseReportArgs(shell, args, 0, out frames, out top))
                    return;

                Stop(shell, frames, top);
                return;
            default:
                shell.WriteError(Help);
                return;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            return CompletionResult.FromHintOptions(
                new[]
                {
                    new CompletionOption("start", "enable profiling and mark a starting frame"),
                    new CompletionOption("report", "print an aggregate report"),
                    new CompletionOption("stop", "report and stop the capture")
                },
                "subcommand");
        }

        if (args.Length == 2 &&
            (args[0].Equals("report", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("stop", StringComparison.OrdinalIgnoreCase)))
            return CompletionResult.FromHint("frames to read, 0 for all valid frames since start");

        if (args.Length == 3 &&
            (args[0].Equals("report", StringComparison.OrdinalIgnoreCase) ||
                args[0].Equals("stop", StringComparison.OrdinalIgnoreCase)))
            return CompletionResult.FromHint("number of top entries to print");

        return CompletionResult.Empty;
    }

    private void Start(IConsoleShell shell)
    {
        if (!_prof.IsEnabled)
        {
            _config.SetCVar(CVars.ProfEnabled, true);
            _enabledProfiler = true;
        }
        else
            _enabledProfiler = false;

        _startIndexOffset = _prof.Buffer.IndexWriteOffset;
        _startTime = _timing.CurTime;

        shell.WriteLine(
            "Lag profiler started. Run `lagprofile report` to inspect samples or `lagprofile stop` to report and stop.");
    }

    private void Stop(IConsoleShell shell, int frameLimit, int topCount)
    {
        Report(shell, frameLimit, topCount, true);

        _startIndexOffset = null;

        if (!_enabledProfiler)
            return;

        _config.SetCVar(CVars.ProfEnabled, false);
        _enabledProfiler = false;
        shell.WriteLine("Profiling disabled because lagprofile enabled it.");
    }

    private void Report(IConsoleShell shell, int frameLimit, int topCount, bool includeAllSinceStart)
    {
        if (!_prof.IsEnabled)
        {
            shell.WriteError("Profiling is disabled. Run `lagprofile start` or `cvar prof.enabled true` first.");
            return;
        }

        ProfBuffer snapshot = _prof.Buffer.Snapshot();
        long? sinceStart = includeAllSinceStart ? _startIndexOffset : null;
        List<long> frameIndices = LagProfileCommand.GetFrameIndices(snapshot, frameLimit, sinceStart);
        if (frameIndices.Count == 0)
        {
            shell.WriteError("No valid profile frames were found. Let the server run for a few frames and try again.");
            return;
        }

        var samples = new Dictionary<string, LagProfileSample>();
        var counters = new Dictionary<string, LagProfileCounter>();
        var frames = new List<LagProfileFrame>(frameIndices.Count);

        foreach (long indexOffset in frameIndices)
        {
            ref ProfIndex index = ref snapshot.Index(indexOffset);
            long? frame = TryGetFrameNumber(snapshot, index);
            TimeAndAllocSample frameTime = LagProfileCommand.TryGetFrameTime(snapshot, index);
            frames.Add(new(frame, frameTime.Time, frameTime.Alloc));

            for (long logOffset = index.StartPos; logOffset < index.EndPos; logOffset++)
            {
                ref ProfLog log = ref snapshot.Log(logOffset);
                switch (log.Type)
                {
                    case ProfLogType.Value:
                        AddValue(samples, counters, log.Value.StringId, log.Value.Value);
                        break;
                    case ProfLogType.GroupEnd:
                        AddSample(samples, "group", log.GroupEnd.StringId, log.GroupEnd.Value);
                        break;
                }
            }
        }

        bool reportingSinceStart = sinceStart is not null;
        TimeSpan elapsed = reportingSinceStart ? _timing.CurTime - _startTime : TimeSpan.Zero;
        string source = reportingSinceStart
            ? $"since lagprofile start ({elapsed.TotalSeconds:N1}s)"
            : "recent profile buffer";

        shell.WriteLine($"Lag profile report from {source}: {frames.Count} frame(s), top {topCount} sample(s).");
        shell.WriteLine("Slowest frames:");
        foreach (LagProfileFrame frame in frames.OrderByDescending(frame => frame.TimeSeconds)
            .Take(Math.Min(topCount, 10)))
        {
            string frameName = frame.Frame is { } number ? number.ToString() : "?";
            shell.WriteLine($"  frame {frameName,8}: {frame.TimeSeconds * 1000:N2} ms, {
                LagProfileCommand.FormatBytes(frame.AllocatedBytes)} allocated");
        }

        shell.WriteLine("Top profile entries by total time:");
        foreach (LagProfileSample sample in samples.Values
            .OrderByDescending(sample => sample.TotalSeconds)
            .ThenByDescending(sample => sample.MaxSeconds)
            .Take(topCount))
        {
            shell.WriteLine(
                $"  {sample.Kind,-6} {sample.Name,-48} calls={sample.Count,5} total={sample.TotalSeconds * 1000,
                    9:N2} ms avg={sample.AverageSeconds * 1000,7:N3} ms max={sample.MaxSeconds * 1000,7:N3} ms alloc={
                    LagProfileCommand.FormatBytes(sample.AllocatedBytes)}");
        }

        if (counters.Count == 0)
            return;

        shell.WriteLine("Profile counters by max value:");
        foreach (LagProfileCounter counter in counters.Values
            .OrderByDescending(counter => counter.Max)
            .ThenByDescending(counter => counter.Average)
            .Take(topCount))
        {
            shell.WriteLine(
                $"  sample {counter.Name,-48} calls={counter.Count,5} avg={counter.Average,9:N2} max={counter.Max,
                    9:N0} last={counter.Last,9:N0}");
        }
    }

    private bool TryParseReportArgs(IConsoleShell shell,
        string[] args,
        int defaultFrameLimit,
        out int frameLimit,
        out int topCount)
    {
        frameLimit = defaultFrameLimit;
        topCount = 25;

        if (args.Length > 3)
        {
            shell.WriteError(Help);
            return false;
        }

        if (args.Length >= 2 &&
            (!int.TryParse(args[1], out frameLimit) || frameLimit < 0))
        {
            shell.WriteError("Frame count must be a non-negative integer.");
            return false;
        }

        if (args.Length >= 3 &&
            (!int.TryParse(args[2], out topCount) || topCount <= 0))
        {
            shell.WriteError("Top count must be a positive integer.");
            return false;
        }

        return true;
    }

    private void AddValue(Dictionary<string, LagProfileSample> samples,
        Dictionary<string, LagProfileCounter> counters,
        int stringId,
        ProfValue value)
    {
        string name = _prof.GetString(stringId);
        if (name == ProfTextStartFrame)
            return;

        switch (value.Type)
        {
            case ProfValueType.TimeAllocSample:
                LagProfileCommand.AddSample(samples, "sample", name, value);
                break;
            case ProfValueType.Int32:
                LagProfileCommand.AddCounter(counters, name, value.Int32);
                break;
            case ProfValueType.Int64:
                LagProfileCommand.AddCounter(counters, name, value.Int64);
                break;
        }
    }

    private void AddSample(Dictionary<string, LagProfileSample> samples, string kind, int stringId, ProfValue value)
    {
        LagProfileCommand.AddSample(samples, kind, _prof.GetString(stringId), value);
    }

    private static void AddSample(Dictionary<string, LagProfileSample> samples, string kind, string name,
        ProfValue value)
    {
        if (value.Type != ProfValueType.TimeAllocSample)
            return;

        if (kind == "group" && name == "Frame")
            return;

        var key = $"{kind}:{name}";
        if (!samples.TryGetValue(key, out LagProfileSample? sample))
        {
            sample = new(kind, name);
            samples.Add(key, sample);
        }

        sample.Add(value.TimeAllocSample);
    }

    private static void AddCounter(Dictionary<string, LagProfileCounter> counters, string name, long value)
    {
        if (!counters.TryGetValue(name, out LagProfileCounter? counter))
        {
            counter = new(name);
            counters.Add(name, counter);
        }

        counter.Add(value);
    }

    private static List<long> GetFrameIndices(ProfBuffer snapshot, int frameLimit, long? sinceIndexOffset)
    {
        long validLogStart = snapshot.LogWriteOffset - snapshot.LogBuffer.LongLength;
        long validIndexStart = Math.Max(0, snapshot.IndexWriteOffset - snapshot.IndexBuffer.LongLength);
        if (sinceIndexOffset is { } start)
            validIndexStart = Math.Max(validIndexStart, start);

        var frames = new List<long>();
        for (long indexOffset = snapshot.IndexWriteOffset - 1; indexOffset >= validIndexStart; indexOffset--)
        {
            ref ProfIndex index = ref snapshot.Index(indexOffset);
            if (index.Type != ProfIndexType.Frame ||
                index.StartPos < validLogStart ||
                index.EndPos > snapshot.LogWriteOffset)
                continue;

            frames.Add(indexOffset);
            if (frameLimit > 0 && frames.Count >= frameLimit)
                break;
        }

        frames.Reverse();
        return frames;
    }

    private long? TryGetFrameNumber(ProfBuffer snapshot, ProfIndex index)
    {
        ref ProfLog start = ref snapshot.Log(index.StartPos);
        if (start.Type != ProfLogType.Value ||
            start.Value.Value.Type != ProfValueType.Int64 ||
            _prof.GetString(start.Value.StringId) != ProfTextStartFrame)
            return null;

        return start.Value.Value.Int64;
    }

    private static TimeAndAllocSample TryGetFrameTime(ProfBuffer snapshot, ProfIndex index)
    {
        ref ProfLog end = ref snapshot.Log(index.EndPos - 1);
        if (end.Type != ProfLogType.GroupEnd ||
            end.GroupEnd.Value.Type != ProfValueType.TimeAllocSample)
            return default(TimeAndAllocSample);

        return end.GroupEnd.Value.TimeAllocSample;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes:N0} B";

        double kib = bytes / 1024d;
        if (kib < 1024)
            return $"{kib:N1} KiB";

        return $"{kib / 1024d:N1} MiB";
    }

    private sealed class LagProfileSample
    {
        public readonly string Kind;
        public readonly string Name;
        public long AllocatedBytes;
        public int Count;
        public double MaxSeconds;
        public double TotalSeconds;

        public LagProfileSample(string kind, string name)
        {
            Kind = kind;
            Name = name;
        }

        public double AverageSeconds => Count == 0 ? 0 : TotalSeconds / Count;

        public void Add(TimeAndAllocSample sample)
        {
            Count++;
            TotalSeconds += sample.Time;
            MaxSeconds = Math.Max(MaxSeconds, sample.Time);
            AllocatedBytes += sample.Alloc;
        }
    }

    private sealed class LagProfileCounter
    {
        public readonly string Name;
        public int Count;
        public long Last;
        public long Max;
        public double Total;

        public LagProfileCounter(string name) => Name = name;

        public double Average => Count == 0 ? 0 : Total / Count;

        public void Add(long value)
        {
            Count++;
            Total += value;
            Max = Math.Max(Max, value);
            Last = value;
        }
    }

    private readonly record struct LagProfileFrame(long? Frame, double TimeSeconds, long AllocatedBytes);
}
