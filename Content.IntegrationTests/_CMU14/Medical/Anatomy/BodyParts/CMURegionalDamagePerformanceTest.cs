using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Content.Shared.Administration.Systems;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.IntegrationTests.CMU14.Medical.Anatomy.BodyParts;

/// <summary>
/// Explicit mutation and integration-tick workloads. Host block timings include the client
/// and test scheduler; mutation timings cover only the synchronous server operation.
/// Neither is a production server frame percentile or a network bandwidth measurement.
/// </summary>
[TestFixture, Explicit]
public sealed class CMURegionalDamagePerformanceTest
{
    private const int Seed = 29317;
    private const int Samples = 32;
    private const float SampleSeconds = 0.25f;
    private const int PreparationTicks = 2;

    [TestCase(50, 0)]
    [TestCase(200, 0)]
    [TestCase(500, 0)]
    [TestCase(50, 50)]
    [TestCase(200, 50)]
    [TestCase(500, 50)]
    public Task DamageHealingBursts(int population, int injuredPercent)
        => RunWorkload(population, injuredPercent, churn: false);

    [TestCase(50)]
    [TestCase(200)]
    [TestCase(500)]
    public Task DetachedSubtreeChurn(int population)
        => RunWorkload(population, 50, churn: true);

    private static async Task RunWorkload(int population, int injuredPercent, bool churn)
    {
        await using var pair = await PoolManager.GetServerClient();
        var patients = new List<Patient>(population);
        var carriers = new List<EntityUid>(population);
        var mutationMilliseconds = new double[Samples];
        var mutationAllocations = new long[Samples];
        var hostWallMilliseconds = new double[Samples];
        var processCpuMilliseconds = new double[Samples];
        var processAllocations = new long[Samples];
        var modifiedComponents = new int[Samples];
        var addedComponents = new int[Samples];
        var removedComponents = new int[Samples];
        var baselineAlreadyDirty = new int[Samples];
        var simulatedSeconds = new double[Samples];
        var sampleTicks = pair.SecondsToTicks(SampleSeconds);
        var retainedBeforePopulation = GC.GetTotalMemory(true);
        long retainedBeforeSamples = 0;
        long retainedOutstandingWork = 0;
        long retainedMatchingState = 0;
        var operationsPerSample = Math.Max(1, population / 10);
        using var process = Process.GetCurrentProcess();

        try
        {
            await pair.Server.WaitAssertion(() =>
            {
                var entities = pair.Server.EntMan;
                var index = entities.System<CMUMedicalBodyIndexSystem>();
                pair.Server.ResolveDependency<IRobustRandom>().SetSeed(Seed);
                for (var i = 0; i < population; i++)
                {
                    var body = entities.SpawnEntity("CMMobHuman", MapCoordinates.Nullspace);
                    var parts = index.GetBodyParts(body).ToArray();
                    var torso = parts.Single(p => p.Comp.PartType == BodyPartType.Torso).Owner;
                    var arm = parts.Single(p => p.Comp.PartType == BodyPartType.Arm && p.Comp.Symmetry == BodyPartSymmetry.Right).Owner;
                    var slot = index.GetBodyPartSlots(torso).Single(s => s.Part == arm).SlotId;
                    var organs = index.GetOrgans(body).Select(o => o.Owner).ToArray();
                    patients.Add(new Patient(body, torso, arm, slot, organs, new ComponentStamp[1 + organs.Length * 5]));
                }
            });

            for (var sample = -2; sample < Samples; sample++)
            {
                await pair.Server.WaitPost(() => PreparePopulation(pair.Server.EntMan, patients,
                    pair.Server.ResolveDependency<IRobustRandom>(), injuredPercent, churn, Seed + sample + 2));
                await pair.RunTicksSync(PreparationTicks);
                if (sample == 0)
                    retainedBeforeSamples = GC.GetTotalMemory(true);

                TimeSpan simulationStart = default;
                await pair.Server.WaitPost(() =>
                {
                    var timing = pair.Server.ResolveDependency<IGameTiming>();
                    simulationStart = timing.CurTime;
                    var alreadyDirty = CaptureComponents(pair.Server.EntMan, patients, timing.CurTick);
                    if (sample >= 0)
                        baselineAlreadyDirty[sample] = alreadyDirty;
                });

                // Measure dispatch and mutation first. Component probes run between this phase
                // and the separately measured tick phase; their cost is excluded from both.
                var blockAllocated = GC.GetTotalAllocatedBytes(true);
                var blockCpu = process.TotalProcessorTime;
                var blockStart = Stopwatch.GetTimestamp();
                var failed = false;
                await pair.Server.WaitPost(() =>
                {
                    var entities = pair.Server.EntMan;
                    var damageable = entities.System<DamageableSystem>();
                    var hits = entities.System<SharedHitLocationSystem>();
                    var detach = entities.System<DetachableOrganSystem>();
                    var bodySystem = entities.System<SharedBodySystem>();
                    var injury = new DamageSpecifier { DamageDict = { ["Slash"] = 6, ["Heat"] = 3 } };
                    var healing = new DamageSpecifier { DamageDict = { ["Slash"] = -3, ["Heat"] = -1 } };
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    for (var operation = 0; operation < operationsPerSample; operation++)
                    {
                        var patient = patients[((sample + 2) * operationsPerSample + operation) % patients.Count];
                        if (churn)
                        {
                            var carrier = detach.Detach(patient.Arm);
                            if (carrier is not { } detached)
                            {
                                failed = true;
                                continue;
                            }
                            carriers.Add(detached);
                            if (!bodySystem.AttachPart(patient.Torso, patient.ArmSlot, patient.Arm))
                            {
                                failed = true;
                                continue;
                            }
                            entities.DeleteEntity(detached);
                            carriers.RemoveAt(carriers.Count - 1);
                        }
                        else
                        {
                            hits.SetForcedHit(patient.Body, (operation % 3) switch
                            {
                                0 => BodyPartType.Torso,
                                1 => BodyPartType.Arm,
                                _ => BodyPartType.Leg,
                            });
                            var applied = damageable.TryChangeDamage(patient.Body, injury, ignoreResistances: true);
                            var healed = damageable.TryChangeDamage(patient.Body, healing, ignoreResistances: true);
                            failed |= applied == null || healed == null;
                        }
                    }
                    var elapsed = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    if (sample >= 0)
                    {
                        mutationMilliseconds[sample] = elapsed;
                        mutationAllocations[sample] = allocatedBytes;
                    }
                });
                var dispatchWall = Stopwatch.GetElapsedTime(blockStart).TotalMilliseconds;
                var dispatchCpu = (process.TotalProcessorTime - blockCpu).TotalMilliseconds;
                var dispatchAllocations = GC.GetTotalAllocatedBytes(true) - blockAllocated;

                await pair.Server.WaitAssertion(() =>
                {
                    Assert.That(failed, Is.False, "Every measured mutation must commit.");
                    var changes = CompareComponents(pair.Server.EntMan, patients);
                    if (sample < 0)
                        return;
                    modifiedComponents[sample] = changes.Modified;
                    addedComponents[sample] = changes.Added;
                    removedComponents[sample] = changes.Removed;
                });

                blockAllocated = GC.GetTotalAllocatedBytes(true);
                blockCpu = process.TotalProcessorTime;
                blockStart = Stopwatch.GetTimestamp();
                await pair.RunTicksSync(sampleTicks);
                var tickWall = Stopwatch.GetElapsedTime(blockStart).TotalMilliseconds;
                var tickCpu = (process.TotalProcessorTime - blockCpu).TotalMilliseconds;
                var tickAllocations = GC.GetTotalAllocatedBytes(true) - blockAllocated;
                if (sample < 0)
                    continue;
                hostWallMilliseconds[sample] = dispatchWall + tickWall;
                processCpuMilliseconds[sample] = dispatchCpu + tickCpu;
                processAllocations[sample] = dispatchAllocations + tickAllocations;
                await pair.Server.WaitPost(() => simulatedSeconds[sample] =
                    (pair.Server.ResolveDependency<IGameTiming>().CurTime - simulationStart).TotalSeconds);
            }

            retainedOutstandingWork = GC.GetTotalMemory(true);
            // Match sample zero's pre-mutation population and settling time, not the last
            // sample's extra injury and deferred work. Keep both retained snapshots distinct.
            await pair.Server.WaitPost(() => PreparePopulation(pair.Server.EntMan, patients,
                pair.Server.ResolveDependency<IRobustRandom>(), injuredPercent, churn, Seed + 2));
            await pair.RunTicksSync(PreparationTicks);
            retainedMatchingState = GC.GetTotalMemory(true);
        }
        finally
        {
            await pair.Server.WaitPost(() =>
            {
                foreach (var carrier in carriers)
                    pair.Server.EntMan.DeleteEntity(carrier);
                foreach (var patient in patients)
                {
                    Array.Clear(patient.Baseline);
                    pair.Server.EntMan.DeleteEntity(patient.Body);
                }
            });
        }

        await pair.RunTicksSync(2);
        var retainedAfterCleanup = GC.GetTotalMemory(true);
        TestContext.Out.WriteLine("CMU_MEDICAL_PERF " + JsonSerializer.Serialize(new
        {
            label = Environment.GetEnvironmentVariable("CMU_MEDICAL_PERF_LABEL") ?? "candidate",
            workload = churn ? "detachedSubtreeChurn" : "damageHealingBursts",
            population,
            injuredPercent,
            operationsPerSample,
            seed = Seed,
            samples = Samples,
            requestedSampleSeconds = SampleSeconds,
            sampleTicks,
            preparationTicks = PreparationTicks,
            tickPeriodSeconds = pair.Server.Timing.TickPeriod.TotalSeconds,
            simulatedSeconds,
            totalMeasuredSimulatedSeconds = simulatedSeconds.Sum(),
            runtime = Environment.Version.ToString(),
            processorCount = Environment.ProcessorCount,
            stopwatchFrequency = Stopwatch.Frequency,
            mutationMilliseconds,
            mutationAllocations,
            hostWallMilliseconds,
            processCpuMilliseconds,
            processAllocations,
            modifiedComponents,
            addedComponents,
            removedComponents,
            baselineAlreadyDirty,
            retainedBeforePopulation,
            retainedBeforeSamples,
            retainedOutstandingWork,
            retainedMatchingState,
            retainedAfterCleanup,
            matchingStateRetainedDelta = retainedMatchingState - retainedBeforeSamples,
            mutationQuantilesMs = Quantiles(mutationMilliseconds),
            hostBlockQuantilesMs = Quantiles(hostWallMilliseconds),
            processCpuQuantilesMs = Quantiles(processCpuMilliseconds),
            measurementScope = "Nullspace patients; no viewers. Mutation excludes caller DamageSpecifier construction/probes; host samples sum mutation dispatch and tick phases, excluding component probes and preparation. Component counters compare identity/LastModifiedTick directly around mutation, not subsequent ticks or Dirty calls/bytes. Nonzero baselineAlreadyDirty flags unobservable same-tick redirties. Full-process retained snapshots distinguish outstanding work from matching reset/preinjury state. Quantiles describe 32 operation blocks, not frames.",
        }));
        await pair.CleanReturnAsync();
    }

    private static void PreparePopulation(IEntityManager entities, List<Patient> patients,
        IRobustRandom random, int injuredPercent, bool churn, int seed)
    {
        random.SetSeed(seed);
        var rejuvenate = entities.System<RejuvenateSystem>();
        var damageable = entities.System<DamageableSystem>();
        var hits = entities.System<SharedHitLocationSystem>();
        var injury = new DamageSpecifier { DamageDict = { ["Slash"] = 20, ["Heat"] = 10 } };
        for (var i = 0; i < patients.Count; i++)
        {
            var patient = patients[i];
            rejuvenate.PerformRejuvenate(patient.Body);
            if (i * 100 / patients.Count >= injuredPercent)
                continue;
            hits.SetForcedHit(patient.Body, churn ? BodyPartType.Arm : BodyPartType.Torso);
            damageable.TryChangeDamage(patient.Body, injury, ignoreResistances: true);
        }
    }

    private static int CaptureComponents(IEntityManager entities, List<Patient> patients, GameTick tick)
    {
        var alreadyDirty = 0;
        foreach (var patient in patients)
        {
            var index = 0;
            Capture<DamageableComponent>(entities, patient.Body, patient.Baseline, ref index, tick, ref alreadyDirty);
            foreach (var organ in patient.Organs)
            {
                Capture<BodyPartHealthComponent>(entities, organ, patient.Baseline, ref index, tick, ref alreadyDirty);
                Capture<BodyPartWoundComponent>(entities, organ, patient.Baseline, ref index, tick, ref alreadyDirty);
                Capture<BoneComponent>(entities, organ, patient.Baseline, ref index, tick, ref alreadyDirty);
                Capture<FractureComponent>(entities, organ, patient.Baseline, ref index, tick, ref alreadyDirty);
                Capture<OrganHealthComponent>(entities, organ, patient.Baseline, ref index, tick, ref alreadyDirty);
            }
        }
        return alreadyDirty;
    }

    private static void Capture<T>(IEntityManager entities, EntityUid uid, ComponentStamp[] baseline,
        ref int index, GameTick tick, ref int alreadyDirty) where T : Component
    {
        entities.TryGetComponent<T>(uid, out var component);
        baseline[index++] = new ComponentStamp(component, component?.LastModifiedTick ?? default);
        if (component?.LastModifiedTick == tick)
            alreadyDirty++;
    }

    private static ComponentChanges CompareComponents(IEntityManager entities, List<Patient> patients)
    {
        var changes = new ComponentChanges();
        foreach (var patient in patients)
        {
            var index = 0;
            Compare<DamageableComponent>(entities, patient.Body, patient.Baseline, ref index, ref changes);
            foreach (var organ in patient.Organs)
            {
                Compare<BodyPartHealthComponent>(entities, organ, patient.Baseline, ref index, ref changes);
                Compare<BodyPartWoundComponent>(entities, organ, patient.Baseline, ref index, ref changes);
                Compare<BoneComponent>(entities, organ, patient.Baseline, ref index, ref changes);
                Compare<FractureComponent>(entities, organ, patient.Baseline, ref index, ref changes);
                Compare<OrganHealthComponent>(entities, organ, patient.Baseline, ref index, ref changes);
            }
            // Do not let the observer artificially retain removed/replaced components.
            Array.Clear(patient.Baseline);
        }
        return changes;
    }

    private static void Compare<T>(IEntityManager entities, EntityUid uid, ComponentStamp[] baseline,
        ref int index, ref ComponentChanges changes) where T : Component
    {
        entities.TryGetComponent<T>(uid, out var component);
        var previous = baseline[index++];
        if (!ReferenceEquals(previous.Component, component))
        {
            if (previous.Component != null)
                changes.Removed++;
            if (component != null)
                changes.Added++;
        }
        else if (component != null && component.LastModifiedTick != previous.ModifiedTick)
            changes.Modified++;
    }

    private static double[] Quantiles(double[] values)
    {
        var sorted = values.Order().ToArray();
        return [sorted[(sorted.Length - 1) / 2], sorted[(int)Math.Ceiling(sorted.Length * 0.95) - 1], sorted[(int)Math.Ceiling(sorted.Length * 0.99) - 1]];
    }

    private readonly record struct ComponentStamp(Component? Component, GameTick ModifiedTick);
    private struct ComponentChanges
    {
        public int Modified;
        public int Added;
        public int Removed;
    }
    private sealed record Patient(EntityUid Body, EntityUid Torso, EntityUid Arm, string ArmSlot,
        EntityUid[] Organs, ComponentStamp[] Baseline);
}
