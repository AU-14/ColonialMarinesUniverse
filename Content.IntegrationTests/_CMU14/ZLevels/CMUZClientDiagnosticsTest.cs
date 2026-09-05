using System.Numerics;
using Content.Client.CMU14.ZLevels.Lighting;
using Content.Client.Viewport;
using Content.IntegrationTests.Fixtures;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Moq;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Candidate = Content.Client.CMU14.ZLevels.Lighting.CMUZLevelProjectedLightingSystem.ProjectedLightCandidate;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZClientDiagnosticsTest : GameTest
{
    [TestCase(0.6f, 1f)]
    [TestCase(0.02f, 0f)]
    public async Task IneffectiveContributionsAvoidWorldQueries(float minEnergy, float radiusScale)
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var previous = config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
            try
            {
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, true);
                using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
                var source = scene.SpawnLight(0, new Vector2(0.5f));
                scene.Lights.SetRadius(source, 4f);
                var light = CEntMan.GetComponent<PointLightComponent>(source);
                var xform = CEntMan.GetComponent<TransformComponent>(source);
                Assert.That(scene.Lighting.TryBuildSourceLight((source, light, xform), scene.MapIds[0], minEnergy, out var built), Is.True);
                var stats = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats;
                stats.Reset();
                scene.Lighting.CollectCandidates(new() { built },
                    (scene.Levels[0], CEntMan.GetComponent<CMUZLevelMapComponent>(scene.Levels[0])), scene.MapIds[0],
                    scene.Levels[2], scene.MapIds[2], EntityUid.Invalid, MapId.Nullspace, -2, 0.5f, 0.1f, radiusScale, 12f, minEnergy, 0);
                Assert.Multiple(() =>
                {
                    Assert.That(stats.OpeningsFound, Is.GreaterThan(1), "This must exercise real apertures, not an empty search.");
                    Assert.That(stats.TransmissionChecks, Is.Zero);
                    Assert.That(stats.Raycasts, Is.Zero);
                    Assert.That(scene.Lighting.CollectedCandidates, Is.Empty);
                });
            }
            finally
            {
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, previous);
            }
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task DiagnosticTogglePreservesCompleteAndBudgetLimitedLighting(int openingLimit)
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var previous = config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
            try
            {
                using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
                for (var x = -4; x <= 4; x++)
                for (var y = -4; y <= 4; y++)
                    scene.Maps.SetTile(scene.Levels[2], scene.Grids[2], new Vector2i(x, y), scene.Floor);
                scene.Maps.SetTile(scene.Levels[2], scene.Grids[2], new Vector2i(-2, 0), Tile.Empty);
                scene.Maps.SetTile(scene.Levels[2], scene.Grids[2], new Vector2i(2, 0), Tile.Empty);
                var source = scene.SpawnLight(0, new Vector2(0.5f));
                scene.Lights.SetRadius(source, 4f);
                var light = CEntMan.GetComponent<PointLightComponent>(source);
                var xform = CEntMan.GetComponent<TransformComponent>(source);
                Assert.That(scene.Lighting.TryBuildSourceLight((source, light, xform), scene.MapIds[0], 0.01f, out var built), Is.True);
                var bounds = new Box2(-3.9f, -3.9f, 3.9f, 3.9f);
                var candidates = (List<Candidate>) scene.Lighting.CollectedCandidates;
                Candidate[] expected = null;
                foreach (var diagnostics in new[] { true, false, true })
                {
                    config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, diagnostics);
                    var stats = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats;
                    var previousBuilds = stats.PortalLightQueryBuilds;
                    var previousRays = stats.Raycasts;
                    var previousChecks = stats.TransmissionChecks;
                    Assert.That(scene.Lighting.TryUpdateCurrentViewOpenings(scene.MapIds[2], bounds, Vector2.Zero, openingLimit, 0f), Is.True);
                    scene.Lighting.BuildSourceLightBuckets(new Box2Rotated(bounds, Angle.Zero), 0.01f,
                        (scene.Levels[2], CEntMan.GetComponent<CMUZLevelMapComponent>(scene.Levels[2])),
                        CEntMan.GetComponent<MapComponent>(scene.Levels[2]), 2, 32, true, true, false, Array.Empty<int>());
                    candidates.Clear();
                    scene.Lighting.CollectCandidates(new() { built },
                        (scene.Levels[0], CEntMan.GetComponent<CMUZLevelMapComponent>(scene.Levels[0])), scene.MapIds[0],
                        scene.Levels[2], scene.MapIds[2], scene.Levels[2], scene.MapIds[2], -2, 0.5f, 0.1f, 1f, 12f, 0.01f, 0);
                    Assert.That(candidates.Select(c => c.PortalTile), Is.EquivalentTo(new[] { new Vector2i(-2, 0), new Vector2i(2, 0) }));
                    expected ??= candidates.ToArray();
                    Assert.That(candidates, Is.EqualTo(expected), "Diagnostics must not own aperture completeness or lighting output.");
                    if (diagnostics)
                    {
                        Assert.That(stats.SourceQueries, Is.EqualTo(3));
                        Assert.That(stats.PortalLightQueryBuilds, Is.EqualTo(openingLimit == 0 ? 1 : 0));
                        Assert.That(stats.TransmissionChecks, Is.GreaterThan(0));
                    }
                    else
                    {
                        Assert.That(stats.PortalLightQueryBuilds, Is.EqualTo(previousBuilds));
                        Assert.That(stats.Raycasts, Is.EqualTo(previousRays));
                        Assert.That(stats.TransmissionChecks, Is.EqualTo(previousChecks));
                    }
                }

                // A subsequent update must rebuild geometry after a same-tick floor edit.
                scene.Maps.SetTile(scene.Levels[2], scene.Grids[2], new Vector2i(-2, 0), scene.Floor);
                var buildsBeforeEdit = CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats.PortalLightQueryBuilds;
                Assert.That(scene.Lighting.TryUpdateCurrentViewOpenings(scene.MapIds[2], bounds, Vector2.Zero, 0, 0f), Is.True);
                scene.Lighting.BuildSourceLightBuckets(new Box2Rotated(bounds, Angle.Zero), 0.01f,
                    (scene.Levels[2], CEntMan.GetComponent<CMUZLevelMapComponent>(scene.Levels[2])),
                    CEntMan.GetComponent<MapComponent>(scene.Levels[2]), 2, 32, true, true, false, Array.Empty<int>());
                Assert.That(CMUZLevelProjectedLightingSystem.LastProjectedLightingDebugStats.PortalLightQueryBuilds, Is.EqualTo(buildsBeforeEdit + 1));
                candidates.Clear();
                scene.Lighting.CollectCandidates(new() { built },
                    (scene.Levels[0], CEntMan.GetComponent<CMUZLevelMapComponent>(scene.Levels[0])), scene.MapIds[0],
                    scene.Levels[2], scene.MapIds[2], scene.Levels[2], scene.MapIds[2], -2, 0.5f, 0.1f, 1f, 12f, 0.01f, 0);
                Assert.That(candidates.Select(c => c.PortalTile), Is.EquivalentTo(new[] { new Vector2i(2, 0) }));
            }
            finally
            {
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, previous);
            }
        });
    }

    [Test]
    public async Task DisabledDiagnosticsStillRemoveLightsWhenBudgetBecomesZero()
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var previous = config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
            try
            {
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, false);
                using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
                var source = scene.SpawnLight(2, new Vector2(0.5f));
                var candidates = new List<Candidate> { scene.Candidate(source, 2, 1, scene.Levels[2], Vector2i.Zero, new Vector2(0.5f)) };
                scene.Lighting.ReconcileProjectedLights(candidates, 1, 1, 1, 1f);
                var projected = scene.Projected().Single();
                scene.Lighting.ReconcileProjectedLights(candidates, 0, 0, 2, 1f);
                Assert.That(CEntMan.Deleted(projected), Is.True);
                Assert.That(scene.Projected(), Is.Empty);
            }
            finally
            {
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, previous);
            }
        });
    }

    [Test]
    public async Task ViewportPassesAndOffsetsSurviveDiagnosticToggles()
    {
        await Client.WaitAssertion(() =>
        {
            var config = Client.ResolveDependency<IConfigurationManager>();
            var previous = config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
            try
            {
                using var scene = new CMUZProjectedLightingScene(CEntMan, Client.ResolveDependency<ITileDefinitionManager>());
                var viewer = CEntMan.SpawnEntity(null, new MapCoordinates(Vector2.Zero, scene.MapIds[2]));
                CEntMan.AddComponent<CMUZLevelViewerComponent>(viewer);
                var eye = CEntMan.AddComponent<EyeComponent>(viewer).Eye;
                eye.Position = new MapCoordinates(Vector2.Zero, scene.MapIds[2]);
                eye.Rotation = Angle.FromDegrees(37);
                using var control = new ScalingViewport { Eye = eye };
                var viewport = new Mock<IClydeViewport>();
                viewport.SetupProperty(v => v.Eye, eye);
                viewport.SetupProperty(v => v.ClearColor, Color.Red);
                viewport.SetupGet(v => v.Size).Returns(new Vector2i(256, 256));
                viewport.Setup(v => v.LocalToWorld(It.IsAny<Vector2>())).Returns((Vector2 pixel) =>
                    new MapCoordinates((pixel - new Vector2(128f)) / 32f + viewport.Object.Eye!.Offset, viewport.Object.Eye.Position.MapId));
                var passes = new List<(MapId Map, Vector2 Offset)>();
                viewport.Setup(v => v.Render()).Callback(() => passes.Add((viewport.Object.Eye!.Position.MapId, viewport.Object.Eye.Offset)));
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, true);
                control.RenderZLevelPasses(viewport.Object);
                var expected = passes.ToArray();
                Assert.That(expected.Select(p => p.Map), Is.EqualTo(scene.MapIds));
                var stats = ScalingViewport.LastZRenderDebugStats;
                var sequence = stats.Sequence;
                var depths = stats.LowerRenderedDepths.ToArray();
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, false);
                for (var frame = 0; frame < 20; frame++)
                {
                    passes.Clear();
                    control.RenderZLevelPasses(viewport.Object);
                    Assert.That(passes, Is.EqualTo(expected));
                    Assert.That(stats.Sequence, Is.EqualTo(sequence));
                    Assert.That(stats.LowerRenderedDepths, Is.EqualTo(depths), "Disabled diagnostics must not accumulate depth entries.");
                    Assert.That(viewport.Object.Eye, Is.SameAs(eye));
                    Assert.That(viewport.Object.ClearColor, Is.EqualTo(Color.Red));
                }
            }
            finally
            {
                config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, previous);
            }
        });
    }
}
