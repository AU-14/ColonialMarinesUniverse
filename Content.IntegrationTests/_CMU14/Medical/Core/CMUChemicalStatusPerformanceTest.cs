using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.Json;
using Prometheus;
using Content.Shared.Administration.Systems;
using Content.Shared.CMU14.Chemistry.Effects;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Movement.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.IntegrationTests.CMU14.Medical.Core;

/// <summary>
/// Explicit integration workload. Host measurements include the test scheduler, client and
/// dirty-version observer; these are not standalone production server frame measurements.
/// </summary>
[TestFixture, Explicit]
public sealed class CMUChemicalStatusPerformanceTest
{
    [TestCase(50, false)]
    [TestCase(200, false)]
    [TestCase(500, false)]
    [TestCase(50, true)]
    [TestCase(200, true)]
    [TestCase(500, true)]
    public async Task SustainedMedication(int population, bool halfInjured)
    {
        await using var pair = await PoolManager.GetServerClient();
        var patients = new List<EntityUid>(population);
        const int samples = 24;
        var sampleTicks = pair.SecondsToTicks(0.25f);
        var preparationTicks = pair.SecondsToTicks(1);
        var wallMilliseconds = new double[samples];
        var cpuMilliseconds = new double[samples];
        var allocations = new long[samples];
        var observedVersions = new int[samples];
        var simulatedSeconds = new double[samples];
        var medicationAndMovement = new double[population * 3];
        var painObservations = new double[population * 3];
        var serverSystemTickWallMilliseconds = new double[samples * sampleTicks];
        var serverSystemTickAllocatedBytes = new long[samples * sampleTicks];
        using var process = Process.GetCurrentProcess();
        var retainedBeforePopulation = GC.GetTotalMemory(true);
        long retainedBeforeSamples = 0;
        long retainedOutstandingWork = 0;
        long retainedMatchingState = 0;
        var nerveAdditions = 0;
        var nerveRemovals = 0;
        var finalAggregateDamage = 0.0;
        var finalPatientPain = 0.0;
        var settledPatientPain = 0.0;
        var profileSystems = Environment.GetEnvironmentVariable("CMU_MEDICAL_PERF_SYSTEM_TIMINGS") == "1";
        var systems = pair.Server.ResolveDependency<IEntitySystemManager>();
        var originalMetricsEnabled = systems.MetricsEnabled;
        string? timingBefore = null;
        string? timingAfter = null;

        try
        {
            await pair.Server.WaitPost(() =>
            {
                if (profileSystems)
                    systems.MetricsEnabled = true;
                var entities = pair.Server.EntMan;
                entities.System<CMUChemicalDirtyProbeSystem>();
                for (var i = 0; i < population; i++)
                {
                    var patient = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                    patients.Add(patient);
                    entities.AddComponent<CMUChemicalDirtyProbeComponent>(patient);
                }
            });

            // Warm code, then restore the canonical injury/medication age used again
            // for the matching-state retained snapshot at the end of the experiment.
            for (var preparation = 0; preparation < 2; preparation++)
            {
                await pair.Server.WaitPost(() => PreparePopulation(pair.Server.EntMan, patients,
                    pair.Server.ResolveDependency<IRobustRandom>(), halfInjured));
                await pair.RunTicksSync(preparationTicks);
            }
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<CMUChemicalDirtyProbeSystem>().ResetCounters());
            await pair.Server.WaitAssertion(() => CaptureMedicationAndMovement(pair.Server.EntMan, patients,
                pair.Server.ResolveDependency<IGameTiming>().CurTime, medicationAndMovement));
            if (profileSystems)
                timingBefore = await ReadServerTimings();
            retainedBeforeSamples = GC.GetTotalMemory(true);
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<CMUMedicalFrameProbeEndSystem>()
                .Begin(serverSystemTickWallMilliseconds, serverSystemTickAllocatedBytes));

            for (var sample = 0; sample < samples; sample++)
            {
                TimeSpan simulationStart = default;
                await pair.Server.WaitPost(() => simulationStart = pair.Server.ResolveDependency<IGameTiming>().CurTime);
                var allocated = GC.GetTotalAllocatedBytes(true);
                var cpu = process.TotalProcessorTime;
                var start = Stopwatch.GetTimestamp();
                if (sample % 4 == 0)
                    await pair.Server.WaitPost(() => ApplyMedication(pair.Server.EntMan, patients));
                await pair.RunTicksSync(sampleTicks);
                wallMilliseconds[sample] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                cpuMilliseconds[sample] = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                allocations[sample] = GC.GetTotalAllocatedBytes(true) - allocated;

                // Capture a version written after the observer's last update. Boundary
                // probes and their reset are outside the timed/allocated block.
                await pair.Server.WaitPost(() =>
                {
                    var observer = pair.Server.EntMan.System<CMUChemicalDirtyProbeSystem>();
                    observer.Observe();
                    foreach (var patient in patients)
                    {
                        var probe = pair.Server.EntMan.GetComponent<CMUChemicalDirtyProbeComponent>(patient);
                        observedVersions[sample] += probe.ObservedVersions;
                        nerveAdditions += probe.Additions;
                        nerveRemovals += probe.Removals;
                    }
                    simulatedSeconds[sample] = (pair.Server.ResolveDependency<IGameTiming>().CurTime - simulationStart).TotalSeconds;
                    observer.ResetCounters();
                });
            }

            await pair.Server.WaitAssertion(() =>
            {
                var frameProbe = pair.Server.EntMan.System<CMUMedicalFrameProbeEndSystem>();
                frameProbe.End();
                Assert.That(frameProbe.Recorded, Is.EqualTo(serverSystemTickWallMilliseconds.Length));
            });
            if (profileSystems)
                timingAfter = await ReadServerTimings();
            await pair.Server.WaitPost(() =>
            {
                var entities = pair.Server.EntMan;
                CaptureMedicationAndMovement(entities, patients,
                    pair.Server.ResolveDependency<IGameTiming>().CurTime, medicationAndMovement);
                var damageable = entities.System<DamageableSystem>();
                var painSystem = entities.System<SharedPainShockSystem>();
                var now = pair.Server.ResolveDependency<IGameTiming>().CurTime;
                for (var i = 0; i < patients.Count; i++)
                {
                    var patient = patients[i];
                    finalAggregateDamage += damageable.GetTotalDamage(patient).Float();
                    if (entities.TryGetComponent<PainShockComponent>(patient, out var pain))
                    {
                        finalPatientPain += pain.Pain.Float();
                        painObservations[i * 3] = pain.Pain.Float();
                        painObservations[i * 3 + 1] = Math.Round((now - pain.LastPainUpdate).TotalSeconds, 6);
                        // Compare clinical state at one event time, as well as the
                        // raw sampled state. Modifier hooks can settle pain between
                        // its periodic display updates; do not erase that evidence.
                        painSystem.SettlePainBeforeModifierChange(patient);
                        painObservations[i * 3 + 2] = pain.Pain.Float();
                        settledPatientPain += pain.Pain.Float();
                    }
                }
            });
            retainedOutstandingWork = GC.GetTotalMemory(true);
            await pair.Server.WaitPost(() => PreparePopulation(pair.Server.EntMan, patients,
                pair.Server.ResolveDependency<IRobustRandom>(), halfInjured));
            await pair.RunTicksSync(preparationTicks);
            await pair.Server.WaitPost(() => pair.Server.EntMan.System<CMUChemicalDirtyProbeSystem>().ResetCounters());
            retainedMatchingState = GC.GetTotalMemory(true);
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                systems.MetricsEnabled = originalMetricsEnabled;
                pair.Server.EntMan.System<CMUMedicalFrameProbeEndSystem>().End();
                foreach (var patient in patients)
                    pair.Server.EntMan.DeleteEntity(patient);
            });
        }
        await pair.RunTicksSync(2);
        var retainedAfterCleanup = GC.GetTotalMemory(true);
        TestContext.Out.WriteLine("CMU_MEDICAL_PERF " + JsonSerializer.Serialize(new
        {
            label = Environment.GetEnvironmentVariable("CMU_MEDICAL_PERF_LABEL") ?? "candidate",
            workload = "sustainedMedication",
            population,
            halfInjured,
            seed = 781,
            requestedSampleSeconds = 0.25,
            sampleTicks,
            preparationTicks,
            tickPeriodSeconds = pair.Server.Timing.TickPeriod.TotalSeconds,
            samples,
            simulatedSeconds,
            totalMeasuredSimulatedSeconds = simulatedSeconds.Sum(),
            runtime = Environment.Version.ToString(),
            processorCount = Environment.ProcessorCount,
            wallMilliseconds,
            cpuMilliseconds,
            allocations,
            nerveObservedVersionChanges = observedVersions,
            nerveAdditions,
            nerveRemovals,
            finalAggregateDamage,
            finalPatientPain,
            settledPatientPain,
            painObservations,
            medicationAndMovement,
            serverSystemTickWallMilliseconds,
            serverSystemTickAllocatedBytes,
            frameMeasurementScope = "Test-only preallocated probes bracket every server entity-system Update. Includes the per-tick nerve observer and all server systems, excludes event-bus flush/culling/network/client rendering and medication applied between ticks. These are entity-system phase wall/allocation percentiles, not complete production frames. Process CPU block measurements are separate.",
            profileSystems,
            timingBefore,
            timingAfter,
            retainedBeforePopulation,
            retainedBeforeSamples,
            retainedOutstandingWork,
            retainedMatchingState,
            retainedAfterCleanup,
            // Profiling retains exported histogram snapshots and changes instrumentation
            // cost. Its raw GC observations are not a retained-memory comparison.
            matchingStateRetainedDelta = profileSystems ? (long?) null : retainedMatchingState - retainedBeforeSamples,
            medianWallMs = wallMilliseconds.Order().ElementAt(samples / 2),
            medianCpuMs = cpuMilliseconds.Order().ElementAt(samples / 2),
            measurementScope = "Host blocks include server/client ticks, test scheduler and per-tick nerve-version observer. Boundary probes and preparation are excluded. Observed versions track component identity and LastModifiedTick changes, not Dirty calls or bytes; same-tick writes coalesce. Retained snapshots cover the full process. Matching snapshots use the same rejuvenated injury/medication preparation and age; outstanding-work memory is a separate post-workload snapshot. Nullspace patients have no player viewers.",
            profilingScope = "Optional profileSystems enables existing server per-system Stopwatch histograms; before/after exports also include server update phases. Subtract cumulative buckets/count/sum. These are instrumented wall-time distributions in seconds with coarse buckets, not CPU or complete-frame percentiles. Profiling runs do not provide a valid retained-memory comparison and are separate from uninstrumented A/B runs.",
        }));
        await pair.CleanReturnAsync();
    }

    private static async Task<string> ReadServerTimings()
    {
        using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream);
        var exposition = Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int) stream.Length);
        return string.Join('\n', exposition.Split('\n').Where(line =>
            line.StartsWith("robust_server_update_usage_", StringComparison.Ordinal) ||
            line.StartsWith("robust_entity_systems_update_usage_", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Outside measured blocks: require every intended property, and retain each
    /// patient's remaining dose time, walk speed and sprint speed for A/B comparison.
    /// </summary>
    private static void CaptureMedicationAndMovement(IEntityManager entities, List<EntityUid> patients,
        TimeSpan now, double[] projection)
    {
        for (var i = 0; i < patients.Count; i++)
        {
            var patient = patients[i];
            var nerve = entities.GetComponent<ChemicalNerveStimulationComponent>(patient);
            var muscle = entities.GetComponent<ChemicalMuscleStimulationComponent>(patient);
            var cardiac = entities.GetComponent<ChemicalCardiacPacingComponent>(patient);
            var density = entities.GetComponent<ChemicalHyperdensityComponent>(patient);
            var shield = entities.GetComponent<ChemicalNeuroshieldComponent>(patient);
            var sensitivity = entities.GetComponent<ChemicalPainSensitivityComponent>(patient);
            var addiction = entities.GetComponent<ChemicalAddictionTreatmentComponent>(patient);
            Assert.That(nerve.Strength == 2 && muscle.Strength == 2 && cardiac.Strength == 2 &&
                density.Protection == 0.75f && shield.Protection == 0.8f && sensitivity.Multiplier == 1 &&
                addiction.Strength == 2 && addiction.Progress == 0, Is.True, "Medication projection changed.");
            Assert.That(nerve.ExpiresAt > now && muscle.ExpiresAt == nerve.ExpiresAt &&
                cardiac.ExpiresAt == nerve.ExpiresAt && density.ExpiresAt == nerve.ExpiresAt &&
                shield.ExpiresAt == nerve.ExpiresAt && sensitivity.ExpiresAt == nerve.ExpiresAt &&
                addiction.ExpiresAt == nerve.ExpiresAt, Is.True, "Live source expiry projection changed.");
            var movement = entities.GetComponent<MovementSpeedModifierComponent>(patient);
            // Discard sub-microsecond absolute clock conversion noise, not clinical differences.
            projection[i * 3] = Math.Round((nerve.ExpiresAt - now).TotalSeconds, 6);
#pragma warning disable RA0002 // Read the actual cached movement projection without refreshing it.
            projection[i * 3 + 1] = Math.Round(movement.CurrentWalkSpeed, 6);
            projection[i * 3 + 2] = Math.Round(movement.CurrentSprintSpeed, 6);
#pragma warning restore RA0002
        }
    }

    private static void PreparePopulation(IEntityManager entities, List<EntityUid> patients,
        IRobustRandom random, bool halfInjured)
    {
        random.SetSeed(781);
        var rejuvenate = entities.System<RejuvenateSystem>();
        var damageable = entities.System<DamageableSystem>();
        var injury = new DamageSpecifier { DamageDict = { ["Slash"] = 10, ["Heat"] = 5 } };
        for (var i = 0; i < patients.Count; i++)
        {
            rejuvenate.PerformRejuvenate(patients[i]);
            if (halfInjured && i % 2 == 0)
                damageable.TryChangeDamage(patients[i], injury, ignoreResistances: true);
        }
        ApplyMedication(entities, patients);
    }

    private static void ApplyMedication(IEntityManager entities, List<EntityUid> patients)
    {
        var status = entities.System<ChemicalPropertyStatusSystem>();
        foreach (var patient in patients)
        {
            status.ApplyNerveStimulation(patient, 2, "benchmark");
            status.ApplyMuscleStimulation(patient, 2, "benchmark");
            status.ApplyCardiacPacing(patient, 2, "benchmark");
            status.ApplyHyperdensity(patient, source: "benchmark");
            status.ApplyNeuroshield(patient, source: "benchmark");
            status.ApplyPainSensitivity(patient, 1, "benchmark");
            status.ApplyAddictionTreatment(patient, 2, 0, "benchmark");
        }
    }
}

[RegisterComponent]
public sealed partial class CMUChemicalDirtyProbeComponent : Component
{
    public ChemicalNerveStimulationComponent? LastObservedComponent;
    public GameTick LastObservedTick;
    public int ObservedVersions;
    public int Additions;
    public int Removals;
}

public sealed partial class CMUChemicalDirtyProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(ChemicalPropertyStatusSystem));
    }

    public override void Update(float frameTime) => Observe();

    public void ResetCounters()
    {
        Observe();
        var query = EntityQueryEnumerator<CMUChemicalDirtyProbeComponent>();
        while (query.MoveNext(out var probe))
        {
            probe.ObservedVersions = 0;
            probe.Additions = 0;
            probe.Removals = 0;
        }
    }

    public void Observe()
    {
        var query = EntityQueryEnumerator<CMUChemicalDirtyProbeComponent>();
        while (query.MoveNext(out var uid, out var probe))
        {
            TryComp<ChemicalNerveStimulationComponent>(uid, out var nerve);
            var tick = nerve?.LastModifiedTick ?? default;
            if (ReferenceEquals(nerve, probe.LastObservedComponent) && tick == probe.LastObservedTick)
                continue;
            if (!ReferenceEquals(nerve, probe.LastObservedComponent))
            {
                if (probe.LastObservedComponent != null)
                    probe.Removals++;
                if (nerve != null)
                    probe.Additions++;
            }
            probe.LastObservedComponent = nerve;
            probe.LastObservedTick = tick;
            probe.ObservedVersions++;
        }
    }
}
