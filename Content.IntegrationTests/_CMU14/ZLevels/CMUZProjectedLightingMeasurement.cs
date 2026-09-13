using System.Diagnostics;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Map;
using Candidate = Content.Client.CMU14.ZLevels.Lighting.CMUZLevelProjectedLightingSystem.ProjectedLightCandidate;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>Opt-in ECS reconciliation measurement. Excludes discovery, source LOS, render submission and GPU work.</summary>
[TestFixture, Explicit("Run by exact fixture filter to measure projected-light reconciliation allocation and entity churn.")]
public sealed class CMUZProjectedLightingMeasurement : GameTest
{
    [TestCase(1, false)]
    [TestCase(16, true)]
    [TestCase(64, true)]
    public async Task Reconcile(int candidateCount, bool movingGrid)
    {
        var report = new List<string>();
        await Client.WaitAssertion(() =>
        {
            using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
            var source = scene.SpawnLight(2, new Vector2(0.5f));
            var portal = scene.Maps.CreateGridEntity(scene.MapIds[2]);
            scene.Maps.SetTile(portal, new Vector2i(9, 9), scene.Floor);
            var candidates = new List<Candidate>(candidateCount);
            for (var i = 0; i < candidateCount; i++)
            {
                var tile = new Vector2i(i % 8 - 4, i / 8 - 4);
                candidates.Add(scene.Candidate(source, 2, i % 2, portal, tile, tile + new Vector2(0.5f)));
            }

            uint frame = 0;
            void Update()
            {
                if (movingGrid)
                {
                    var position = new Vector2(MathF.Sin(frame * 0.01f) * 0.2f, 0f);
                    scene.Transform.SetCoordinates(portal, new EntityCoordinates(scene.Levels[2], position));
                    for (var i = 0; i < candidates.Count; i++)
                    {
                        var candidate = candidates[i];
                        var center = candidate.PortalTile + new Vector2(0.5f) + position;
                        candidates[i] = candidate with { OpeningCenter = center, ProjectedCenter = center };
                    }
                }

                scene.Lighting.ReconcileProjectedLights(candidates, 16, 32, frame++, 0.18f);
            }

            for (var i = 0; i < 512; i++)
                Update();

            var created = scene.Lighting.TotalCreated;
            var deleted = scene.Lighting.TotalDeleted;
            var reassigned = scene.Lighting.TotalReassigned;
            const int frames = 256;
            var times = new double[17];
            var allocated = new long[17];
            for (var batch = 0; batch < times.Length; batch++)
            {
                var before = GC.GetAllocatedBytesForCurrentThread();
                var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < frames; i++)
                    Update();
                times[batch] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                allocated[batch] = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            report.Add($"candidates={candidateCount} movingGrid={movingGrid} framesPerBatch={frames} live={scene.Projected().Count}");
            report.Add($"created={scene.Lighting.TotalCreated - created} deleted={scene.Lighting.TotalDeleted - deleted} reassigned={scene.Lighting.TotalReassigned - reassigned}");
            report.Add("milliseconds=" + string.Join(",", times.Select(t => t.ToString("F4", System.Globalization.CultureInfo.InvariantCulture))));
            report.Add("allocatedBytes=" + string.Join(",", allocated));
            Assert.That(scene.Lighting.TotalCreated, Is.EqualTo(created));
            Assert.That(scene.Lighting.TotalDeleted, Is.EqualTo(deleted));
        });
        foreach (var line in report)
            TestContext.Out.WriteLine(line);
    }
}
