using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Diagnostics;

public sealed partial class CMUClientMemoryCommand : IConsoleCommand
{
    private const int DefaultTop = 15;
    private const int MaxTop = 100;

    private static Snapshot? _baseline;

    [Dependency] private IComponentFactory _componentFactory = default!;
    [Dependency] private IEntityManager _entities = default!;
    [Dependency] private IGameTiming _timing = default!;

    public string Command => "cmu_client_memory";
    public string Description => "Prints client memory, entity, component, prototype, and map counts.";
    public string Help =>
        "Usage:\n" +
        "  cmu_client_memory snapshot [top=15]\n" +
        "  cmu_client_memory baseline [top=15]\n" +
        "  cmu_client_memory diff [top=15]\n" +
        "  cmu_client_memory gc [top=15]\n" +
        "\n" +
        "Use baseline, wait while memory grows, then diff. Use gc to separate managed heap growth from native/resource cache growth.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var mode = args.Length == 0 ? "snapshot" : args[0].ToLowerInvariant();
        var top = ReadTop(args, mode == "snapshot" ? 0 : 1);

        switch (mode)
        {
            case "snapshot":
                WriteSnapshot(shell, CollectSnapshot(), top, null);
                break;
            case "baseline":
                _baseline = CollectSnapshot();
                shell.WriteLine($"Client memory baseline captured at tick {_baseline.Tick:N0}.");
                WriteSnapshot(shell, _baseline, top, null);
                break;
            case "diff":
                var current = CollectSnapshot();
                WriteSnapshot(shell, current, top, _baseline);
                _baseline = current;
                break;
            case "gc":
                RunGc(shell, top);
                break;
            case "help":
                shell.WriteLine(Help);
                break;
            default:
                shell.WriteError($"Unknown mode '{mode}'.");
                shell.WriteLine(Help);
                break;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromOptions(["snapshot", "baseline", "diff", "gc", "help"])
            : CompletionResult.Empty;
    }

    private void RunGc(IConsoleShell shell, int top)
    {
        var before = CollectSnapshot();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var after = CollectSnapshot(forceFullCollection: true);

        shell.WriteLine("== CMU Client Memory After Forced GC ==");
        WriteMemorySummary(shell, "before", before, null);
        WriteMemorySummary(shell, "after ", after, before);
        shell.WriteLine("Managed falling while working/private stay high usually means native, graphics, audio, or resource cache pressure.");
        shell.WriteLine("Managed staying high after GC points at retained C# objects.");
        shell.WriteLine("");
        WriteSnapshot(shell, after, top, before);
    }

    private Snapshot CollectSnapshot(bool forceFullCollection = false)
    {
        using var process = Process.GetCurrentProcess();

        var componentCounts = CollectComponentCounts();
        var prototypeCounts = CollectPrototypeCounts(out var entityCount);
        var mapCounts = CollectMapCounts();

        return new Snapshot(
            _timing.CurTick.Value,
            GC.GetTotalMemory(forceFullCollection),
            process.WorkingSet64,
            process.PrivateMemorySize64,
            GC.GetTotalAllocatedBytes(false),
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            entityCount,
            componentCounts.Values.Sum(),
            componentCounts,
            prototypeCounts,
            mapCounts);
    }

    private Dictionary<string, int> CollectComponentCounts()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var registration in _componentFactory.GetAllRegistrations())
        {
            var count = 0;
            foreach (var (_, component) in _entities.GetAllComponents(registration.Type, includePaused: true))
            {
                if (!component.Deleted)
                    count++;
            }

            if (count > 0)
                result[registration.Name] = count;
        }

        return result;
    }

    private Dictionary<string, int> CollectPrototypeCounts(out int entityCount)
    {
        entityCount = 0;
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var uid in _entities.GetEntities())
        {
            if (!_entities.TryGetComponent(uid, out MetaDataComponent? meta))
                continue;

            entityCount++;
            var prototype = meta.EntityPrototype?.ID ?? "<none>";
            result.TryGetValue(prototype, out var count);
            result[prototype] = count + 1;
        }

        return result;
    }

    private Dictionary<string, int> CollectMapCounts()
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var uid in _entities.GetEntities())
        {
            if (!_entities.TryGetComponent(uid, out TransformComponent? xform))
                continue;

            var map = xform.MapID.ToString();
            result.TryGetValue(map, out var count);
            result[map] = count + 1;
        }

        return result;
    }

    private static void WriteSnapshot(IConsoleShell shell, Snapshot snapshot, int top, Snapshot? previous)
    {
        shell.WriteLine("== CMU Client Memory ==");
        shell.WriteLine($"Tick: {snapshot.Tick:N0}");
        WriteMemorySummary(shell, "now   ", snapshot, previous);
        shell.WriteLine($"Entities: {snapshot.EntityCount:N0}{FormatDelta(snapshot.EntityCount, previous?.EntityCount)} | Components: {snapshot.ComponentCount:N0}{FormatDelta(snapshot.ComponentCount, previous?.ComponentCount)}");
        shell.WriteLine($"GC collections: gen0={snapshot.Gen0:N0}{FormatDelta(snapshot.Gen0, previous?.Gen0)} gen1={snapshot.Gen1:N0}{FormatDelta(snapshot.Gen1, previous?.Gen1)} gen2={snapshot.Gen2:N0}{FormatDelta(snapshot.Gen2, previous?.Gen2)}");
        WriteThreadPool(shell);
        shell.WriteLine("");
        WriteCounterRows(shell, "Top components", snapshot.ComponentCounts, previous?.ComponentCounts, top);
        shell.WriteLine("");
        WriteCounterRows(shell, "Top prototypes", snapshot.PrototypeCounts, previous?.PrototypeCounts, top);
        shell.WriteLine("");
        WriteCounterRows(shell, "Maps", snapshot.MapCounts, previous?.MapCounts, Math.Min(top, 30));
    }

    private static void WriteMemorySummary(IConsoleShell shell, string label, Snapshot snapshot, Snapshot? previous)
    {
        shell.WriteLine(
            $"Memory {label}: managed={FormatBytes(snapshot.ManagedBytes)}{FormatDelta(snapshot.ManagedBytes, previous?.ManagedBytes)} " +
            $"working={FormatBytes(snapshot.WorkingBytes)}{FormatDelta(snapshot.WorkingBytes, previous?.WorkingBytes)} " +
            $"private={FormatBytes(snapshot.PrivateBytes)}{FormatDelta(snapshot.PrivateBytes, previous?.PrivateBytes)} " +
            $"allocated={FormatBytes(snapshot.TotalAllocatedBytes)}{FormatDelta(snapshot.TotalAllocatedBytes, previous?.TotalAllocatedBytes)}");
    }

    private static void WriteThreadPool(IConsoleShell shell)
    {
        ThreadPool.GetAvailableThreads(out var workerAvailable, out var ioAvailable);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);
        shell.WriteLine($"ThreadPool: worker busy={workerMax - workerAvailable:N0}/{workerMax:N0} io busy={ioMax - ioAvailable:N0}/{ioMax:N0}");
    }

    private static void WriteCounterRows(
        IConsoleShell shell,
        string title,
        IReadOnlyDictionary<string, int> current,
        IReadOnlyDictionary<string, int>? previous,
        int top)
    {
        shell.WriteLine($"== {title} ==");

        var keys = previous == null
            ? current.Keys
            : current.Keys.Union(previous.Keys, StringComparer.Ordinal);

        var rows = keys
            .Select(key => new KeyValuePair<string, int>(key, current.GetValueOrDefault(key)))
            .OrderByDescending(row => previous == null ? row.Value : Math.Abs(row.Value - previous.GetValueOrDefault(row.Key)))
            .ThenByDescending(row => row.Value)
            .ThenBy(row => row.Key, StringComparer.Ordinal)
            .Take(top)
            .ToArray();

        if (rows.Length == 0)
        {
            shell.WriteLine("  none");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. {row.Key,-48} count={row.Value,7:N0}{FormatDelta(row.Value, previous?.GetValueOrDefault(row.Key))}");
        }
    }

    private static int ReadTop(string[] args, int index)
    {
        if (index >= args.Length ||
            !int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var top))
        {
            return DefaultTop;
        }

        return Math.Clamp(top, 1, MaxTop);
    }

    private static string FormatDelta(long current, long? previous)
    {
        if (previous == null)
            return string.Empty;

        var delta = current - previous.Value;
        return delta >= 0
            ? $" (+{FormatBytes(delta)})"
            : $" (-{FormatBytes(-delta)})";
    }

    private static string FormatDelta(int current, int? previous)
    {
        if (previous == null)
            return string.Empty;

        var delta = current - previous.Value;
        return delta >= 0
            ? $" (+{delta:N0})"
            : $" ({delta:N0})";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KiB", "MiB", "GiB"];
        var value = (double) bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:N1}{units[unit]}";
    }

    private sealed record Snapshot(
        uint Tick,
        long ManagedBytes,
        long WorkingBytes,
        long PrivateBytes,
        long TotalAllocatedBytes,
        int Gen0,
        int Gen1,
        int Gen2,
        int EntityCount,
        int ComponentCount,
        Dictionary<string, int> ComponentCounts,
        Dictionary<string, int> PrototypeCounts,
        Dictionary<string, int> MapCounts);
}
