using System.Diagnostics;
using System.Globalization;
using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Client.GameObjects;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// Opt-in headless CPU/allocation measurement of candidate discovery and membership maintenance.
/// The reference reproduces the removed four-query discovery pass against the same live ECS.
/// This does not measure world rendering, GPU work, or whole-client frame time.
/// </summary>
[TestFixture, Explicit("Run by exact fixture filter for presentation candidate measurements.")]
public sealed class CMUZPresentationActivityMeasurement : GameTest
{
    [TestCase(0, 0)]
    [TestCase(4096, 0)]
    [TestCase(4096, 32)]
    [TestCase(16384, 32)]
    public async Task PresentationCandidates(int grounded, int active)
    {
        var report = new List<string>();
        await Client.WaitAssertion(() =>
        {
            var z = Client.System<CMUClientZLevelsSystem>();
            var initialCount = z.PresentationCandidateCount;
            var entities = new List<EntityUid>(grounded + active);
            var elevated = new List<EntityUid>();
            var referenceCandidates = new HashSet<EntityUid>();
            try
            {
                for (var i = 0; i < grounded + active; i++)
                {
                    var uid = CEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
                    entities.Add(uid);
                    CEntMan.AddComponent<CMUZPhysicsComponent>(uid);
                    CEntMan.AddComponent<SpriteComponent>(uid);
                    if (i < grounded)
                        continue;

                    switch ((i - grounded) % 4)
                    {
                        case 0:
                            z.SetZLocalPosition(uid, 0.5f);
                            elevated.Add(uid);
                            break;
                        case 1:
                            CEntMan.AddComponent<CMUZLevelProjectileVisualOffsetComponent>(uid).Offset = new Vector2(0f, 0.5f);
                            break;
                        case 2:
                            CEntMan.AddComponent<CMUZLevelPredictedProjectileVisualOffsetComponent>(uid).Offset = new Vector2(0f, 0.5f);
                            break;
                        case 3:
                            CEntMan.AddComponent<CMUZVisualFollowerComponent>(uid).Target = elevated[^1];
                            break;
                    }
                }

                void DiscoverReference()
                {
                    referenceCandidates.Clear();
                    var physics = CEntMan.EntityQueryEnumerator<CMUZPhysicsComponent, SpriteComponent>();
                    while (physics.MoveNext(out var uid, out var component, out _))
                    {
                        if (component.LocalPosition != 0f)
                            referenceCandidates.Add(uid);
                    }

                    var projectiles = CEntMan.EntityQueryEnumerator<CMUZLevelProjectileVisualOffsetComponent, SpriteComponent>();
                    while (projectiles.MoveNext(out var uid, out _, out _))
                        referenceCandidates.Add(uid);

                    var predicted = CEntMan.EntityQueryEnumerator<CMUZLevelPredictedProjectileVisualOffsetComponent, SpriteComponent>();
                    while (predicted.MoveNext(out var uid, out _, out _))
                        referenceCandidates.Add(uid);

                    var followers = CEntMan.EntityQueryEnumerator<CMUZVisualFollowerComponent, SpriteComponent>();
                    while (followers.MoveNext(out var uid, out _, out _))
                        referenceCandidates.Add(uid);
                }

                void UpdateIndexed() => z.FrameUpdate(0f);

                void ChangeHeightReasons()
                {
                    foreach (var uid in elevated)
                    {
                        z.SetZLocalPosition(uid, 0f);
                        z.SetZLocalPosition(uid, 0.5f);
                    }
                }

                DiscoverReference();
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(initialCount + active));
                Assert.That(referenceCandidates.Count, Is.EqualTo(z.PresentationCandidateCount));
                report.Add($"grounded={grounded} active={active} indexedCandidates={z.PresentationCandidateCount} heightChangesPerChurnCall={elevated.Count * 2}");
                RecordSeries("reference-four-query-discovery", DiscoverReference, report);
                RecordSeries("indexed-frame-update", UpdateIndexed, report);
                if (elevated.Count > 0)
                    RecordSeries("height-reason-churn", ChangeHeightReasons, report);
                Assert.That(z.PresentationCandidateCount, Is.EqualTo(initialCount + active));
            }
            finally
            {
                foreach (var uid in entities)
                    CEntMan.DeleteEntity(uid);
            }
            Assert.That(z.PresentationCandidateCount, Is.EqualTo(initialCount));
        });

        // The game-loop thread is pooled across cases and can retain an earlier NUnit context.
        foreach (var line in report)
            TestContext.Out.WriteLine(line);
    }

    private static void RecordSeries(string scenario, Action action, List<string> report)
    {
        for (var i = 0; i < 256; i++)
            action();

        const int calls = 512;
        var elapsed = new double[17];
        var allocated = new long[elapsed.Length];
        var collections = new int[elapsed.Length, 3];
        for (var batch = 0; batch < elapsed.Length; batch++)
        {
            var generation0 = GC.CollectionCount(0);
            var generation1 = GC.CollectionCount(1);
            var generation2 = GC.CollectionCount(2);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            for (var i = 0; i < calls; i++)
                action();
            elapsed[batch] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
            collections[batch, 0] = GC.CollectionCount(0) - generation0;
            collections[batch, 1] = GC.CollectionCount(1) - generation1;
            collections[batch, 2] = GC.CollectionCount(2) - generation2;
        }

        report.Add($"scenario={scenario} callsPerBatch={calls}");
        report.Add("milliseconds=" + string.Join(",", elapsed.Select(value => value.ToString("F4", CultureInfo.InvariantCulture))));
        report.Add("allocatedBytes=" + string.Join(",", allocated));
        for (var generation = 0; generation < 3; generation++)
        {
            var collectionGeneration = generation;
            report.Add($"processGen{generation}Collections=" +
                string.Join(",", Enumerable.Range(0, elapsed.Length).Select(batch => collections[batch, collectionGeneration])));
        }
    }
}
