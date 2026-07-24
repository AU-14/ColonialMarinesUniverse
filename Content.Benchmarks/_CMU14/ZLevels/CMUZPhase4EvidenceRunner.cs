#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.IntegrationTests.Pair;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.GameTicking;
using Content.Shared._CMU14.ZLevels.Ordnance;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared._CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._CMU14.ZLevels.Vehicles;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Damage.Components;
using Content.Shared.Maps;
using Content.Shared.Warps;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Profiling;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using GameTick = Robust.Shared.Timing.GameTick;

namespace Content.Benchmarks._CMU14.ZLevels;

/// <summary>
/// Runs repeatable, headless Phase 4 evidence scenarios that are too stateful for BenchmarkDotNet.
/// </summary>
internal static class CMUZPhase4EvidenceRunner
{
    private const string ProfilerPrefix = "CMU Z ";
    private const string MovementProfileName = ProfilerPrefix + "Movement";
    private const string NetworkRecoveryHitCounterName = ProfilerPrefix + "Network Recovery Hits";
    private const string PvsViewerCounterName = ProfilerPrefix + "PVS Viewers";
    private const int ProfilerIndexSize = 512;
    private const int ProfilerBufferSize = 1048576;
    private const int OrdnanceWarmupIterations = 64;
    private const int OrdnanceCaptureIterations = 1000;
    private const int OrdnanceProfileIterations = 256;
    private const int VehicleLandingWarmupIterations = 4;
    private const int VehicleLandingCaptureIterations = 100;
    private const int VehicleLandingProfileIterations = 64;
    private const int VehicleLandingOccupants = 24;
    private const int VehicleLandingTrackedOccupants = 16;
    private const double TickDeadlineMilliseconds = 1000.0 / 30.0;
    private const long MinimumSoakGrowthAllowanceBytes = 32L * 1024 * 1024;
    private const int SoakGrowthDivisor = 20;

    private static readonly ProtoId<GameMapPrototype> MapPrototype = "USSBushRedux";
    private static readonly EntProtoId ViewerPrototype = "MobObserver";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task<int> RunAsync(string[] args)
    {
        var options = EvidenceOptions.Parse(args);
        var report = new EvidenceReport
        {
            CapturedAtUtc = DateTimeOffset.UtcNow,
            Configuration = options,
            Environment = CaptureEnvironment(),
        };

        try
        {
            Console.WriteLine(
                $"Multi-Z Phase 4 evidence: warmup={options.WarmupTicks}, capture={options.CaptureTicks}, " +
                $"players={options.Players}, PVS samples={options.PvsSamples}, soak={options.SoakTicks}");

            ProgramShared.PathOffset = GetContentPathOffset();
            PoolManager.Startup();

            var jitWarmupOptions = new EvidenceOptions
            {
                Output = options.Output,
                WarmupTicks = 30,
                CaptureTicks = 10,
                Players = 1,
                PvsSamples = 1,
                SoakTicks = 0,
                SoakCheckpointTicks = options.SoakCheckpointTicks,
                Seed = options.Seed,
            };
            await RunScenario(jitWarmupOptions, multiZ: true, "jit-warmup-multiz");
            report.JitWarmupCompleted = true;

            report.Control = await RunScenario(options, multiZ: false);
            report.MultiZ = await RunScenario(options, multiZ: true);
            report.Comparisons = BuildComparisons(report.Control, report.MultiZ);
            report.Gates = EvaluateGates(report.Control, report.MultiZ);

            var failedHardGates = report.Gates.Count(gate => gate.Hard && !gate.Passed);
            report.Success = failedHardGates == 0;
            report.Summary = failedHardGates == 0
                ? "All headless correctness gates passed."
                : $"{failedHardGates} headless correctness gate(s) failed.";

            WriteReport(options.Output, report);
            PrintSummary(report);
            return report.Success ? 0 : 2;
        }
        catch (Exception exception)
        {
            report.Success = false;
            report.Summary = "Evidence collection failed.";
            report.Error = exception.ToString();

            WriteReport(options.Output, report);
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            PoolManager.Shutdown();
        }
    }

    private static async Task<ScenarioResult> RunScenario(
        EvidenceOptions options,
        bool multiZ,
        string? nameOverride = null)
    {
        var scenarioName = nameOverride ?? (multiZ ? "bush-multiz" : "bush-single-level-control");
        Console.WriteLine($"Starting {scenarioName}...");

        var settings = new PoolSettings
        {
            Connected = false,
            Destructive = true,
            Dirty = true,
            Fresh = true,
            ServerSeed = options.Seed,
        };

        await using var pair = await PoolManager.GetServerClient(
            settings,
            new ExternalTestContext($"Phase4.{scenarioName}", TextWriter.Null));

        var server = pair.Server;
        var entityManager = server.ResolveDependency<IEntityManager>();
        var prototypeManager = server.ResolveDependency<IPrototypeManager>();
        var configuration = server.ResolveDependency<IConfigurationManager>();
        var mapSystem = entityManager.System<SharedMapSystem>();
        var transformSystem = entityManager.System<SharedTransformSystem>();
        var zLevels = entityManager.System<CMUZLevelsSystem>();
        var ticker = entityManager.System<GameTicker>();
        var profiler = server.ResolveDependency<ProfManager>();
        var serializer = server.ResolveDependency<IRobustSerializer>();

        var result = new ScenarioResult
        {
            Name = scenarioName,
            MultiZ = multiZ,
            Pvs = new PvsCapture
            {
                RequestedViewers = options.Players,
            },
        };

        EntityUid baseMap = default;
        MapId baseMapId = default;
        var loadedMapIds = new List<MapId>();
        var mapCountBefore = 0;
        var networkCountBefore = 0;
        long loadProfilerStart = 0;

        await server.WaitPost(() =>
        {
            configuration.SetCVar(CVars.NetPVS, true);
            configuration.SetCVar(CVars.ThreadParallelCount, 0);
            configuration.SetCVar(CVars.NetPvsAsync, false);
            configuration.SetCVar(CVars.ProfIndexSize, ProfilerIndexSize);
            configuration.SetCVar(CVars.ProfBufferSize, ProfilerBufferSize);
            configuration.SetCVar(CVars.ProfEnabled, true);
            loadProfilerStart = profiler.Buffer.LogWriteOffset;

            mapCountBefore = entityManager.Count<MapComponent>();
            networkCountBefore = entityManager.Count<CMUZLevelsNetworkComponent>();
            var entityCountBefore = entityManager.EntityCount;
            var managedBefore = GC.GetTotalMemory(true);
            var gen0Before = GC.CollectionCount(0);
            var gen1Before = GC.CollectionCount(1);
            var gen2Before = GC.CollectionCount(2);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();

            var source = prototypeManager.Index<GameMapPrototype>(MapPrototype);
            var gameMap = multiZ
                ? source
                : source.Persistence(source.MapPath);
            var loadOptions = DeserializationOptions.Default with { InitializeMaps = true };
            ticker.LoadGameMap(gameMap, out baseMapId, loadOptions);

            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            baseMap = mapSystem.GetMap(baseMapId);

            result.Load = new LoadResult
            {
                WallMilliseconds = elapsed.TotalMilliseconds,
                ThreadAllocatedBytes = allocated,
                ManagedBytesBefore = managedBefore,
                ManagedBytesAfter = GC.GetTotalMemory(false),
                Gen0Collections = GC.CollectionCount(0) - gen0Before,
                Gen1Collections = GC.CollectionCount(1) - gen1Before,
                Gen2Collections = GC.CollectionCount(2) - gen2Before,
                EntityCountBefore = entityCountBefore,
                EntityCountAfter = entityManager.EntityCount,
                MapCountBefore = mapCountBefore,
                MapCountAfter = entityManager.Count<MapComponent>(),
                NetworkCountBefore = networkCountBefore,
                NetworkCountAfter = entityManager.Count<CMUZLevelsNetworkComponent>(),
            };

            if (zLevels.TryGetZNetwork(baseMap, out var network))
            {
                result.Topology.HasNetwork = true;
                result.Topology.NetworkEntity = network.Value.Owner.ToString();
                if (zLevels.TryGetDepthBounds(network.Value, out var minDepth, out var maxDepth))
                {
                    result.Topology.MinimumDepth = minDepth;
                    result.Topology.MaximumDepth = maxDepth;
                }

                foreach (var (depth, mapUid) in network.Value.Comp.ZLevels.OrderBy(entry => entry.Key))
                {
                    if (mapUid is not { } level ||
                        !entityManager.TryGetComponent<MapComponent>(level, out var map))
                    {
                        continue;
                    }

                    result.Topology.Depths.Add(depth);
                    loadedMapIds.Add(map.MapId);
                }
            }
            else
            {
                loadedMapIds.Add(baseMapId);
            }

            result.Topology.LoadedMapCount = loadedMapIds.Count;
        });
        await server.WaitRunTicks(2);
        await server.WaitPost(() =>
        {
            result.Load.Profile = CaptureProfiler(profiler, loadProfilerStart);
            configuration.SetCVar(CVars.ProfEnabled, false);
            result.Replication.TopologyNetwork =
                CaptureComponentReplication<CMUZLevelsNetworkComponent>(entityManager, serializer);
            result.Replication.TopologyMaps =
                CaptureComponentReplication<CMUZLevelMapComponent>(entityManager, serializer);
            result.Replication.ZPhysics =
                CaptureComponentReplication<CMUZPhysicsComponent>(entityManager, serializer);
            result.Replication.Falling =
                CaptureComponentReplication<CMUZFallingComponent>(entityManager, serializer);
            result.Replication.VehicleTraversal =
                CaptureComponentReplication<CMUVehicleZTraversalComponent>(entityManager, serializer);
            result.Replication.RepresentativeVehicleTraversal =
                CaptureRepresentativeComponentReplication<CMUVehicleZTraversalComponent>(entityManager, serializer);
            result.Replication.TopologyPayloadBytes =
                result.Replication.TopologyNetwork.PayloadBytes +
                result.Replication.TopologyMaps.PayloadBytes;
        });

        if (multiZ)
        {
            await server.WaitPost(() =>
            {
                result.GameplayBursts = CaptureGameplayBursts(
                    entityManager,
                    configuration,
                    mapSystem,
                    zLevels,
                    profiler,
                    baseMap);
            });
        }

        var sessions = await server.AddDummySessions(options.Players);
        EntityCoordinates[] spawnCoordinates = [];

        await server.WaitPost(() =>
        {
            var coordinates = new List<EntityCoordinates>();
            var query = entityManager.EntityQueryEnumerator<WarpPointComponent, TransformComponent>();
            while (query.MoveNext(out _, out _, out var transform))
            {
                if (transform.MapUid == baseMap)
                    coordinates.Add(transform.Coordinates);
            }

            spawnCoordinates = coordinates.ToArray();

            if (spawnCoordinates.Length == 0)
                spawnCoordinates = [new EntityCoordinates(baseMap, Vector2.Zero)];

            for (var i = 0; i < sessions.Length; i++)
            {
                var player = entityManager.SpawnEntity(
                    ViewerPrototype,
                    spawnCoordinates[i % spawnCoordinates.Length]);
                if (!pair.Server.PlayerMan.SetAttachedEntity(sessions[i], player))
                    throw new InvalidOperationException($"Failed to attach synthetic viewer {sessions[i].Name}.");

                zLevels.EnsureZLevelViewer(player);
            }
        });

        await server.WaitRunTicks(options.WarmupTicks);
        result.Pvs.WarmupReattachments += await EnsureSessionAttachments(
            sessions,
            spawnCoordinates,
            loadedMapIds,
            entityManager,
            pair);
        await server.WaitRunTicks(2);
        result.Pvs.WarmupReattachments += await EnsureSessionAttachments(
            sessions,
            spawnCoordinates,
            loadedMapIds,
            entityManager,
            pair);
        result.Pvs.AttachedViewersAtProfileStart = await CountAttachedViewers(
            sessions,
            loadedMapIds,
            entityManager,
            pair);
        pair.Server.PvsTick(sessions);
        pair.Server.PvsTick(sessions);

        long profilerStart = 0;
        await server.WaitPost(() =>
        {
            configuration.SetCVar(CVars.ProfIndexSize, ProfilerIndexSize);
            configuration.SetCVar(CVars.ProfBufferSize, ProfilerBufferSize);
            configuration.SetCVar(CVars.ProfEnabled, true);
            profilerStart = profiler.Buffer.LogWriteOffset;
            result.Capture.ManagedBytesBefore = GC.GetTotalMemory(false);
            result.Capture.Gen0CollectionsBefore = GC.CollectionCount(0);
            result.Capture.Gen1CollectionsBefore = GC.CollectionCount(1);
            result.Capture.Gen2CollectionsBefore = GC.CollectionCount(2);
        });

        result.Capture.ProfileTicks = options.CaptureTicks;
        await server.WaitRunTicks(result.Capture.ProfileTicks);

        await server.WaitPost(() =>
        {
            result.Capture.Profile = CaptureProfiler(profiler, profilerStart);
            configuration.SetCVar(CVars.ProfEnabled, false);
        });

        var tickSamples = new List<double>(options.CaptureTicks);
        for (var i = 0; i < options.CaptureTicks; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await server.WaitRunTicks(1);
            tickSamples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }

        result.Capture.TickTurnaround = new OperationCapture
        {
            Milliseconds = Distribution.From(tickSamples),
        };
        await server.WaitPost(() =>
        {
            result.Capture.ManagedBytesAfter = GC.GetTotalMemory(false);
            result.Capture.Gen0CollectionsAfter = GC.CollectionCount(0);
            result.Capture.Gen1CollectionsAfter = GC.CollectionCount(1);
            result.Capture.Gen2CollectionsAfter = GC.CollectionCount(2);
            result.Capture.EntityCount = entityManager.EntityCount;
            result.Capture.MapCount = entityManager.Count<MapComponent>();
            result.Capture.NetworkCount = entityManager.Count<CMUZLevelsNetworkComponent>();
        });

        result.Pvs.CaptureReattachments += await EnsureSessionAttachments(
            sessions,
            spawnCoordinates,
            loadedMapIds,
            entityManager,
            pair);
        result.Pvs.AttachedViewersBeforePvs = await CountAttachedViewers(
            sessions,
            loadedMapIds,
            entityManager,
            pair);
        result.Pvs.Static = MeasurePvs(options.PvsSamples, sessions, pair);
        var cyclingPvs = await MeasureCyclingPvs(
            options.PvsSamples,
            sessions,
            spawnCoordinates,
            transformSystem,
            entityManager,
            pair);
        result.Pvs.Cycling = cyclingPvs.Capture;
        result.Pvs.CyclingReattachments = cyclingPvs.Reattachments;

        if (multiZ && options.SoakTicks > 0)
        {
            await server.WaitPost(() =>
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                result.Soak.ManagedBytesAtStart = GC.GetTotalMemory(false);
                result.Soak.EntityCountAtStart = entityManager.EntityCount;
                result.Soak.MapCountAtStart = entityManager.Count<MapComponent>();
                result.Soak.NetworkCountAtStart = entityManager.Count<CMUZLevelsNetworkComponent>();
            });

            var remaining = options.SoakTicks;
            var completed = 0;
            while (remaining > 0)
            {
                var count = Math.Min(remaining, options.SoakCheckpointTicks);
                await server.WaitRunTicks(count);
                completed += count;
                remaining -= count;

                await server.WaitPost(() =>
                {
                    result.Soak.Checkpoints.Add(new SoakCheckpoint
                    {
                        Tick = completed,
                        ManagedBytes = GC.GetTotalMemory(false),
                        EntityCount = entityManager.EntityCount,
                        MapCount = entityManager.Count<MapComponent>(),
                        NetworkCount = entityManager.Count<CMUZLevelsNetworkComponent>(),
                    });
                });
            }

            await server.WaitPost(() =>
            {
                GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
                result.Soak.ManagedBytesAtEnd = GC.GetTotalMemory(false);
                result.Soak.EntityCountAtEnd = entityManager.EntityCount;
                result.Soak.MapCountAtEnd = entityManager.Count<MapComponent>();
                result.Soak.NetworkCountAtEnd = entityManager.Count<CMUZLevelsNetworkComponent>();
            });
        }

        long teardownProfilerStart = 0;
        await server.WaitPost(() =>
        {
            configuration.SetCVar(CVars.ProfEnabled, true);
            teardownProfilerStart = profiler.Buffer.LogWriteOffset;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            foreach (var mapId in loadedMapIds)
            {
                if (mapSystem.MapExists(mapId))
                    mapSystem.DeleteMap(mapId);
            }

            result.Teardown.DeleteWallMilliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            result.Teardown.DeleteThreadAllocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        });
        await server.WaitRunTicks(2);
        await server.WaitPost(() =>
        {
            result.Teardown.Profile = CaptureProfiler(profiler, teardownProfilerStart);
            configuration.SetCVar(CVars.ProfEnabled, false);
            result.Teardown.MapCountBefore = mapCountBefore;
            result.Teardown.MapCountAfter = entityManager.Count<MapComponent>();
            result.Teardown.NetworkCountBefore = networkCountBefore;
            result.Teardown.NetworkCountAfter = entityManager.Count<CMUZLevelsNetworkComponent>();
        });

        Console.WriteLine(
            $"Completed {scenarioName}: load={result.Load.WallMilliseconds:F1} ms, " +
            $"tick turnaround p95={result.Capture.TickTurnaround.Milliseconds.P95:F3} ms, " +
            $"static PVS p95={result.Pvs.Static.Milliseconds.P95:F3} ms.");
        return result;
    }

    private static OperationCapture MeasurePvs(
        int samples,
        ICommonSession[] sessions,
        TestPair pair)
    {
        var measurements = new List<OperationSample>(samples);
        for (var i = 0; i < samples; i++)
        {
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            pair.Server.PvsTick(sessions);
            measurements.Add(new OperationSample(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore));
        }

        return SummarizeOperations(measurements);
    }

    private static async Task<int> EnsureSessionAttachments(
        ICommonSession[] sessions,
        EntityCoordinates[] locations,
        IReadOnlyCollection<MapId> scenarioMaps,
        IEntityManager entityManager,
        TestPair pair)
    {
        var reattachments = 0;
        await pair.Server.WaitPost(() =>
        {
            var zLevels = entityManager.System<CMUZLevelsSystem>();
            for (var i = 0; i < sessions.Length; i++)
            {
                if (sessions[i].AttachedEntity is { } attached &&
                    entityManager.TryGetComponent<TransformComponent>(attached, out var transform) &&
                    scenarioMaps.Contains(transform.MapID))
                {
                    zLevels.EnsureZLevelViewer(attached);
                    continue;
                }

                var location = locations[i % locations.Length];
                var player = entityManager.SpawnEntity(ViewerPrototype, location);
                if (!pair.Server.PlayerMan.SetAttachedEntity(sessions[i], player))
                    throw new InvalidOperationException($"Failed to reattach synthetic viewer {sessions[i].Name}.");

                zLevels.EnsureZLevelViewer(player);
                reattachments++;
            }
        });

        return reattachments;
    }

    private static async Task<int> CountAttachedViewers(
        ICommonSession[] sessions,
        IReadOnlyCollection<MapId> scenarioMaps,
        IEntityManager entityManager,
        TestPair pair)
    {
        var attachedViewers = 0;
        await pair.Server.WaitPost(() =>
        {
            foreach (var session in sessions)
            {
                if (session.AttachedEntity is { } attached &&
                    entityManager.TryGetComponent<TransformComponent>(attached, out var transform) &&
                    scenarioMaps.Contains(transform.MapID) &&
                    entityManager.HasComponent<CMUZLevelViewerComponent>(attached))
                {
                    attachedViewers++;
                }
            }
        });

        return attachedViewers;
    }

    private static async Task<CyclingPvsCapture> MeasureCyclingPvs(
        int samples,
        ICommonSession[] sessions,
        EntityCoordinates[] locations,
        SharedTransformSystem transform,
        IEntityManager entityManager,
        TestPair pair)
    {
        var measurements = new List<OperationSample>(samples);
        var reattachments = 0;
        var zLevels = entityManager.System<CMUZLevelsSystem>();
        for (var sample = 0; sample < samples; sample++)
        {
            var offset = (sample + 1) % locations.Length;
            await pair.Server.WaitPost(() =>
            {
                for (var i = 0; i < sessions.Length; i++)
                {
                    var location = locations[(i + offset) % locations.Length];
                    var player = sessions[i].AttachedEntity;
                    if (player is not { } attached ||
                        !entityManager.HasComponent<TransformComponent>(attached))
                    {
                        attached = entityManager.SpawnEntity(ViewerPrototype, location);
                        if (!pair.Server.PlayerMan.SetAttachedEntity(sessions[i], attached))
                        {
                            throw new InvalidOperationException(
                                $"Failed to reattach synthetic viewer {sessions[i].Name}.");
                        }

                        zLevels.EnsureZLevelViewer(attached);
                        reattachments++;
                    }

                    transform.SetCoordinates(attached, location);
                }
            });

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            pair.Server.PvsTick(sessions);
            measurements.Add(new OperationSample(
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                GC.GetAllocatedBytesForCurrentThread() - allocatedBefore));
        }

        return new CyclingPvsCapture(SummarizeOperations(measurements), reattachments);
    }

    private static OperationCapture SummarizeOperations(List<OperationSample> samples)
    {
        return new OperationCapture
        {
            Milliseconds = Distribution.From(samples.Select(sample => sample.Milliseconds)),
            ThreadAllocatedBytes = Distribution.From(samples.Select(sample => (double) sample.ThreadAllocatedBytes)),
            AllocationScope = "calling thread",
        };
    }

    private static ProfileCapture CaptureProfiler(ProfManager profiler, long requestedStart)
    {
        var buffer = profiler.Buffer.Snapshot();
        var timings = new Dictionary<string, List<TimeAndAllocSample>>(StringComparer.Ordinal);
        var counters = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        var start = Math.Max(requestedStart, buffer.LogWriteOffset - buffer.LogBuffer.LongLength);

        for (var index = start; index < buffer.LogWriteOffset; index++)
        {
            ref var log = ref buffer.Log(index);
            switch (log.Type)
            {
                case ProfLogType.GroupEnd:
                {
                    var name = profiler.GetString(log.GroupEnd.StringId);
                    if (log.GroupEnd.Value.Type != ProfValueType.TimeAllocSample ||
                        !name.StartsWith(ProfilerPrefix, StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (!timings.TryGetValue(name, out var values))
                    {
                        values = [];
                        timings.Add(name, values);
                    }

                    values.Add(log.GroupEnd.Value.TimeAllocSample);
                    break;
                }
                case ProfLogType.Value:
                {
                    var name = profiler.GetString(log.Value.StringId);
                    if (!name.StartsWith(ProfilerPrefix, StringComparison.Ordinal))
                        break;

                    double value;
                    switch (log.Value.Value.Type)
                    {
                        case ProfValueType.Int32:
                            value = log.Value.Value.Int32;
                            break;
                        case ProfValueType.Int64:
                            value = log.Value.Value.Int64;
                            break;
                        default:
                            continue;
                    }

                    if (!counters.TryGetValue(name, out var values))
                    {
                        values = [];
                        counters.Add(name, values);
                    }

                    values.Add(value);
                    break;
                }
            }
        }

        return new ProfileCapture
        {
            TotalLogEntriesWritten = buffer.LogWriteOffset - requestedStart,
            LogEntries = buffer.LogWriteOffset - start,
            WasTruncated = start != requestedStart,
            Timings = timings.ToDictionary(
                entry => entry.Key,
                entry => new TimingCapture
                {
                    Milliseconds = Distribution.From(
                        entry.Value.Select(sample => sample.Time * 1000.0)),
                    AllocatedBytes = Distribution.From(
                        entry.Value.Select(sample => (double) sample.Alloc)),
                },
                StringComparer.Ordinal),
            Counters = counters.ToDictionary(
                entry => entry.Key,
                entry => Distribution.From(entry.Value),
                StringComparer.Ordinal),
        };
    }

    private static List<ComparisonResult> BuildComparisons(ScenarioResult control, ScenarioResult multiZ)
    {
        var comparisons = new List<ComparisonResult>
        {
            Compare("Map load wall time (ms)", control.Load.WallMilliseconds, multiZ.Load.WallMilliseconds),
            Compare(
                "Map load thread allocation (bytes)",
                control.Load.ThreadAllocatedBytes,
                multiZ.Load.ThreadAllocatedBytes),
            Compare(
                "Static PVS p50 (ms)",
                control.Pvs.Static.Milliseconds.P50,
                multiZ.Pvs.Static.Milliseconds.P50),
            Compare(
                "Static PVS allocation p50 (bytes)",
                control.Pvs.Static.ThreadAllocatedBytes!.P50,
                multiZ.Pvs.Static.ThreadAllocatedBytes!.P50),
            Compare(
                "Cycling PVS p50 (ms)",
                control.Pvs.Cycling.Milliseconds.P50,
                multiZ.Pvs.Cycling.Milliseconds.P50),
        };

        comparisons.Add(Compare(
            "Server tick turnaround p50 (ms)",
            control.Capture.TickTurnaround.Milliseconds.P50,
            multiZ.Capture.TickTurnaround.Milliseconds.P50));
        comparisons.Add(Compare(
            "Server tick turnaround p95 (ms)",
            control.Capture.TickTurnaround.Milliseconds.P95,
            multiZ.Capture.TickTurnaround.Milliseconds.P95));

        return comparisons;
    }

    private static ComparisonResult Compare(string name, double control, double multiZ)
    {
        return new ComparisonResult
        {
            Name = name,
            Control = control,
            MultiZ = multiZ,
            Delta = multiZ - control,
            Ratio = control == 0 ? null : multiZ / control,
        };
    }

    private static List<GateResult> EvaluateGates(ScenarioResult control, ScenarioResult multiZ)
    {
        var gates = new List<GateResult>
        {
            new()
            {
                Name = "Single-level control has exactly one loaded map and no Z-network",
                Hard = true,
                Passed = control.Topology.LoadedMapCount == 1 && !control.Topology.HasNetwork,
                Details =
                    $"maps={control.Topology.LoadedMapCount}, hasNetwork={control.Topology.HasNetwork}",
            },
            new()
            {
                Name = "USS Bush Redux loads all five declared Z-levels",
                Hard = true,
                Passed = multiZ.Topology.LoadedMapCount == 5 &&
                         multiZ.Topology.Depths.SequenceEqual([-1, 0, 1, 2, 3]),
                Details =
                    $"maps={multiZ.Topology.LoadedMapCount}, depths=[{string.Join(", ", multiZ.Topology.Depths)}]",
            },
            new()
            {
                Name = "USS Bush Redux depth bounds are -1 through 3",
                Hard = true,
                Passed = multiZ.Topology.MinimumDepth == -1 && multiZ.Topology.MaximumDepth == 3,
                Details = $"bounds={multiZ.Topology.MinimumDepth}..{multiZ.Topology.MaximumDepth}",
            },
            new()
            {
                Name = "Scenario teardown restores map and network counts",
                Hard = true,
                Passed = control.Teardown.MapCountAfter == control.Teardown.MapCountBefore &&
                         control.Teardown.NetworkCountAfter == control.Teardown.NetworkCountBefore &&
                         multiZ.Teardown.MapCountAfter == multiZ.Teardown.MapCountBefore &&
                         multiZ.Teardown.NetworkCountAfter == multiZ.Teardown.NetworkCountBefore,
                Details =
                    $"control maps {control.Teardown.MapCountBefore}->{control.Teardown.MapCountAfter}, " +
                    $"networks {control.Teardown.NetworkCountBefore}->{control.Teardown.NetworkCountAfter}; " +
                    $"multi maps {multiZ.Teardown.MapCountBefore}->{multiZ.Teardown.MapCountAfter}, " +
                    $"networks {multiZ.Teardown.NetworkCountBefore}->{multiZ.Teardown.NetworkCountAfter}",
            },
        };

        gates.Add(new GateResult
        {
            Name = "Harness tick turnaround p99 remains inside the 30 TPS deadline",
            Hard = false,
            Passed = multiZ.Capture.TickTurnaround.Milliseconds.P99 < TickDeadlineMilliseconds,
            Details =
                $"p99={multiZ.Capture.TickTurnaround.Milliseconds.P99:F3} ms, " +
                $"deadline={TickDeadlineMilliseconds:F3} ms; includes harness signaling",
        });
        var hasControlMovementProfile = control.Capture.Profile.Timings.TryGetValue(
            MovementProfileName,
            out var controlMovementProfile);
        var hasMultiZMovementProfile = multiZ.Capture.Profile.Timings.TryGetValue(
            MovementProfileName,
            out var multiZMovementProfile);
        gates.Add(new GateResult
        {
            Name = "Profiler captures are present and untruncated",
            Hard = true,
            Passed = control.Capture.Profile.LogEntries > 0 &&
                     !control.Capture.Profile.WasTruncated &&
                     control.Capture.ProfileTicks == control.Capture.TickTurnaround.Milliseconds.Count &&
                     hasControlMovementProfile &&
                     controlMovementProfile!.Milliseconds.Count == control.Capture.ProfileTicks &&
                     multiZ.Capture.Profile.LogEntries > 0 &&
                     !multiZ.Capture.Profile.WasTruncated &&
                     multiZ.Capture.ProfileTicks == multiZ.Capture.TickTurnaround.Milliseconds.Count &&
                     hasMultiZMovementProfile &&
                     multiZMovementProfile!.Milliseconds.Count == multiZ.Capture.ProfileTicks,
            Details =
                $"control ticks={control.Capture.ProfileTicks}, " +
                $"movement samples=" +
                $"{(hasControlMovementProfile ? controlMovementProfile!.Milliseconds.Count : 0)}, " +
                $"entries={control.Capture.Profile.LogEntries}, " +
                $"truncated={control.Capture.Profile.WasTruncated}; " +
                $"multi ticks={multiZ.Capture.ProfileTicks}, " +
                $"movement samples=" +
                $"{(hasMultiZMovementProfile ? multiZMovementProfile!.Milliseconds.Count : 0)}, " +
                $"entries={multiZ.Capture.Profile.LogEntries}, " +
                $"truncated={multiZ.Capture.Profile.WasTruncated}",
        });

        if (multiZ.Capture.Profile.Counters.TryGetValue(
                NetworkRecoveryHitCounterName,
                out var recoveryHits))
        {
            gates.Add(new GateResult
            {
                Name = "Steady-state topology capture has no recovered stale-index hits",
                Hard = false,
                Passed = recoveryHits.Sum == 0,
                Details = $"sum={recoveryHits.Sum:F0}, max/frame={recoveryHits.Max:F0}",
            });
        }

        if (multiZ.Soak.Checkpoints.Count > 0)
        {
            var retainedGrowth = multiZ.Soak.ManagedBytesAtEnd - multiZ.Soak.ManagedBytesAtStart;
            var allowedGrowth = Math.Max(
                MinimumSoakGrowthAllowanceBytes,
                multiZ.Soak.ManagedBytesAtStart / SoakGrowthDivisor);
            gates.Add(new GateResult
            {
                Name = "Soak retained managed-memory growth stays inside the provisional bound",
                Hard = false,
                Passed = retainedGrowth <= allowedGrowth,
                Details = $"growth={retainedGrowth} bytes, allowance={allowedGrowth} bytes",
            });

            var steadyCheckpoints = multiZ.Soak.Checkpoints.Skip(1).ToArray();
            var stableEntityCount = steadyCheckpoints.Length == 0 ||
                                    steadyCheckpoints.All(checkpoint =>
                                        checkpoint.EntityCount == steadyCheckpoints[0].EntityCount);
            var stableTopologyCounts = multiZ.Soak.Checkpoints.All(checkpoint =>
                checkpoint.MapCount == multiZ.Soak.MapCountAtStart &&
                checkpoint.NetworkCount == multiZ.Soak.NetworkCountAtStart);
            gates.Add(new GateResult
            {
                Name = "Soak entity and topology counts stabilize",
                Hard = false,
                Passed = stableEntityCount && stableTopologyCounts,
                Details =
                    $"entities after first checkpoint=" +
                    $"[{string.Join(", ", steadyCheckpoints.Select(checkpoint => checkpoint.EntityCount).Distinct())}], " +
                    $"maps={multiZ.Soak.MapCountAtStart}, networks={multiZ.Soak.NetworkCountAtStart}",
            });
        }

        var hasControlProfiledViewers = control.Capture.Profile.Counters.TryGetValue(
            PvsViewerCounterName,
            out var controlProfiledViewers);
        var hasMultiZProfiledViewers = multiZ.Capture.Profile.Counters.TryGetValue(
            PvsViewerCounterName,
            out var multiZProfiledViewers);
        gates.Add(new GateResult
        {
            Name = "All requested viewers are attached for profile and PVS captures",
            Hard = true,
            Passed = control.Pvs.AttachedViewersAtProfileStart == control.Pvs.RequestedViewers &&
                     control.Pvs.AttachedViewersBeforePvs == control.Pvs.RequestedViewers &&
                     multiZ.Pvs.AttachedViewersAtProfileStart == multiZ.Pvs.RequestedViewers &&
                     multiZ.Pvs.AttachedViewersBeforePvs == multiZ.Pvs.RequestedViewers &&
                     hasControlProfiledViewers &&
                     controlProfiledViewers!.Minimum == control.Pvs.RequestedViewers &&
                     controlProfiledViewers.Maximum == control.Pvs.RequestedViewers &&
                     hasMultiZProfiledViewers &&
                     multiZProfiledViewers!.Minimum == multiZ.Pvs.RequestedViewers &&
                     multiZProfiledViewers.Maximum == multiZ.Pvs.RequestedViewers,
            Details =
                $"control={control.Pvs.AttachedViewersAtProfileStart}/" +
                $"{control.Pvs.AttachedViewersBeforePvs}/{control.Pvs.RequestedViewers}, " +
                $"multi={multiZ.Pvs.AttachedViewersAtProfileStart}/" +
                $"{multiZ.Pvs.AttachedViewersBeforePvs}/{multiZ.Pvs.RequestedViewers}, " +
                $"profile ranges control=" +
                $"{(hasControlProfiledViewers ? $"{controlProfiledViewers!.Minimum:F0}..{controlProfiledViewers.Maximum:F0}" : "missing")}, " +
                $"multi=" +
                $"{(hasMultiZProfiledViewers ? $"{multiZProfiledViewers!.Minimum:F0}..{multiZProfiledViewers.Maximum:F0}" : "missing")}",
        });
        gates.Add(new GateResult
        {
            Name = "Synthetic viewer lifecycle requires no reattachment",
            Hard = true,
            Passed = control.Pvs.WarmupReattachments == 0 &&
                     control.Pvs.CaptureReattachments == 0 &&
                     control.Pvs.CyclingReattachments == 0 &&
                     multiZ.Pvs.WarmupReattachments == 0 &&
                     multiZ.Pvs.CaptureReattachments == 0 &&
                     multiZ.Pvs.CyclingReattachments == 0,
            Details =
                $"control warmup/capture/cycling=" +
                $"{control.Pvs.WarmupReattachments}/{control.Pvs.CaptureReattachments}/" +
                $"{control.Pvs.CyclingReattachments}, " +
                $"multi={multiZ.Pvs.WarmupReattachments}/{multiZ.Pvs.CaptureReattachments}/" +
                $"{multiZ.Pvs.CyclingReattachments}",
        });

        return gates;
    }

    private static EnvironmentResult CaptureEnvironment()
    {
        return new EnvironmentResult
        {
            Framework = RuntimeInformation.FrameworkDescription,
            RuntimeVersion = Environment.Version.ToString(),
            OperatingSystem = RuntimeInformation.OSDescription,
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            ServerGc = System.Runtime.GCSettings.IsServerGC,
        };
    }

    private static string GetContentPathOffset()
    {
        return AppContext.BaseDirectory;
    }

    private static void WriteReport(string path, EvidenceReport report)
    {
        var absolutePath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(absolutePath, JsonSerializer.Serialize(report, JsonOptions));
        Console.WriteLine($"Evidence report: {absolutePath}");
    }

    private static void PrintSummary(EvidenceReport report)
    {
        Console.WriteLine(report.Summary);
        foreach (var gate in report.Gates)
        {
            var severity = gate.Hard ? "gate" : "alert";
            Console.WriteLine($"[{(gate.Passed ? "PASS" : "FAIL")}] {severity}: {gate.Name} ({gate.Details})");
        }
    }

    private static ComponentReplicationCapture CaptureComponentReplication<TComponent>(
        IEntityManager entityManager,
        IRobustSerializer serializer)
        where TComponent : Component
    {
        var payloads = new List<double>();
        long payloadBytes = 0;
        var instances = 0;
        var states = 0;
        var nullStates = 0;
        using var stream = new MemoryStream();
        var query = entityManager.EntityQueryEnumerator<TComponent>();
        while (query.MoveNext(out _, out var component))
        {
            instances++;
            var state = entityManager.GetComponentState(
                entityManager.EventBus,
                component,
                null,
                GameTick.Zero);
            if (state == null)
            {
                nullStates++;
                continue;
            }

            stream.SetLength(0);
            serializer.Serialize(stream, state);
            payloads.Add(stream.Length);
            payloadBytes += stream.Length;
            states++;
        }

        return new ComponentReplicationCapture
        {
            Instances = instances,
            SerializedStates = states,
            NullStates = nullStates,
            PayloadBytes = payloadBytes,
            PayloadBytesPerState = Distribution.From(payloads),
        };
    }

    private static ComponentReplicationCapture CaptureRepresentativeComponentReplication<TComponent>(
        IEntityManager entityManager,
        IRobustSerializer serializer)
        where TComponent : Component, new()
    {
        var uid = entityManager.Spawn();
        entityManager.EnsureComponent<TComponent>(uid);
        try
        {
            return CaptureComponentReplication<TComponent>(entityManager, serializer);
        }
        finally
        {
            entityManager.DeleteEntity(uid);
        }
    }

    private static GameplayBurstCapture CaptureGameplayBursts(
        IEntityManager entityManager,
        IConfigurationManager configuration,
        SharedMapSystem mapSystem,
        CMUZLevelsSystem zLevels,
        ProfManager profiler,
        EntityUid baseMap)
    {
        var ordnance = entityManager.System<CMUTopDownOrdnanceSystem>();
        var result = new GameplayBurstCapture();
        var areaState = new List<(AreaComponent Component, bool MortarFire, bool OrbitalBombardment)>();
        var roofState = new List<(RoofingEntityComponent Component, bool MortarFire, bool OrbitalBombardment)>();

#pragma warning disable RA0002
        var areaQuery = entityManager.EntityQueryEnumerator<AreaComponent>();
        while (areaQuery.MoveNext(out _, out var area))
        {
            areaState.Add((area, area.MortarFire, area.OB));
            area.MortarFire = true;
            area.OB = true;
        }

        var roofQuery = entityManager.EntityQueryEnumerator<RoofingEntityComponent>();
        while (roofQuery.MoveNext(out _, out var roof))
        {
            roofState.Add((roof, roof.CanMortarFire, roof.CanOrbitalBombard));
            roof.CanMortarFire = true;
            roof.CanOrbitalBombard = true;
        }
#pragma warning restore RA0002

        try
        {
            var selected = FindRepresentativeOrdnanceCoordinate(
                entityManager,
                mapSystem,
                zLevels,
                ordnance,
                baseMap,
                out var expectedSurfaces);
            var columnBuffer = new CMUTopDownOrdnanceResult(selected);

            for (var i = 0; i < OrdnanceWarmupIterations; i++)
                ValidateOrdnanceResolution(ordnance, selected, expectedSurfaces, columnBuffer);

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < OrdnanceCaptureIterations; i++)
                ValidateOrdnanceResolution(ordnance, selected, expectedSurfaces, columnBuffer);

            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            configuration.SetCVar(CVars.ProfEnabled, true);
            var profilerStart = profiler.Buffer.LogWriteOffset;
            for (var i = 0; i < OrdnanceProfileIterations; i++)
                ValidateOrdnanceResolution(ordnance, selected, expectedSurfaces, columnBuffer);
            result.Ordnance = new OrdnanceBurstCapture
            {
                SelectedMap = selected.MapId.ToString(),
                SelectedX = selected.Position.X,
                SelectedY = selected.Position.Y,
                ExpectedSurfaces = expectedSurfaces,
                WarmupIterations = OrdnanceWarmupIterations,
                CaptureIterations = OrdnanceCaptureIterations,
                WallMilliseconds = elapsed.TotalMilliseconds,
                ThreadAllocatedBytes = allocated,
                ProfileIterations = OrdnanceProfileIterations,
                Profile = CaptureProfiler(profiler, profilerStart),
            };
            configuration.SetCVar(CVars.ProfEnabled, false);

            result.VehicleLanding = CaptureVehicleLandingBurst(
                entityManager,
                configuration,
                profiler,
                zLevels,
                baseMap);
            return result;
        }
        finally
        {
            configuration.SetCVar(CVars.ProfEnabled, false);
#pragma warning disable RA0002
            foreach (var (area, mortarFire, orbitalBombardment) in areaState)
            {
                area.MortarFire = mortarFire;
                area.OB = orbitalBombardment;
            }

            foreach (var (roof, mortarFire, orbitalBombardment) in roofState)
            {
                roof.CanMortarFire = mortarFire;
                roof.CanOrbitalBombard = orbitalBombardment;
            }
#pragma warning restore RA0002
        }
    }

    private static MapCoordinates FindRepresentativeOrdnanceCoordinate(
        IEntityManager entityManager,
        SharedMapSystem mapSystem,
        CMUZLevelsSystem zLevels,
        CMUTopDownOrdnanceSystem ordnance,
        EntityUid baseMap,
        out int expectedSurfaces)
    {
        if (!zLevels.TryGetZNetwork(baseMap, out var network))
            throw new InvalidOperationException("The gameplay burst capture requires a Multi-Z network.");

        MapCoordinates best = default;
        expectedSurfaces = 0;
        foreach (var (_, mapUid) in network.Value.Comp.ZLevels.OrderByDescending(entry => entry.Key))
        {
            if (mapUid is not { } map ||
                !entityManager.TryGetComponent<MapGridComponent>(map, out var grid))
            {
                continue;
            }

            foreach (var tile in mapSystem.GetAllTiles(map, grid))
            {
                var selected = mapSystem.GridTileToWorld(map, grid, tile.GridIndices);
                if (!ordnance.TryResolveImpactColumn(
                        selected,
                        CMUTopDownOrdnanceKind.OrbitalBombardment,
                        out var column))
                {
                    continue;
                }

                if (column.Surfaces.Count <= expectedSurfaces)
                    continue;

                best = selected;
                expectedSurfaces = column.Surfaces.Count;
                if (expectedSurfaces == network.Value.Comp.ZLevels.Count)
                    return best;
            }
        }

        if (expectedSurfaces == 0)
            throw new InvalidOperationException("No representative Bush ordnance column was found.");

        return best;
    }

    private static void ValidateOrdnanceResolution(
        CMUTopDownOrdnanceSystem ordnance,
        MapCoordinates selected,
        int expectedSurfaces,
        CMUTopDownOrdnanceResult column)
    {
        if (!ordnance.TryResolveImpactColumn(
                selected,
                CMUTopDownOrdnanceKind.OrbitalBombardment,
                column) ||
            column.Surfaces.Count != expectedSurfaces)
        {
            throw new InvalidOperationException(
                $"Ordnance column changed during capture: expected {expectedSurfaces} surfaces.");
        }
    }

    private static VehicleLandingBurstCapture CaptureVehicleLandingBurst(
        IEntityManager entityManager,
        IConfigurationManager configuration,
        ProfManager profiler,
        CMUZLevelsSystem zLevels,
        EntityUid baseMap)
    {
        if (!zLevels.TryGetZNetwork(baseMap, out var network))
            throw new InvalidOperationException("The vehicle landing capture requires a Multi-Z network.");

        var interiorMap = network.Value.Comp.ZLevels
            .OrderByDescending(entry => entry.Key)
            .Select(entry => entry.Value)
            .FirstOrDefault(map => map is not null && map != baseMap);
        if (interiorMap is not { } interiorMapUid)
            throw new InvalidOperationException("The vehicle landing capture requires a separate interior map.");

        var vehicle = entityManager.SpawnEntity(null, new EntityCoordinates(baseMap, Vector2.Zero));
        var occupants = new List<EntityUid>(VehicleLandingOccupants);
        try
        {
            var traversal = entityManager.EnsureComponent<CMUVehicleZTraversalComponent>(vehicle);
            traversal.LandingWheelDamageMultiplier = 0f;
            traversal.LandingCrushDamageMultiplier = 0f;
            traversal.LandingOccupantDamageMultiplier = 0.35f;

            var interior = entityManager.EnsureComponent<VehicleInteriorComponent>(vehicle);
#pragma warning disable RA0002
            interior.Map = interiorMapUid;
            interior.MapId = entityManager.GetComponent<MapComponent>(interiorMapUid).MapId;
#pragma warning restore RA0002

            var sound = entityManager.EnsureComponent<VehicleSoundComponent>(vehicle);
            sound.CollisionSound = new SoundPathSpecifier("/Audio/Effects/metal_crunch.ogg");
            sound.CollisionSoundCooldown = 0.5f;

            for (var i = 0; i < VehicleLandingOccupants; i++)
            {
                var occupantUid = entityManager.SpawnEntity(
                    null,
                    new EntityCoordinates(interiorMapUid, new Vector2(i % 6, i / 6)));
                occupants.Add(occupantUid);
                entityManager.EnsureComponent<DamageableComponent>(occupantUid);
                var occupant = entityManager.EnsureComponent<VehicleInteriorOccupantComponent>(occupantUid);
#pragma warning disable RA0002
                occupant.Vehicle = vehicle;
#pragma warning restore RA0002

                if (i < VehicleLandingTrackedOccupants)
                {
#pragma warning disable RA0002
                    interior.Passengers.Add(occupantUid);
#pragma warning restore RA0002
                }
            }

            for (var i = 0; i < VehicleLandingWarmupIterations; i++)
                RaiseVehicleLanding(entityManager, vehicle, sound);

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            for (var i = 0; i < VehicleLandingCaptureIterations; i++)
                RaiseVehicleLanding(entityManager, vehicle, sound);

            var elapsed = Stopwatch.GetElapsedTime(started);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            configuration.SetCVar(CVars.ProfEnabled, true);
            var profilerStart = profiler.Buffer.LogWriteOffset;
            for (var i = 0; i < VehicleLandingProfileIterations; i++)
                RaiseVehicleLanding(entityManager, vehicle, sound);

            return new VehicleLandingBurstCapture
            {
                Occupants = VehicleLandingOccupants,
                TrackedOccupants = VehicleLandingTrackedOccupants,
                FallbackOnlyOccupants = VehicleLandingOccupants - VehicleLandingTrackedOccupants,
                WarmupIterations = VehicleLandingWarmupIterations,
                CaptureIterations = VehicleLandingCaptureIterations,
                WallMilliseconds = elapsed.TotalMilliseconds,
                ThreadAllocatedBytes = allocated,
                ProfileIterations = VehicleLandingProfileIterations,
                Profile = CaptureProfiler(profiler, profilerStart),
            };
        }
        finally
        {
            configuration.SetCVar(CVars.ProfEnabled, false);
            foreach (var occupant in occupants)
                entityManager.DeleteEntity(occupant);
            entityManager.DeleteEntity(vehicle);
        }
    }

    private static void RaiseVehicleLanding(
        IEntityManager entityManager,
        EntityUid vehicle,
        VehicleSoundComponent sound)
    {
        sound.NextCollisionSound = TimeSpan.Zero;
        var hit = new CMUZLevelHitEvent(8f);
        entityManager.EventBus.RaiseLocalEvent(vehicle, hit);
    }

    private readonly record struct OperationSample(double Milliseconds, long ThreadAllocatedBytes);
    private readonly record struct CyclingPvsCapture(OperationCapture Capture, int Reattachments);
}

internal sealed class EvidenceOptions
{
    public string Output { get; init; } =
        Path.Combine("artifacts", "multiz-phase4", "evidence.json");

    public int WarmupTicks { get; init; } = 60;
    public int CaptureTicks { get; init; } = 300;
    public int Players { get; init; } = 8;
    public int PvsSamples { get; init; } = 30;
    public int SoakTicks { get; init; }
    public int SoakCheckpointTicks { get; init; } = 900;
    public int Seed { get; init; } = 42;

    public static EvidenceOptions Parse(string[] args)
    {
        string? GetValue(string name)
        {
            var index = Array.IndexOf(args, name);
            if (index == -1)
                return null;
            if (index + 1 >= args.Length)
                throw new ArgumentException($"Missing value after {name}.");
            return args[index + 1];
        }

        int GetPositiveInt(string name, int fallback, bool allowZero = false)
        {
            var text = GetValue(name);
            if (text == null)
                return fallback;
            if (!int.TryParse(text, out var value) ||
                allowZero && value < 0 ||
                !allowZero && value <= 0)
            {
                throw new ArgumentException($"Invalid integer value for {name}: {text}.");
            }

            return value;
        }

        return new EvidenceOptions
        {
            Output = GetValue("--output") ??
                     Path.Combine("artifacts", "multiz-phase4", "evidence.json"),
            WarmupTicks = GetPositiveInt("--warmup-ticks", 60),
            CaptureTicks = GetPositiveInt("--capture-ticks", 300),
            Players = GetPositiveInt("--players", 8),
            PvsSamples = GetPositiveInt("--pvs-samples", 30),
            SoakTicks = GetPositiveInt("--soak-ticks", 0, allowZero: true),
            SoakCheckpointTicks = GetPositiveInt("--soak-checkpoint-ticks", 900),
            Seed = GetPositiveInt("--seed", 42, allowZero: true),
        };
    }
}

internal sealed class EvidenceReport
{
    public int SchemaVersion { get; init; } = 1;
    public DateTimeOffset CapturedAtUtc { get; init; }
    public EvidenceOptions Configuration { get; init; } = new();
    public EnvironmentResult Environment { get; init; } = new();
    public bool JitWarmupCompleted { get; set; }
    public ScenarioResult? Control { get; set; }
    public ScenarioResult? MultiZ { get; set; }
    public List<ComparisonResult> Comparisons { get; set; } = [];
    public List<GateResult> Gates { get; set; } = [];
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? Error { get; set; }
}

internal sealed class EnvironmentResult
{
    public string Framework { get; init; } = string.Empty;
    public string RuntimeVersion { get; init; } = string.Empty;
    public string OperatingSystem { get; init; } = string.Empty;
    public string ProcessArchitecture { get; init; } = string.Empty;
    public int ProcessorCount { get; init; }
    public bool ServerGc { get; init; }
}

internal sealed class ScenarioResult
{
    public string Name { get; init; } = string.Empty;
    public bool MultiZ { get; init; }
    public LoadResult Load { get; set; } = new();
    public TopologyResult Topology { get; init; } = new();
    public TickCapture Capture { get; init; } = new();
    public ReplicationCapture Replication { get; init; } = new();
    public GameplayBurstCapture? GameplayBursts { get; set; }
    public PvsCapture Pvs { get; init; } = new();
    public SoakResult Soak { get; init; } = new();
    public TeardownResult Teardown { get; init; } = new();
}

internal sealed class LoadResult
{
    public double WallMilliseconds { get; init; }
    public long ThreadAllocatedBytes { get; init; }
    public long ManagedBytesBefore { get; init; }
    public long ManagedBytesAfter { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public int EntityCountBefore { get; init; }
    public int EntityCountAfter { get; init; }
    public int MapCountBefore { get; init; }
    public int MapCountAfter { get; init; }
    public int NetworkCountBefore { get; init; }
    public int NetworkCountAfter { get; init; }
    public ProfileCapture Profile { get; set; } = new();
}

internal sealed class TopologyResult
{
    public bool HasNetwork { get; set; }
    public string? NetworkEntity { get; set; }
    public int? MinimumDepth { get; set; }
    public int? MaximumDepth { get; set; }
    public int LoadedMapCount { get; set; }
    public List<int> Depths { get; init; } = [];
}

internal sealed class TickCapture
{
    public int ProfileTicks { get; set; }
    public long ManagedBytesBefore { get; set; }
    public long ManagedBytesAfter { get; set; }
    public int Gen0CollectionsBefore { get; set; }
    public int Gen0CollectionsAfter { get; set; }
    public int Gen1CollectionsBefore { get; set; }
    public int Gen1CollectionsAfter { get; set; }
    public int Gen2CollectionsBefore { get; set; }
    public int Gen2CollectionsAfter { get; set; }
    public int EntityCount { get; set; }
    public int MapCount { get; set; }
    public int NetworkCount { get; set; }
    public OperationCapture TickTurnaround { get; set; } = new();
    public ProfileCapture Profile { get; set; } = new();
}

internal sealed class ReplicationCapture
{
    public ComponentReplicationCapture TopologyNetwork { get; set; } = new();
    public ComponentReplicationCapture TopologyMaps { get; set; } = new();
    public long TopologyPayloadBytes { get; set; }
    public ComponentReplicationCapture ZPhysics { get; set; } = new();
    public ComponentReplicationCapture Falling { get; set; } = new();
    public ComponentReplicationCapture VehicleTraversal { get; set; } = new();
    public ComponentReplicationCapture RepresentativeVehicleTraversal { get; set; } = new();
}

internal sealed class ComponentReplicationCapture
{
    public int Instances { get; init; }
    public int SerializedStates { get; init; }
    public int NullStates { get; init; }
    public long PayloadBytes { get; init; }
    public Distribution PayloadBytesPerState { get; init; } = new();
}

internal sealed class GameplayBurstCapture
{
    public OrdnanceBurstCapture Ordnance { get; set; } = new();
    public VehicleLandingBurstCapture VehicleLanding { get; set; } = new();
}

internal sealed class OrdnanceBurstCapture
{
    public string SelectedMap { get; init; } = string.Empty;
    public float SelectedX { get; init; }
    public float SelectedY { get; init; }
    public int ExpectedSurfaces { get; init; }
    public int WarmupIterations { get; init; }
    public int CaptureIterations { get; init; }
    public double WallMilliseconds { get; init; }
    public long ThreadAllocatedBytes { get; init; }
    public int ProfileIterations { get; init; }
    public ProfileCapture Profile { get; init; } = new();
}

internal sealed class VehicleLandingBurstCapture
{
    public int Occupants { get; init; }
    public int TrackedOccupants { get; init; }
    public int FallbackOnlyOccupants { get; init; }
    public int WarmupIterations { get; init; }
    public int CaptureIterations { get; init; }
    public double WallMilliseconds { get; init; }
    public long ThreadAllocatedBytes { get; init; }
    public int ProfileIterations { get; init; }
    public ProfileCapture Profile { get; init; } = new();
}

internal sealed class ProfileCapture
{
    public long TotalLogEntriesWritten { get; init; }
    public long LogEntries { get; init; }
    public bool WasTruncated { get; init; }
    public Dictionary<string, TimingCapture> Timings { get; init; } = [];
    public Dictionary<string, Distribution> Counters { get; init; } = [];
}

internal sealed class TimingCapture
{
    public Distribution Milliseconds { get; init; } = new();
    public Distribution AllocatedBytes { get; init; } = new();
}

internal sealed class PvsCapture
{
    public int RequestedViewers { get; init; }
    public int WarmupReattachments { get; set; }
    public int CaptureReattachments { get; set; }
    public int AttachedViewersAtProfileStart { get; set; }
    public int AttachedViewersBeforePvs { get; set; }
    public OperationCapture Static { get; set; } = new();
    public OperationCapture Cycling { get; set; } = new();
    public int CyclingReattachments { get; set; }
}

internal sealed class OperationCapture
{
    public Distribution Milliseconds { get; init; } = new();
    public Distribution? ThreadAllocatedBytes { get; init; }
    public string? AllocationScope { get; init; }
}

internal sealed class SoakResult
{
    public long ManagedBytesAtStart { get; set; }
    public long ManagedBytesAtEnd { get; set; }
    public int EntityCountAtStart { get; set; }
    public int EntityCountAtEnd { get; set; }
    public int MapCountAtStart { get; set; }
    public int MapCountAtEnd { get; set; }
    public int NetworkCountAtStart { get; set; }
    public int NetworkCountAtEnd { get; set; }
    public List<SoakCheckpoint> Checkpoints { get; init; } = [];
}

internal sealed class SoakCheckpoint
{
    public int Tick { get; init; }
    public long ManagedBytes { get; init; }
    public int EntityCount { get; init; }
    public int MapCount { get; init; }
    public int NetworkCount { get; init; }
}

internal sealed class TeardownResult
{
    public double DeleteWallMilliseconds { get; set; }
    public long DeleteThreadAllocatedBytes { get; set; }
    public ProfileCapture Profile { get; set; } = new();
    public int MapCountBefore { get; set; }
    public int MapCountAfter { get; set; }
    public int NetworkCountBefore { get; set; }
    public int NetworkCountAfter { get; set; }
}

internal sealed class Distribution
{
    public int Count { get; init; }
    public double Minimum { get; init; }
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
    public double Maximum { get; init; }
    public double Mean { get; init; }
    public double Sum { get; init; }

    [JsonIgnore]
    public double Min => Minimum;

    [JsonIgnore]
    public double Max => Maximum;

    public static Distribution From(IEnumerable<double> source)
    {
        var values = source.Order().ToArray();
        if (values.Length == 0)
            return new Distribution();

        var sum = values.Sum();
        return new Distribution
        {
            Count = values.Length,
            Minimum = values[0],
            P50 = Percentile(values, 0.50),
            P95 = Percentile(values, 0.95),
            P99 = Percentile(values, 0.99),
            Maximum = values[^1],
            Mean = sum / values.Length,
            Sum = sum,
        };
    }

    private static double Percentile(double[] values, double percentile)
    {
        var index = Math.Clamp((int) Math.Ceiling(percentile * values.Length) - 1, 0, values.Length - 1);
        return values[index];
    }
}

internal sealed class ComparisonResult
{
    public string Name { get; init; } = string.Empty;
    public double Control { get; init; }
    public double MultiZ { get; init; }
    public double Delta { get; init; }
    public double? Ratio { get; init; }
}

internal sealed class GateResult
{
    public string Name { get; init; } = string.Empty;
    public bool Hard { get; init; }
    public bool Passed { get; init; }
    public string Details { get; init; } = string.Empty;
}
