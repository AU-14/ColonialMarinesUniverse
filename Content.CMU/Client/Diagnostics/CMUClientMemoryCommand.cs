using System.Globalization;
using System.Linq;
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
    public string Description => "Prints client entity, component, prototype, and map counts.";
    public string Help =>
        "Usage:\n" +
        "  cmu_client_memory snapshot [top=15]\n" +
        "  cmu_client_memory baseline [top=15]\n" +
        "  cmu_client_memory diff [top=15]\n" +
        "\n" +
        "Use baseline, wait while counts grow, then diff.";

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
            ? CompletionResult.FromOptions(["snapshot", "baseline", "diff", "help"])
            : CompletionResult.Empty;
    }

    private Snapshot CollectSnapshot()
    {
        var componentCounts = CollectComponentCounts();
        var prototypeCounts = CollectPrototypeCounts(out var entityCount);
        var mapCounts = CollectMapCounts();

        return new Snapshot(
            _timing.CurTick.Value,
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
        shell.WriteLine("== CMU Client Counts ==");
        shell.WriteLine($"Tick: {snapshot.Tick:N0}");
        shell.WriteLine("Runtime memory, GC, process, and ThreadPool counters are omitted by the content sandbox.");
        shell.WriteLine($"Entities: {snapshot.EntityCount:N0}{FormatDelta(snapshot.EntityCount, previous?.EntityCount)} | Components: {snapshot.ComponentCount:N0}{FormatDelta(snapshot.ComponentCount, previous?.ComponentCount)}");
        shell.WriteLine("");
        WriteCounterRows(shell, "Top components", snapshot.ComponentCounts, previous?.ComponentCounts, top);
        shell.WriteLine("");
        WriteCounterRows(shell, "Top prototypes", snapshot.PrototypeCounts, previous?.PrototypeCounts, top);
        shell.WriteLine("");
        WriteCounterRows(shell, "Maps", snapshot.MapCounts, previous?.MapCounts, Math.Min(top, 30));
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

    private static string FormatDelta(int current, int? previous)
    {
        if (previous == null)
            return string.Empty;

        var delta = current - previous.Value;
        return delta >= 0
            ? $" (+{delta:N0})"
            : $" ({delta:N0})";
    }

    private sealed record Snapshot(
        uint Tick,
        int EntityCount,
        int ComponentCount,
        Dictionary<string, int> ComponentCounts,
        Dictionary<string, int> PrototypeCounts,
        Dictionary<string, int> MapCounts);
}
