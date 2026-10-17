using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Content.Server.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.IntegrationTests.CMU14.Medical.Diagnostics;

/// <summary>
/// Explicit synchronous projection workloads. Logical viewers share one test server;
/// serialization measures message bytes before transport, compression and UI rendering.
/// </summary>
[TestFixture, Explicit]
public sealed class CMUMedicalProjectionPerformanceTest
{
    private const int Samples = 16;
    private const int Seed = 61723;

    [TestPrototypes]
    private const string Chemicals = """
        - type: reagent
          parent: Water
          id: CMUProjectionBenchmarkKnown
          unknown: false
          overdose: 3
        - type: reagent
          parent: Water
          id: CMUProjectionBenchmarkUnknown
          unknown: true
        """;

    [TestCase(50, false, 1)]
    [TestCase(50, false, 4)]
    [TestCase(50, false, 8)]
    [TestCase(50, true, 1)]
    [TestCase(50, true, 4)]
    [TestCase(50, true, 8)]
    [TestCase(200, false, 1)]
    [TestCase(200, false, 4)]
    [TestCase(200, false, 8)]
    [TestCase(200, true, 1)]
    [TestCase(200, true, 4)]
    [TestCase(200, true, 8)]
    public async Task ProjectAndSerializeLogicalViewers(int population, bool halfInjured, int viewerCount)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        var spawned = new List<EntityUid>();
        ProjectionReport? report = null;
        try
        {
            // Setup, warm-up, samples and retained snapshots share a server callback.
            // Physiology cannot advance between samples and alter the compared states.
            await pair.Server.WaitAssertion(() =>
            {
                var em = pair.Server.EntMan;
                var scans = em.System<HealthScannerSystem>();
                var autodoc = em.System<CMUAutodocSystem>();
                var bay = em.System<CMUMedicalPatientBaySystem>();
                var skills = em.System<SkillsSystem>();
                var bloodstream = em.System<SharedRMCBloodstreamSystem>();
                var solutions = em.System<SharedSolutionContainerSystem>();
                var damage = em.System<DamageableSystem>();
                var serializer = pair.Server.ResolveDependency<IRobustSerializer>();
                var timing = pair.Server.ResolveDependency<IGameTiming>();
                pair.Server.ResolveDependency<IRobustRandom>().SetSeed(Seed);
                var viewers = new EntityUid[viewerCount];
                var patients = new Patient[population];
                var scannerLatest = new object[population * viewerCount];
                var autodocLatest = new object[population * viewerCount];
                var scannerSamples = new ProjectionSample[Samples];
                var autodocSamples = new ProjectionSample[Samples];
                var maps = em.System<SharedMapSystem>();
                // The 200-pod lookup takes the spatial path instead of scanning every
                // component. Real tiles keep every anchored machine inside grid bounds.
                // Map preparation is outside both timing and the population snapshot.
                for (var i = 0; i < population; i++)
                {
                    var tile = map.Tile.GridIndices + new Vector2i(i % 20 * 10, i / 20 * 10);
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, tile, map.Tile.Tile);
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, tile + new Vector2i(1, 0), map.Tile.Tile);
                }
                var retainedBeforePopulation = GC.GetTotalMemory(true);

                EntityUid Spawn(string prototype, EntityCoordinates coordinates)
                {
                    var entity = em.SpawnEntity(prototype, coordinates);
                    spawned.Add(entity);
                    return entity;
                }

                for (var i = 0; i < viewerCount; i++)
                {
                    viewers[i] = Spawn("CMMobHuman", map.GridCoords);
                    skills.SetSkill(viewers[i], "RMCSkillMedical", i % 2 == 0 ? 2 : 0);
                    skills.SetSkill(viewers[i], "RMCSkillSurgery", 2);
                }

                var injury = new DamageSpecifier { DamageDict = { ["Slash"] = 10, ["Heat"] = 5 } };
                List<ReagentData> firstDonor = [new DnaData { DNA = "benchmark-donor-one" }];
                List<ReagentData> secondDonor = [new DnaData { DNA = "benchmark-donor-two" }];
                for (var i = 0; i < population; i++)
                {
                    // Disjoint linkage radii prevent a neighboring machine from being
                    // accidentally measured in place of the intended patient.
                    var tile = map.Tile.GridIndices + new Vector2i(i % 20 * 10, i / 20 * 10);
                    var coordinates = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, tile);
                    var body = Spawn("CMMobHuman", coordinates);
                    var scanner = Spawn("CMHealthAnalyzer", coordinates);
                    var console = Spawn("CMUAutodocConsole", coordinates);
                    var pod = Spawn("CMUAutodocPod",
                        maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, tile + new Vector2i(1, 0)));
                    Assert.That(em.GetComponent<TransformComponent>(console).Anchored, Is.True);
                    Assert.That(em.GetComponent<TransformComponent>(pod).Anchored, Is.True);
                    if (halfInjured && i % 2 == 0)
                        Assert.That(damage.TryChangeDamage(body, injury, ignoreResistances: true), Is.Not.Null);

                    // A third are unmedicated, a third have a known medication, and a
                    // third mix DNA variants of that medication with an unknown agent.
                    if (i % 3 != 0)
                    {
                        Assert.That(bloodstream.TryGetChemicalSolution(body, out var solution, out _), Is.True);
                        solutions.SetCapacity(solution, 2000);
                        Assert.That(solutions.TryAddReagent(solution, "CMUProjectionBenchmarkKnown", 2, data: firstDonor), Is.True);
                        if (i % 3 == 2)
                        {
                            Assert.That(solutions.TryAddReagent(solution, "CMUProjectionBenchmarkKnown", 2, data: secondDonor), Is.True);
                            Assert.That(solutions.TryAddReagent(solution, "CMUProjectionBenchmarkUnknown", 7, data: secondDonor), Is.True);
                        }
                    }
                    var podComponent = em.GetComponent<CMUAutodocPodComponent>(pod);
                    Assert.That(bay.TryInsertPatient(pod, podComponent.BodyContainer, body), Is.True);
                    var consoleComponent = em.GetComponent<CMUAutodocConsoleComponent>(console);
                    var offered = autodoc.BuildStateForViewer(console, consoleComponent, viewers[0]);
                    Assert.That(offered.Patient, Is.EqualTo(em.GetNetEntity(body)));
                    Assert.That(offered.Pod, Is.EqualTo(em.GetNetEntity(pod)));
                    patients[i] = new Patient(body, scanner, console, consoleComponent);
                }

                using var stream = new MemoryStream(64 * 1024);
                // Prime native CPU sampling and serializer type tables before measuring.
                _ = ReadThreadCpuTicks();
                for (var warm = 0; warm < 2; warm++)
                {
                    RunBlock(false, warm, scans, autodoc, serializer, stream, patients, viewers, scannerLatest);
                    RunBlock(true, warm, scans, autodoc, serializer, stream, patients, viewers, autodocLatest);
                }
                var retainedBeforeSamples = GC.GetTotalMemory(true);
                var simulationStart = timing.CurTime;
                for (var sample = 0; sample < Samples; sample++)
                {
                    // Alternate workload order to reduce systematic order/thermal bias.
                    if (sample % 2 == 0)
                    {
                        scannerSamples[sample] = RunBlock(false, sample, scans, autodoc, serializer, stream, patients, viewers, scannerLatest);
                        autodocSamples[sample] = RunBlock(true, sample, scans, autodoc, serializer, stream, patients, viewers, autodocLatest);
                    }
                    else
                    {
                        autodocSamples[sample] = RunBlock(true, sample, scans, autodoc, serializer, stream, patients, viewers, autodocLatest);
                        scannerSamples[sample] = RunBlock(false, sample, scans, autodoc, serializer, stream, patients, viewers, scannerLatest);
                    }
                }
                var retainedMatchingState = GC.GetTotalMemory(true);
                Assert.That(timing.CurTime, Is.EqualTo(simulationStart));
                Assert.That(scannerSamples.All(s => s.SerializedBytes > 0), Is.True);
                Assert.That(autodocSamples.All(s => s.SerializedBytes > 0), Is.True);
                GC.KeepAlive(scannerLatest);
                GC.KeepAlive(autodocLatest);
                report = new ProjectionReport(scannerSamples, autodocSamples, retainedBeforePopulation,
                    retainedBeforeSamples, retainedMatchingState, stream.Capacity);
            });
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                for (var i = spawned.Count - 1; i >= 0; i--)
                    pair.Server.EntMan.DeleteEntity(spawned[i]);
            });
        }

        await pair.RunTicksSync(2);
        var retainedAfterCleanup = GC.GetTotalMemory(true);
        Assert.That(report, Is.Not.Null);
        TestContext.Out.WriteLine("CMU_MEDICAL_PROJECTION_PERF " + JsonSerializer.Serialize(new
        {
            label = Environment.GetEnvironmentVariable("CMU_MEDICAL_PERF_LABEL") ?? "candidate",
            workload = "logicalViewerProjectionAndSerialization",
            population,
            halfInjured,
            viewerCount,
            samples = Samples,
            seed = Seed,
            projectionsPerBlock = population * viewerCount,
            chemistry = "round-robin empty, known, known mixed-DNA plus unknown",
            mapLayout = "two floor tiles per console/pod pair, 10-tile spacing; floor setup precedes population memory snapshot",
            runtime = Environment.Version.ToString(),
            processorCount = Environment.ProcessorCount,
            threadCpuClock = OperatingSystem.IsWindows() ? "Windows GetThreadTimes, 100ns units" : "unavailable",
            report,
            retainedAfterCleanup,
            matchingStateRetainedDelta = report!.RetainedMatchingState - report.RetainedBeforeSamples,
            scannerMedianWallMs = Median(report.Scanner.Select(s => s.WallMilliseconds)),
            autodocMedianWallMs = Median(report.Autodoc.Select(s => s.WallMilliseconds)),
            measurementScope = "Synchronous server projection plus real Robust message serialization for distinct logical viewer/patient pairs; no transport, actual multiple network clients, rendered controls or frame timing. Setup, correctness probes, warmup, GC retained snapshots and JSON output are excluded from timed blocks. Each block includes overwriting one retained latest message per pair and reuse of a prewarmed MemoryStream. Thread allocation uses GC.GetAllocatedBytesForCurrentThread; CPU excludes other threads when supported. Retained snapshots cover the full process at the same simulation time with matching logical retained projections and may include runtime/client noise. Raw samples are provided for repeated-run comparisons; this test asserts no performance threshold.",
        }));
        await pair.CleanReturnAsync();
    }

    private static ProjectionSample RunBlock(bool machine, int rotation, HealthScannerSystem scans,
        CMUAutodocSystem autodoc, IRobustSerializer serializer, MemoryStream stream,
        Patient[] patients, EntityUid[] viewers, object[] latest)
    {
        long bytes = 0;
        var cpuBefore = ReadThreadCpuTicks();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var offset = 0; offset < patients.Length; offset++)
        {
            var index = (offset + rotation) % patients.Length;
            var patient = patients[index];
            for (var viewerIndex = 0; viewerIndex < viewers.Length; viewerIndex++)
            {
                var viewer = viewers[(viewerIndex + rotation) % viewers.Length];
                object message = machine
                    ? new CMUAutodocStateMessage(autodoc.BuildStateForViewer(patient.Console, patient.ConsoleComponent, viewer))
                    : new HealthScannerStateMessage(scans.BuildStateForViewer(patient.Scanner, patient.Body, viewer));
                stream.Position = 0;
                stream.SetLength(0);
                serializer.Serialize(stream, message);
                bytes += stream.Length;
                latest[index * viewers.Length + (viewerIndex + rotation) % viewers.Length] = message;
            }
        }
        var wall = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var cpuAfter = ReadThreadCpuTicks();
        return new ProjectionSample(wall,
            cpuBefore.HasValue && cpuAfter.HasValue ? (cpuAfter.Value - cpuBefore.Value) / 10000.0 : null,
            allocated, bytes);
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.Order().ToArray();
        return (ordered[(ordered.Length - 1) / 2] + ordered[ordered.Length / 2]) / 2;
    }

    private static long? ReadThreadCpuTicks()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        return GetThreadTimes(GetCurrentThread(), out _, out _, out var kernel, out var user) != 0
            ? kernel + user
            : null;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern nint GetCurrentThread();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetThreadTimes(nint thread, out long creation, out long exit,
        out long kernel, out long user);

    private sealed record Patient(EntityUid Body, EntityUid Scanner, EntityUid Console,
        CMUAutodocConsoleComponent ConsoleComponent);

    private sealed record ProjectionReport(ProjectionSample[] Scanner, ProjectionSample[] Autodoc,
        long RetainedBeforePopulation, long RetainedBeforeSamples, long RetainedMatchingState,
        int SerializationBufferCapacity);

    private readonly record struct ProjectionSample(double WallMilliseconds, double? ThreadCpuMilliseconds,
        long ThreadAllocatedBytes, long SerializedBytes);
}
