using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Content.Shared.Administration;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Profiling;
using Robust.Shared.Timing;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ServerPerformanceCommand : IConsoleCommand
{
    private const int DefaultDirtyTicks = 5;
    private const int DefaultFrames = 10;
    private const int DefaultTop = 20;
    private const int MaxDirtyTicks = 600;
    private const int MaxFrames = 300;
    private const int MaxTop = 100;

    private static readonly string[] Modes =
    [
        "snapshot",
        "deep",
        "baseline",
        "components",
        "entities",
        "prototypes",
        "maps",
        "systems",
        "profile",
        "metrics",
        "help",
    ];

    public string Command => "serverperf";

    public string Description => "Prints detailed server performance diagnostics for lag hunting.";

    public string Help =>
        "Usage:\n" +
        "  serverperf snapshot [top=20] [dirtyTicks=5]\n" +
        "  serverperf deep [top=20] [dirtyTicks=30] [filter]\n" +
        "  serverperf baseline [dirtyTicks=30]\n" +
        "  serverperf components [top=20] [dirtyTicks=5] [filter]\n" +
        "  serverperf entities [top=20] [dirtyTicks=5] [filter]\n" +
        "  serverperf prototypes [top=20] [filter]\n" +
        "  serverperf maps [top=20]\n" +
        "  serverperf systems [top=20] [filter]\n" +
        "  serverperf profile on|off|top|systems|engine [frames=10] [top=20] [filter]\n" +
        "  serverperf metrics on|off|status\n" +
        "\n" +
        "For before/after lag hunts: serverperf baseline, trigger the action, then serverperf deep 40 120.\n" +
        "Use 'serverperf profile on' during lag, wait a few seconds, then run 'serverperf profile systems'.";

    private static SnapshotState? LastSnapshot;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var mode = args.Length == 0 ? "snapshot" : args[0].ToLowerInvariant();

        switch (mode)
        {
            case "snapshot":
                RunSnapshot(shell, args);
                break;
            case "deep":
                RunDeep(shell, args);
                break;
            case "baseline":
                RunBaseline(shell, args);
                break;
            case "components":
                RunComponents(shell, args);
                break;
            case "entities":
                RunEntities(shell, args);
                break;
            case "prototypes":
                RunPrototypes(shell, args);
                break;
            case "maps":
                RunMaps(shell, args);
                break;
            case "systems":
                RunSystems(shell, args);
                break;
            case "profile":
                RunProfile(shell, args);
                break;
            case "metrics":
                RunMetrics(shell, args);
                break;
            case "help":
                shell.WriteLine(Help);
                break;
            default:
                shell.WriteError($"Unknown serverperf mode '{mode}'.");
                shell.WriteLine(Help);
                break;
        }
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(Modes),
            2 when args[0].Equals("profile", StringComparison.OrdinalIgnoreCase) =>
                CompletionResult.FromOptions(["on", "off", "top", "systems", "engine"]),
            2 when args[0].Equals("metrics", StringComparison.OrdinalIgnoreCase) =>
                CompletionResult.FromOptions(["on", "off", "status"]),
            _ => CompletionResult.Empty,
        };
    }

    private static void RunSnapshot(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var dirtyTicks = ReadInt(args, ref index, DefaultDirtyTicks, 1, MaxDirtyTicks);
        var snapshot = CollectSnapshot(dirtyTicks, null);

        shell.WriteLine("== Server Performance Snapshot ==");
        WriteSummary(shell);
        shell.WriteLine("");
        WriteProfileTop(shell, ProfileMode.Systems, DefaultFrames, Math.Min(top, 15), null, includeDisabledHint: false);
        shell.WriteLine("");
        WriteSnapshotDeltaReport(shell, snapshot, top, null);
        shell.WriteLine("");
        WriteComponentReport(shell, snapshot, top, null);
        shell.WriteLine("");
        WriteEntityReport(shell, snapshot, top, null);
        shell.WriteLine("");
        WritePrototypeReport(shell, snapshot, top, null);
        shell.WriteLine("");
        WriteMapReport(shell, snapshot, top);
        LastSnapshot = snapshot;
    }

    private static void RunDeep(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var dirtyTicks = ReadInt(args, ref index, 30, 1, MaxDirtyTicks);
        var filter = ReadFilter(args, index);
        var snapshot = CollectSnapshot(dirtyTicks, filter);

        shell.WriteLine("== Deep Server Performance Snapshot ==");
        WriteSummary(shell);
        shell.WriteLine("");
        WriteProfileTop(shell, ProfileMode.Systems, Math.Max(DefaultFrames, 30), Math.Min(top, 30), filter, includeDisabledHint: true);
        shell.WriteLine("");
        WriteSnapshotDeltaReport(shell, snapshot, top, filter);
        shell.WriteLine("");
        WriteRecentCreationReport(shell, snapshot, top, filter);
        shell.WriteLine("");
        WriteComponentReport(shell, snapshot, top, filter);
        shell.WriteLine("");
        WriteEntityReport(shell, snapshot, top, filter);
        shell.WriteLine("");
        WriteEntityDetailReport(shell, snapshot, Math.Min(top, 30), filter);
        shell.WriteLine("");
        WritePrototypeReport(shell, snapshot, top, filter);
        shell.WriteLine("");
        WriteMapReport(shell, snapshot, top);
        LastSnapshot = snapshot;
    }

    private static void RunBaseline(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var dirtyTicks = ReadInt(args, ref index, 30, 1, MaxDirtyTicks);
        LastSnapshot = CollectSnapshot(dirtyTicks, null);

        shell.WriteLine($"Baseline captured at tick {LastSnapshot.Tick.Value:N0}.");
        shell.WriteLine($"Entities={LastSnapshot.EntityCount:N0} components={LastSnapshot.TotalComponents:N0} prototypes={LastSnapshot.Prototypes.Count:N0} maps={LastSnapshot.Maps.Count:N0}");
        shell.WriteLine("Trigger the action, then run: serverperf deep 40 120");
    }

    private static void RunComponents(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var dirtyTicks = ReadInt(args, ref index, DefaultDirtyTicks, 1, MaxDirtyTicks);
        var filter = ReadFilter(args, index);

        WriteComponentReport(shell, CollectComponentStats(dirtyTicks, filter), dirtyTicks, top, filter);
    }

    private static void RunEntities(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var dirtyTicks = ReadInt(args, ref index, DefaultDirtyTicks, 1, MaxDirtyTicks);
        var filter = ReadFilter(args, index);

        WriteEntityReport(shell, CollectEntityStats(dirtyTicks, filter), dirtyTicks, top, filter);
    }

    private static void RunPrototypes(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var filter = ReadFilter(args, index);

        WritePrototypeReport(shell, CollectPrototypeStats(DefaultDirtyTicks, filter), top, filter);
    }

    private static void RunMaps(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);

        WriteMapReport(shell, CollectMapStats(DefaultDirtyTicks), top);
    }

    private static void RunSystems(IConsoleShell shell, string[] args)
    {
        var index = 1;
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var filter = ReadFilter(args, index);

        var systems = IoCManager.Resolve<IEntitySystemManager>();
        var updateOrder = GetUpdateSystemOrder(systems);
        var frameOrder = GetFrameSystemOrder(systems);
        var loaded = systems.GetEntitySystemTypes().OrderBy(t => t.Name).ToArray();

        shell.WriteLine("== Entity Systems ==");
        shell.WriteLine($"Loaded: {loaded.Length:N0} | Tick update: {updateOrder.Count:N0} | Frame update: {frameOrder.Count:N0} | MetricsEnabled: {systems.MetricsEnabled}");

        var rows = updateOrder
            .Select((type, i) => new SystemRow(i, type, frameOrder.Contains(type)))
            .Where(row => Matches(filter, row.Type.Name, row.Type.FullName ?? string.Empty))
            .Take(top)
            .ToArray();

        if (rows.Length == 0)
        {
            shell.WriteLine("No matching tick-updating systems.");
            return;
        }

        shell.WriteLine($"Top {rows.Length:N0} matching tick update systems by update order:");
        foreach (var row in rows)
        {
            shell.WriteLine($"{row.Index + 1,4}. {row.Type.Name,-48} frame={YesNo(row.HasFrameUpdate),3} {row.Type.Namespace}");
        }

        shell.WriteLine("For timings: serverperf profile on, wait during lag, then serverperf profile systems.");
    }

    private static void RunProfile(IConsoleShell shell, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteLine("Usage: serverperf profile on|off|top|systems|engine [frames=10] [top=20] [filter]");
            return;
        }

        var action = args[1].ToLowerInvariant();
        var cfg = IoCManager.Resolve<IConfigurationManager>();

        switch (action)
        {
            case "on":
                cfg.SetCVar(CVars.ProfEnabled, true);
                shell.WriteLine("Profiler enabled. Let the server run during lag, then use: serverperf profile systems");
                return;
            case "off":
                cfg.SetCVar(CVars.ProfEnabled, false);
                shell.WriteLine("Profiler disabled.");
                return;
            case "top":
            case "systems":
            case "engine":
                break;
            default:
                shell.WriteError($"Unknown profile action '{action}'.");
                shell.WriteLine("Usage: serverperf profile on|off|top|systems|engine [frames=10] [top=20] [filter]");
                return;
        }

        var index = 2;
        var frames = ReadInt(args, ref index, DefaultFrames, 1, MaxFrames);
        var top = ReadInt(args, ref index, DefaultTop, 1, MaxTop);
        var filter = ReadFilter(args, index);
        var mode = action switch
        {
            "systems" => ProfileMode.Systems,
            "engine" => ProfileMode.Engine,
            _ => ProfileMode.All,
        };

        WriteProfileTop(shell, mode, frames, top, filter, includeDisabledHint: true);
    }

    private static void RunMetrics(IConsoleShell shell, string[] args)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var systems = IoCManager.Resolve<IEntitySystemManager>();

        if (args.Length < 2 || args[1].Equals("status", StringComparison.OrdinalIgnoreCase))
        {
            WriteMetricsStatus(shell, cfg, systems);
            return;
        }

        if (args[1].Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            cfg.SetCVar(CVars.MetricsEnabled, true);
            systems.MetricsEnabled = true;
            shell.WriteLine("Prometheus metrics enabled.");
            WriteMetricsStatus(shell, cfg, systems);
            return;
        }

        if (args[1].Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            cfg.SetCVar(CVars.MetricsEnabled, false);
            systems.MetricsEnabled = false;
            shell.WriteLine("Prometheus metrics disabled.");
            return;
        }

        shell.WriteError($"Unknown metrics action '{args[1]}'.");
        shell.WriteLine("Usage: serverperf metrics on|off|status");
    }

    private static void WriteSummary(IConsoleShell shell)
    {
        var cfg = IoCManager.Resolve<IConfigurationManager>();
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var prof = IoCManager.Resolve<ProfManager>();
        var systems = IoCManager.Resolve<IEntitySystemManager>();
        var timing = IoCManager.Resolve<IGameTiming>();

        using var process = Process.GetCurrentProcess();
        ThreadPool.GetAvailableThreads(out var workerAvailable, out var ioAvailable);
        ThreadPool.GetMaxThreads(out var workerMax, out var ioMax);

        shell.WriteLine($"Tick: {timing.CurTick.Value:N0} | TickRate: {timing.TickRate:N0}/s | Target: {timing.TickPeriod.TotalMilliseconds:N3} ms");
        shell.WriteLine($"Frame: real={timing.RealFrameTime.TotalMilliseconds:N3} ms avg={timing.RealFrameTimeAvg.TotalMilliseconds:N3} ms sd={timing.RealFrameTimeStdDev.TotalMilliseconds:N3} ms fpsAvg={timing.FramesPerSecondAvg:N2}");
        shell.WriteLine($"Entities: {entityManager.EntityCount:N0} | Systems: {systems.GetEntitySystemTypes().Count():N0} | Paused: {timing.Paused} | Profiler: {prof.IsEnabled} | Metrics: {cfg.GetCVar(CVars.MetricsEnabled)}");
        shell.WriteLine($"Memory: managed={FormatBytes(GC.GetTotalMemory(false))} working={FormatBytes(process.WorkingSet64)} private={FormatBytes(process.PrivateMemorySize64)}");
        shell.WriteLine($"GC collections: gen0={GC.CollectionCount(0):N0} gen1={GC.CollectionCount(1):N0} gen2={GC.CollectionCount(2):N0}");
        shell.WriteLine($"ThreadPool: worker busy={workerMax - workerAvailable:N0}/{workerMax:N0} io busy={ioMax - ioAvailable:N0}/{ioMax:N0}");
    }

    private static void WriteMetricsStatus(IConsoleShell shell, IConfigurationManager cfg, IEntitySystemManager systems)
    {
        shell.WriteLine($"metrics.enabled={cfg.GetCVar(CVars.MetricsEnabled)} | entity-system metrics={systems.MetricsEnabled}");
        shell.WriteLine($"Endpoint: http://{cfg.GetCVar(CVars.MetricsHost)}:{cfg.GetCVar(CVars.MetricsPort)}/metrics");
    }

    private static void WriteSnapshotDeltaReport(IConsoleShell shell, SnapshotState snapshot, int top, string? filter)
    {
        shell.WriteLine("== Delta Since Previous Snapshot ==");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        if (LastSnapshot == null)
        {
            shell.WriteLine("No previous snapshot in memory. This snapshot is now the comparison baseline.");
            return;
        }

        var previous = LastSnapshot;
        var elapsedTicks = snapshot.Tick.Value >= previous.Tick.Value
            ? snapshot.Tick.Value - previous.Tick.Value
            : 0;

        shell.WriteLine($"Previous tick={previous.Tick.Value:N0} current tick={snapshot.Tick.Value:N0} elapsedTicks={elapsedTicks:N0}");
        shell.WriteLine($"Entities {previous.EntityCount:N0} -> {snapshot.EntityCount:N0} ({FormatSigned(snapshot.EntityCount - previous.EntityCount)}) | " +
                        $"Components {previous.TotalComponents:N0} -> {snapshot.TotalComponents:N0} ({FormatSigned(snapshot.TotalComponents - previous.TotalComponents)})");

        WriteCounterDeltaRows(shell, "Prototype count deltas", previous.PrototypeCounts, snapshot.PrototypeCounts, top);
        WriteCounterDeltaRows(shell, "Component count deltas", previous.ComponentCounts, snapshot.ComponentCounts, top);
        WriteMapDeltaRows(shell, previous, snapshot, top);
        WriteEntityChurnRows(shell, "New entities since previous snapshot", GetNewEntities(previous, snapshot), top);
        WriteEntityChurnRows(shell, "Removed entities since previous snapshot", GetRemovedEntities(previous, snapshot), top);
        WriteEntityChurnRows(shell, "Modified entities since previous snapshot", snapshot.Entities.Where(e => e.LastModifiedTick.Value > previous.Tick.Value), top);
    }

    private static void WriteRecentCreationReport(IConsoleShell shell, SnapshotState snapshot, int top, string? filter)
    {
        shell.WriteLine($"== Recent Creations (dirty window {snapshot.DirtyTicks:N0} ticks) ==");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        WriteEntityChurnRows(shell, "Created entities by prototype/map", snapshot.Entities.Where(e => IsRecent(e.CreationTick, snapshot.MinRecentTick)), top);

        shell.WriteLine("Created components by type:");
        var components = snapshot.Components
            .Where(s => s.Created > 0)
            .OrderByDescending(s => s.Created)
            .ThenByDescending(s => s.Count)
            .Take(top)
            .ToArray();

        WriteComponentRows(shell, components);
    }

    private static void WriteComponentReport(IConsoleShell shell, SnapshotState snapshot, int top, string? filter)
    {
        WriteComponentReport(shell, snapshot.Components, snapshot.DirtyTicks, top, filter);
    }

    private static void WriteComponentReport(IConsoleShell shell, List<ComponentStats> stats, int dirtyTicks, int top, string? filter)
    {
        shell.WriteLine($"== Components (top {top:N0}, dirty window {dirtyTicks:N0} ticks) ==");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        var byCount = stats
            .OrderByDescending(s => s.Count)
            .ThenByDescending(s => s.Dirty)
            .Take(top)
            .ToArray();

        shell.WriteLine("By count:");
        WriteComponentRows(shell, byCount);

        var byDirty = stats
            .Where(s => s.Dirty > 0 || s.Created > 0)
            .OrderByDescending(s => s.Dirty)
            .ThenByDescending(s => s.Created)
            .ThenByDescending(s => s.Count)
            .Take(top)
            .ToArray();

        shell.WriteLine("Recently dirty/created:");
        WriteComponentRows(shell, byDirty);
    }

    private static void WriteComponentRows(IConsoleShell shell, IReadOnlyList<ComponentStats> rows)
    {
        if (rows.Count == 0)
        {
            shell.WriteLine("  none");
            return;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. {row.Name,-42} count={row.Count,7:N0} dirty={row.Dirty,6:N0} created={row.Created,6:N0} net={YesNo(row.Networked),3}");
        }
    }

    private static void WriteEntityReport(IConsoleShell shell, SnapshotState snapshot, int top, string? filter)
    {
        WriteEntityReport(shell, snapshot.Entities, snapshot.DirtyTicks, top, filter);
    }

    private static void WriteEntityReport(IConsoleShell shell, List<EntityStats> stats, int dirtyTicks, int top, string? filter)
    {
        var rows = stats
            .OrderByDescending(s => s.DirtyComponents)
            .ThenByDescending(s => s.ComponentCount)
            .ThenByDescending(s => s.LastModifiedTick.Value)
            .Take(top)
            .ToArray();

        shell.WriteLine($"== Entities (top {top:N0}, dirty window {dirtyTicks:N0} ticks) ==");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        if (rows.Length == 0)
        {
            shell.WriteLine("No matching entities.");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. {row.Uid,-10} comps={row.ComponentCount,3:N0} dirty={row.DirtyComponents,3:N0} net={row.NetworkedComponents,3:N0} tick={row.LastModifiedTick.Value,10:N0} created={row.CreationTick.Value,10:N0} map={row.MapId} grid={row.GridUid} proto={row.Prototype} name=\"{Truncate(row.Name, 48)}\"");
        }
    }

    private static void WriteEntityDetailReport(IConsoleShell shell, SnapshotState snapshot, int top, string? filter)
    {
        var rows = snapshot.Entities
            .Where(s => s.DirtyComponents > 0 || s.CreatedComponentNames.Length > 0 || IsRecent(s.CreationTick, snapshot.MinRecentTick))
            .OrderByDescending(s => s.DirtyComponents)
            .ThenByDescending(s => s.CreatedComponentNames.Length)
            .ThenByDescending(s => s.ComponentCount)
            .ThenByDescending(s => s.LastModifiedTick.Value)
            .Take(top)
            .ToArray();

        shell.WriteLine($"== Dirty Entity Details (top {top:N0}) ==");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        if (rows.Length == 0)
        {
            shell.WriteLine("No recently dirty or created entities.");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            var entityCreated = IsRecent(row.CreationTick, snapshot.MinRecentTick);
            shell.WriteLine($"{i + 1,3}. {row.Uid,-10} proto={row.Prototype} map={row.MapId} grid={row.GridUid} createdEntity={YesNo(entityCreated)} name=\"{Truncate(row.Name, 40)}\"");
            shell.WriteLine($"     dirty=[{JoinNames(row.DirtyComponentNames, 12)}]");
            shell.WriteLine($"     created=[{JoinNames(row.CreatedComponentNames, 12)}]");
            shell.WriteLine($"     comps=[{JoinNames(row.ComponentNames, 18)}]");
        }
    }

    private static void WritePrototypeReport(IConsoleShell shell, SnapshotState snapshot, int top, string? filter)
    {
        WritePrototypeReport(shell, snapshot.Prototypes, top, filter);
    }

    private static void WritePrototypeReport(IConsoleShell shell, List<PrototypeStats> stats, int top, string? filter)
    {
        var rows = stats
            .OrderByDescending(s => s.Count)
            .ThenByDescending(s => s.DirtyEntities)
            .ThenBy(s => s.Prototype)
            .Take(top)
            .ToArray();

        shell.WriteLine($"== Prototypes (top {top:N0}) ==");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        if (rows.Length == 0)
        {
            shell.WriteLine("No matching prototypes.");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. {row.Prototype,-56} count={row.Count,7:N0} dirtyEntities={row.DirtyEntities,6:N0} createdEntities={row.CreatedEntities,6:N0}");
        }
    }

    private static void WriteMapReport(IConsoleShell shell, SnapshotState snapshot, int top)
    {
        WriteMapReport(shell, snapshot.Maps, top);
    }

    private static void WriteMapReport(IConsoleShell shell, List<MapStats> stats, int top)
    {
        var rows = stats
            .OrderByDescending(s => s.Entities)
            .ThenByDescending(s => s.Grids)
            .Take(top)
            .ToArray();

        shell.WriteLine($"== Maps (top {top:N0}) ==");
        if (rows.Length == 0)
        {
            shell.WriteLine("No maps with entities.");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. map={row.MapId,-8} entities={row.Entities,7:N0} dirty={row.DirtyEntities,6:N0} created={row.CreatedEntities,6:N0} grids={row.Grids,5:N0} inNullspace={row.NullspaceEntities,6:N0}");
        }
    }

    private static void WriteProfileTop(
        IConsoleShell shell,
        ProfileMode mode,
        int frames,
        int top,
        string? filter,
        bool includeDisabledHint)
    {
        var prof = IoCManager.Resolve<ProfManager>();

        shell.WriteLine($"== Profiler ({mode.ToString().ToLowerInvariant()}, last {frames:N0} frames, top {top:N0}) ==");
        if (!prof.IsEnabled && includeDisabledHint)
            shell.WriteLine("Profiler is disabled. Run 'serverperf profile on', wait during lag, then run this again.");

        if (!TryCollectProfileStats(mode, frames, filter, out var stats, out var frameCount, out var invalidFrames))
        {
            shell.WriteLine("No profiler frame data available yet.");
            return;
        }

        shell.WriteLine($"Frames read: {frameCount:N0} | Invalid/overwritten frames skipped: {invalidFrames:N0}");
        if (filter != null)
            shell.WriteLine($"Filter: {filter}");

        var rows = stats.Values
            .OrderByDescending(s => s.TotalMs)
            .ThenByDescending(s => s.MaxMs)
            .Take(top)
            .ToArray();

        if (rows.Length == 0)
        {
            shell.WriteLine("No matching profiler groups.");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. {row.Name,-52} total={row.TotalMs,9:N3}ms avg={row.AverageMs,8:N3}ms max={row.MaxMs,8:N3}ms calls={row.Count,5:N0} alloc={FormatBytes(row.TotalAlloc),9}");
        }
    }

    private static SnapshotState CollectSnapshot(int dirtyTicks, string? filter)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var timing = IoCManager.Resolve<IGameTiming>();
        var components = CollectComponentStats(dirtyTicks, filter);
        var entities = CollectEntityStats(dirtyTicks, filter);
        var prototypes = CollectPrototypeStats(dirtyTicks, filter);
        var maps = CollectMapStats(dirtyTicks);

        return new SnapshotState(
            timing.CurTick,
            dirtyTicks,
            MinRecentTick(timing, dirtyTicks),
            entityManager.EntityCount,
            components,
            entities,
            prototypes,
            maps);
    }

    private static void WriteCounterDeltaRows(
        IConsoleShell shell,
        string title,
        IReadOnlyDictionary<string, int> previous,
        IReadOnlyDictionary<string, int> current,
        int top)
    {
        shell.WriteLine($"{title}:");

        var rows = previous.Keys
            .Concat(current.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(key =>
            {
                previous.TryGetValue(key, out var oldValue);
                current.TryGetValue(key, out var newValue);
                return new CounterDeltaRow(key, oldValue, newValue, newValue - oldValue);
            })
            .Where(row => row.Delta != 0)
            .OrderByDescending(row => Math.Abs(row.Delta))
            .ThenBy(row => row.Name)
            .Take(top)
            .ToArray();

        if (rows.Length == 0)
        {
            shell.WriteLine("  no count changes");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. {row.Name,-56} {row.Previous,7:N0} -> {row.Current,7:N0} ({FormatSigned(row.Delta)})");
        }
    }

    private static void WriteMapDeltaRows(IConsoleShell shell, SnapshotState previous, SnapshotState current, int top)
    {
        shell.WriteLine("Map deltas:");

        var rows = previous.MapsById.Keys
            .Concat(current.MapsById.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(mapId =>
            {
                previous.MapsById.TryGetValue(mapId, out var oldMap);
                current.MapsById.TryGetValue(mapId, out var newMap);
                var oldEntities = oldMap?.Entities ?? 0;
                var newEntities = newMap?.Entities ?? 0;
                var oldGrids = oldMap?.Grids ?? 0;
                var newGrids = newMap?.Grids ?? 0;
                var oldNullspace = oldMap?.NullspaceEntities ?? 0;
                var newNullspace = newMap?.NullspaceEntities ?? 0;

                return new MapDeltaRow(
                    mapId,
                    oldEntities,
                    newEntities,
                    newEntities - oldEntities,
                    oldGrids,
                    newGrids,
                    newGrids - oldGrids,
                    oldNullspace,
                    newNullspace,
                    newNullspace - oldNullspace);
            })
            .Where(row => row.EntityDelta != 0 || row.GridDelta != 0 || row.NullspaceDelta != 0)
            .OrderByDescending(row => Math.Abs(row.EntityDelta) + Math.Abs(row.GridDelta * 25) + Math.Abs(row.NullspaceDelta))
            .ThenBy(row => row.MapId)
            .Take(top)
            .ToArray();

        if (rows.Length == 0)
        {
            shell.WriteLine("  no map count changes");
            return;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            shell.WriteLine($"{i + 1,3}. map={row.MapId,-8} entities {row.PreviousEntities,7:N0}->{row.CurrentEntities,7:N0} ({FormatSigned(row.EntityDelta)}) grids {row.PreviousGrids,4:N0}->{row.CurrentGrids,4:N0} ({FormatSigned(row.GridDelta)}) nullspace {row.PreviousNullspace,6:N0}->{row.CurrentNullspace,6:N0} ({FormatSigned(row.NullspaceDelta)})");
        }
    }

    private static void WriteEntityChurnRows(IConsoleShell shell, string title, IEnumerable<EntityStats> entities, int top)
    {
        shell.WriteLine($"{title}:");

        var rows = entities
            .GroupBy(entity => new EntityChurnKey(entity.Prototype, entity.MapId))
            .Select(group =>
            {
                var sample = group
                    .OrderByDescending(entity => entity.LastModifiedTick.Value)
                    .First();

                return new EntityChurnRow(
                    group.Key.Prototype,
                    group.Key.MapId,
                    group.Count(),
                    sample.Uid,
                    sample.Name,
                    sample.GridUid);
            })
            .OrderByDescending(row => row.Count)
            .ThenBy(row => row.Prototype)
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
            shell.WriteLine($"{i + 1,3}. {row.Prototype,-48} count={row.Count,6:N0} map={row.MapId,-8} sample={row.SampleUid} grid={row.SampleGrid} name=\"{Truncate(row.SampleName, 40)}\"");
        }
    }

    private static IEnumerable<EntityStats> GetNewEntities(SnapshotState previous, SnapshotState current)
    {
        foreach (var (uid, entity) in current.EntitiesByUid)
        {
            if (!previous.EntitiesByUid.ContainsKey(uid))
                yield return entity;
        }
    }

    private static IEnumerable<EntityStats> GetRemovedEntities(SnapshotState previous, SnapshotState current)
    {
        foreach (var (uid, entity) in previous.EntitiesByUid)
        {
            if (!current.EntitiesByUid.ContainsKey(uid))
                yield return entity;
        }
    }

    private static List<ComponentStats> CollectComponentStats(int dirtyTicks, string? filter)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var componentFactory = IoCManager.Resolve<IComponentFactory>();
        var timing = IoCManager.Resolve<IGameTiming>();
        var minTick = MinRecentTick(timing, dirtyTicks);
        var results = new List<ComponentStats>();

        foreach (var registration in componentFactory.GetAllRegistrations().OrderBy(r => r.Name))
        {
            if (!Matches(filter, registration.Name, registration.Type.FullName ?? string.Empty))
                continue;

            var count = 0;
            var dirty = 0;
            var created = 0;

            foreach (var (_, component) in entityManager.GetAllComponents(registration.Type, includePaused: true))
            {
                if (component.Deleted)
                    continue;

                count++;

                if (IsRecent(component.LastModifiedTick, minTick))
                    dirty++;

                if (IsRecent(component.CreationTick, minTick))
                    created++;
            }

            if (count == 0 && dirty == 0 && created == 0)
                continue;

            results.Add(new ComponentStats(
                registration.Name,
                count,
                dirty,
                created,
                registration.NetID != null));
        }

        return results;
    }

    private static List<EntityStats> CollectEntityStats(int dirtyTicks, string? filter)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var timing = IoCManager.Resolve<IGameTiming>();
        var minTick = MinRecentTick(timing, dirtyTicks);
        var results = new List<EntityStats>();

        foreach (var uid in entityManager.GetEntities())
        {
            if (!entityManager.TryGetComponent(uid, out MetaDataComponent? meta))
                continue;

            var prototype = meta.EntityPrototype?.ID ?? "<none>";
            var name = meta.EntityName;
            if (!Matches(filter, uid.ToString(), prototype, name))
                continue;

            var components = entityManager.GetComponents(uid)
                .Where(component => !component.Deleted)
                .Distinct()
                .ToArray();

            var dirty = 0;
            var networked = 0;
            var componentNames = new List<string>(components.Length);
            var dirtyComponents = new List<string>();
            var createdComponents = new List<string>();

            foreach (var component in components)
            {
                var componentName = ComponentName(component);
                componentNames.Add(componentName);

                if (component.NetSyncEnabled)
                    networked++;

                if (IsRecent(component.LastModifiedTick, minTick))
                {
                    dirty++;
                    dirtyComponents.Add(componentName);
                }

                if (IsRecent(component.CreationTick, minTick))
                    createdComponents.Add(componentName);
            }

            entityManager.TryGetComponent(uid, out TransformComponent? xform);

            results.Add(new EntityStats(
                uid,
                name,
                prototype,
                components.Length,
                dirty,
                networked,
                meta.EntityLastModifiedTick,
                meta.CreationTick,
                xform?.MapID.ToString() ?? "<none>",
                xform?.GridUid?.ToString() ?? "<none>",
                componentNames.OrderBy(n => n).ToArray(),
                dirtyComponents.OrderBy(n => n).ToArray(),
                createdComponents.OrderBy(n => n).ToArray()));
        }

        return results;
    }

    private static List<PrototypeStats> CollectPrototypeStats(int dirtyTicks, string? filter)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var timing = IoCManager.Resolve<IGameTiming>();
        var minTick = MinRecentTick(timing, dirtyTicks);
        var results = new Dictionary<string, PrototypeStats>();

        foreach (var uid in entityManager.GetEntities())
        {
            if (!entityManager.TryGetComponent(uid, out MetaDataComponent? meta))
                continue;

            var prototype = meta.EntityPrototype?.ID ?? "<none>";
            if (!Matches(filter, prototype, meta.EntityName))
                continue;

            if (!results.TryGetValue(prototype, out var stats))
            {
                stats = new PrototypeStats(prototype);
                results[prototype] = stats;
            }

            stats.Count++;

            if (IsRecent(meta.EntityLastModifiedTick, minTick))
                stats.DirtyEntities++;

            if (IsRecent(meta.CreationTick, minTick))
                stats.CreatedEntities++;
        }

        return results.Values.ToList();
    }

    private static List<MapStats> CollectMapStats(int dirtyTicks)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        var timing = IoCManager.Resolve<IGameTiming>();
        var minTick = MinRecentTick(timing, dirtyTicks);
        var results = new Dictionary<string, MapStats>();
        var gridsByMap = new Dictionary<string, HashSet<EntityUid>>();

        foreach (var uid in entityManager.GetEntities())
        {
            if (!entityManager.TryGetComponent(uid, out TransformComponent? xform))
                continue;

            var mapId = xform.MapID.ToString();
            if (!results.TryGetValue(mapId, out var stats))
            {
                stats = new MapStats(mapId);
                results[mapId] = stats;
            }

            stats.Entities++;

            if (xform.MapID == Robust.Shared.Map.MapId.Nullspace)
                stats.NullspaceEntities++;

            if (xform.GridUid is { } grid)
            {
                if (!gridsByMap.TryGetValue(mapId, out var grids))
                {
                    grids = new HashSet<EntityUid>();
                    gridsByMap[mapId] = grids;
                }

                grids.Add(grid);
            }

            if (entityManager.TryGetComponent(uid, out MetaDataComponent? meta) &&
                IsRecent(meta.EntityLastModifiedTick, minTick))
            {
                stats.DirtyEntities++;
            }

            if (meta != null && IsRecent(meta.CreationTick, minTick))
                stats.CreatedEntities++;
        }

        foreach (var (map, grids) in gridsByMap)
        {
            if (results.TryGetValue(map, out var stats))
                stats.Grids = grids.Count;
        }

        return results.Values.ToList();
    }

    private static bool TryCollectProfileStats(
        ProfileMode mode,
        int frames,
        string? filter,
        out Dictionary<string, ProfileStats> stats,
        out int frameCount,
        out int invalidFrames)
    {
        var prof = IoCManager.Resolve<ProfManager>();
        var systems = IoCManager.Resolve<IEntitySystemManager>();
        stats = new Dictionary<string, ProfileStats>();
        frameCount = 0;
        invalidFrames = 0;

        var buffer = prof.Buffer.Snapshot();
        if (buffer.LogBuffer.Length == 0 || buffer.IndexBuffer.Length == 0 || buffer.IndexWriteOffset == 0)
            return false;

        var systemNames = systems.GetEntitySystemTypes().Select(type => type.Name).ToHashSet(StringComparer.Ordinal);
        var startIndex = Math.Max(0, buffer.IndexWriteOffset - buffer.IndexBuffer.LongLength);
        var validFrames = new List<ProfIndex>();
        var earliestLog = buffer.LogWriteOffset - buffer.LogBuffer.LongLength;

        for (var i = startIndex; i < buffer.IndexWriteOffset; i++)
        {
            var index = buffer.Index(i);
            if (index.Type != ProfIndexType.Frame)
                continue;

            if (index.StartPos < earliestLog || index.EndPos > buffer.LogWriteOffset || index.EndPos <= index.StartPos)
            {
                invalidFrames++;
                continue;
            }

            validFrames.Add(index);
        }

        foreach (var index in validFrames.TakeLast(frames))
        {
            frameCount++;

            for (var logIndex = index.StartPos; logIndex < index.EndPos; logIndex++)
            {
                var log = buffer.Log(logIndex);
                if (log.Type != ProfLogType.GroupEnd ||
                    log.GroupEnd.Value.Type != ProfValueType.TimeAllocSample)
                {
                    continue;
                }

                var name = prof.GetString(log.GroupEnd.StringId);
                if (!MatchesProfileMode(mode, name, systemNames) ||
                    !Matches(filter, name))
                {
                    continue;
                }

                if (!stats.TryGetValue(name, out var row))
                {
                    row = new ProfileStats(name);
                    stats[name] = row;
                }

                var sample = log.GroupEnd.Value.TimeAllocSample;
                var ms = sample.Time * 1000.0;
                row.Count++;
                row.TotalMs += ms;
                row.MaxMs = Math.Max(row.MaxMs, ms);
                row.TotalAlloc += sample.Alloc;
                row.MaxAlloc = Math.Max(row.MaxAlloc, sample.Alloc);
            }
        }

        return frameCount > 0;
    }

    private static List<Type> GetUpdateSystemOrder(IEntitySystemManager systems)
    {
        var field = systems.GetType().GetField("_updateOrder", BindingFlags.Instance | BindingFlags.NonPublic);
        var updateOrder = field?.GetValue(systems) as Array;
        if (updateOrder == null)
            return systems.GetEntitySystemTypes().OrderBy(type => type.Name).ToList();

        var result = new List<Type>(updateOrder.Length);
        foreach (var row in updateOrder)
        {
            var systemField = row.GetType().GetField("System", BindingFlags.Instance | BindingFlags.Public);
            if (systemField?.GetValue(row) is IEntitySystem system)
                result.Add(system.GetType());
        }

        return result;
    }

    private static HashSet<Type> GetFrameSystemOrder(IEntitySystemManager systems)
    {
        var field = systems.GetType().GetField("_frameUpdateOrder", BindingFlags.Instance | BindingFlags.NonPublic);
        var frameOrder = field?.GetValue(systems) as IEnumerable<IEntitySystem>;

        return frameOrder?.Select(system => system.GetType()).ToHashSet() ?? [];
    }

    private static bool MatchesProfileMode(ProfileMode mode, string name, HashSet<string> systemNames)
    {
        return mode switch
        {
            ProfileMode.Systems => systemNames.Contains(name),
            ProfileMode.Engine => !systemNames.Contains(name),
            _ => true,
        };
    }

    private static uint MinRecentTick(IGameTiming timing, int dirtyTicks)
    {
        return timing.CurTick.Value > dirtyTicks
            ? timing.CurTick.Value - (uint) dirtyTicks
            : 0;
    }

    private static bool IsRecent(GameTick tick, uint minTick)
    {
        return tick.Value != 0 && tick.Value >= minTick;
    }

    private static int ReadInt(string[] args, ref int index, int fallback, int min, int max)
    {
        if (index >= args.Length || !int.TryParse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            return fallback;

        index++;
        return Math.Clamp(value, min, max);
    }

    private static string? ReadFilter(string[] args, int index)
    {
        if (index >= args.Length)
            return null;

        var filter = string.Join(' ', args.Skip(index)).Trim();
        return filter.Length == 0 ? null : filter;
    }

    private static bool Matches(string? filter, params string[] values)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        foreach (var value in values)
        {
            if (value.Contains(filter, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string ComponentName(IComponent component)
    {
        const string suffix = "Component";
        var name = component.GetType().Name;
        return name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }

    private static string JoinNames(IReadOnlyList<string> values, int max)
    {
        if (values.Count == 0)
            return string.Empty;

        var visible = values.Take(max).ToArray();
        var joined = string.Join(", ", visible);
        var remaining = values.Count - visible.Length;
        return remaining <= 0
            ? joined
            : $"{joined}, +{remaining:N0} more";
    }

    private static string FormatSigned(int value)
    {
        return value >= 0
            ? $"+{value:N0}"
            : value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static string YesNo(bool value)
    {
        return value ? "yes" : "no";
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

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        return value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private sealed record ComponentStats(string Name, int Count, int Dirty, int Created, bool Networked);

    private sealed record EntityStats(
        EntityUid Uid,
        string Name,
        string Prototype,
        int ComponentCount,
        int DirtyComponents,
        int NetworkedComponents,
        GameTick LastModifiedTick,
        GameTick CreationTick,
        string MapId,
        string GridUid,
        string[] ComponentNames,
        string[] DirtyComponentNames,
        string[] CreatedComponentNames);

    private sealed class PrototypeStats(string prototype)
    {
        public readonly string Prototype = prototype;
        public int CreatedEntities;
        public int Count;
        public int DirtyEntities;
    }

    private sealed class MapStats(string mapId)
    {
        public readonly string MapId = mapId;
        public int CreatedEntities;
        public int DirtyEntities;
        public int Entities;
        public int Grids;
        public int NullspaceEntities;
    }

    private sealed class SnapshotState
    {
        public SnapshotState(
            GameTick tick,
            int dirtyTicks,
            uint minRecentTick,
            int entityCount,
            List<ComponentStats> components,
            List<EntityStats> entities,
            List<PrototypeStats> prototypes,
            List<MapStats> maps)
        {
            Tick = tick;
            DirtyTicks = dirtyTicks;
            MinRecentTick = minRecentTick;
            EntityCount = entityCount;
            Components = components;
            Entities = entities;
            Prototypes = prototypes;
            Maps = maps;
            ComponentCounts = components.ToDictionary(row => row.Name, row => row.Count, StringComparer.Ordinal);
            EntityCountsByMap = maps.ToDictionary(row => row.MapId, row => row.Entities, StringComparer.Ordinal);
            EntitiesByUid = entities.ToDictionary(row => row.Uid);
            MapsById = maps.ToDictionary(row => row.MapId, StringComparer.Ordinal);
            PrototypeCounts = prototypes.ToDictionary(row => row.Prototype, row => row.Count, StringComparer.Ordinal);
            TotalComponents = components.Sum(row => row.Count);
        }

        public readonly List<ComponentStats> Components;
        public readonly Dictionary<string, int> ComponentCounts;
        public readonly int DirtyTicks;
        public readonly int EntityCount;
        public readonly Dictionary<string, int> EntityCountsByMap;
        public readonly Dictionary<EntityUid, EntityStats> EntitiesByUid;
        public readonly List<EntityStats> Entities;
        public readonly List<MapStats> Maps;
        public readonly Dictionary<string, MapStats> MapsById;
        public readonly uint MinRecentTick;
        public readonly Dictionary<string, int> PrototypeCounts;
        public readonly List<PrototypeStats> Prototypes;
        public readonly GameTick Tick;
        public readonly int TotalComponents;
    }

    private readonly record struct CounterDeltaRow(string Name, int Previous, int Current, int Delta);

    private readonly record struct EntityChurnKey(string Prototype, string MapId);

    private readonly record struct EntityChurnRow(
        string Prototype,
        string MapId,
        int Count,
        EntityUid SampleUid,
        string SampleName,
        string SampleGrid);

    private readonly record struct MapDeltaRow(
        string MapId,
        int PreviousEntities,
        int CurrentEntities,
        int EntityDelta,
        int PreviousGrids,
        int CurrentGrids,
        int GridDelta,
        int PreviousNullspace,
        int CurrentNullspace,
        int NullspaceDelta);

    private sealed class ProfileStats(string name)
    {
        public readonly string Name = name;
        public int Count;
        public double TotalMs;
        public double MaxMs;
        public long TotalAlloc;
        public long MaxAlloc;

        public double AverageMs => Count == 0 ? 0 : TotalMs / Count;
    }

    private readonly record struct SystemRow(int Index, Type Type, bool HasFrameUpdate);

    private enum ProfileMode
    {
        All,
        Systems,
        Engine,
    }
}
