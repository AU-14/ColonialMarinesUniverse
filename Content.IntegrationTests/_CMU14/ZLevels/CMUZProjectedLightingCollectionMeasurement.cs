using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using Content.Client.CMU14.ZLevels.Lighting;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Candidate = Content.Client.CMU14.ZLevels.Lighting.CMUZLevelProjectedLightingSystem.ProjectedLightCandidate;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>Measures real aperture search, crossed-floor checks and source rays; excludes rendering and reconciliation.</summary>
[TestFixture, Explicit("Run by exact fixture filter to compare projected-light candidate collection.")]
public sealed class CMUZProjectedLightingCollectionMeasurement : GameTest
{
    [TestCase(0.6f, 1f, true)]
    [TestCase(0.02f, 1f, true)]
    [TestCase(0.02f, 0f, true)]
    [TestCase(0.6f, 1f, false)]
    [TestCase(0.02f, 1f, false)]
    [TestCase(0.02f, 0f, false)]
    public async Task Collect(float minEnergy, float radiusScale, bool diagnostics)
    {
        var report = new List<string>();
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            // The identical fixture also runs against the pre-change assembly, without this CVar.
            const string diagnosticCVar = "cmu.zlevels.client_diagnostics";
            var configurable = config.IsCVarRegistered(diagnosticCVar);
            var previous = configurable && config.GetCVar<bool>(diagnosticCVar);
            try
            {
                if (configurable)
                    config.SetCVar(diagnosticCVar, diagnostics);
                using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
                var source = scene.SpawnLight(0, new Vector2(0.5f));
                scene.Lights.SetRadius(source, 6f);
                var light = CEntMan.GetComponent<PointLightComponent>(source);
                var xform = CEntMan.GetComponent<TransformComponent>(source);
                Assert.That(scene.Lighting.TryBuildSourceLight((source, light, xform), scene.MapIds[0], minEnergy, out var built), Is.True);
                var sources = new List<CMUZLevelProjectedLightingSystem.SourceLight> { built };
                var sourceMap = (scene.Levels[0], CEntMan.GetComponent<CMUZLevelMapComponent>(scene.Levels[0]));
                // FrameUpdate clears this retained list before collecting each frame's contributions.
                var candidates = (List<Candidate>) scene.Lighting.CollectedCandidates;
                void Collect()
                {
                    candidates.Clear();
                    scene.Lighting.CollectCandidates(sources, sourceMap, scene.MapIds[0], scene.Levels[2], scene.MapIds[2],
                        EntityUid.Invalid, MapId.Nullspace, -2, 0.5f, 0.1f, radiusScale, 12f, minEnergy, 0);
                }

                for (var i = 0; i < 512; i++)
                    Collect();

                var stats = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats;
                stats.Reset();
                const int updates = 64;
                var times = new double[17];
                var allocated = new long[17];
                for (var batch = 0; batch < times.Length; batch++)
                {
                    var bytes = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    for (var i = 0; i < updates; i++)
                        Collect();
                    times[batch] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocated[batch] = GC.GetAllocatedBytesForCurrentThread() - bytes;
                }

                report.Add(FormattableString.Invariant($"minEnergy={minEnergy} radiusScale={radiusScale} diagnostics={(!configurable || diagnostics)} updatesPerBatch={updates} candidates={candidates.Count} raycasts={stats.Raycasts}"));
                report.Add("milliseconds=" + string.Join(",", times.Select(t => t.ToString("F4", CultureInfo.InvariantCulture))));
                report.Add("allocatedBytes=" + string.Join(",", allocated));
                report.Add("signature=" + string.Join(";", candidates.OrderBy(c => c.PortalTile.X).ThenBy(c => c.PortalTile.Y)
                    .Select(c => FormattableString.Invariant($"{c.PortalTile.X},{c.PortalTile.Y},{c.ProjectedEnergy:R},{c.ProjectedRadius:R}"))));
                if (minEnergy > 0.5f || radiusScale == 0f)
                    Assert.That(candidates, Is.Empty);
                else
                    Assert.That(candidates, Is.Not.Empty);
            }
            finally
            {
                if (configurable)
                    config.SetCVar(diagnosticCVar, previous);
            }
        });
        foreach (var line in report)
            TestContext.Out.WriteLine(line);
    }
}
