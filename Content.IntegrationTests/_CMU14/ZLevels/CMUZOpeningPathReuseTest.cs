using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Profiling;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZOpeningPathReuseTest : GameTest
{
    private readonly List<EntityUid> _cleanup = new();
    private readonly Dictionary<int, Entity<MapGridComponent>> _levels = new();
    private readonly Vector2i _opening = new(2, 0);
    private EntityUid _camera = EntityUid.Invalid;
    private Tile _floor;
    private Tile _aperture;
    private (bool Enabled, int Depth, int Probes, bool Profiler)? _originalSettings;

    [Test]
    public async Task LowerPathChecksEachFloorOnceAndStopsAtClosedOrMissingFloor()
    {
        try
        {
            await Server.WaitAssertion(() => CreateScenario(3, upperPreview: false));
            await Server.WaitAssertion(() => RefreshAndAssert(new[] { -1, -2, -3 }, 1, 2));

            EntityUid[] oldEyes = [];
            await Server.WaitAssertion(() =>
            {
                oldEyes = Enumerable.ToArray(SComp<CMUZLevelViewerComponent>(_camera).Eyes);
                Server.System<SharedMapSystem>().SetTile(_levels[-1], _opening, _floor);
                RefreshAndAssert(new[] { -1 }, 1, 1);
            });
            await Pair.RunTicksSync(2);
            await Server.WaitAssertion(() =>
            {
                foreach (var eye in oldEyes)
                {
                    Assert.That(SEntMan.Deleted(eye), Is.True);
                    Assert.That(ServerSession!.ViewSubscriptions, Does.Not.Contain(eye));
                }
            });

            await Server.WaitAssertion(() =>
            {
                Server.System<SharedMapSystem>().SetTile(_levels[-1], _opening, _aperture);
                Assert.That(Server.System<CMUZLevelsSystem>().TryRemoveMapFromZNetwork(_levels[-2]), Is.True);
                // The map at -3 remains in the network, but a missing intermediate map must stop traversal.
                RefreshAndAssert(new[] { -1 }, 1, 0);
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    [TestCase(1)]
    [TestCase(3)]
    public async Task StairPreviewReservesUpperProbeBeforeLowerPath(int maxProbes)
    {
        try
        {
            await Server.WaitAssertion(() => CreateScenario(maxProbes, upperPreview: true));
            await Server.WaitAssertion(() =>
            {
                var expected = maxProbes == 1 ? new[] { 1 } : new[] { 1, -1, -2 };
                RefreshAndAssert(expected, maxProbes == 1 ? 0 : 1, maxProbes == 1 ? 0 : 1);
                Assert.That(SComp<CMUZLevelViewerComponent>(_camera).StairPreviewUp, Is.True);
            });
        }
        finally
        {
            await CleanupScenario();
        }
    }

    private void CreateScenario(int maxProbes, bool upperPreview)
    {
        var config = Server.CfgMan;
        _originalSettings = (
            config.GetCVar(CMUZLevelsCVars.Enabled),
            config.GetCVar(CMUZLevelsCVars.MaxRenderDepth),
            config.GetCVar(CMUZLevelsCVars.MaxViewProbesPerPlayer),
            config.GetCVar(Robust.Shared.CVars.ProfEnabled));
        config.SetCVar(CMUZLevelsCVars.Enabled, true);
        config.SetCVar(CMUZLevelsCVars.MaxRenderDepth, 3);
        config.SetCVar(CMUZLevelsCVars.MaxViewProbesPerPlayer, maxProbes);

        var tiles = Server.ResolveDependency<ITileDefinitionManager>();
        _floor = new Tile(tiles["Plating"].TileId);
        _aperture = new Tile(tiles["Lattice"].TileId);
        var maps = Server.System<SharedMapSystem>();
        for (var depth = -3; depth <= (upperPreview ? 1 : 0); depth++)
        {
            var map = maps.CreateMap(runMapInit: true);
            _cleanup.Add(map);
            var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
            _levels.Add(depth, (map, grid));
            // Cover the entire production query radius so empty map edges cannot bypass a closed floor.
            for (var x = -25; x <= 25; x++)
            for (var y = -25; y <= 25; y++)
                maps.SetTile(map, grid, new Vector2i(x, y), _floor);
            if (depth <= 0)
                maps.SetTile(map, grid, _opening, _aperture);
        }

        var zLevels = Server.System<CMUZLevelsSystem>();
        var network = zLevels.CreateZNetwork();
        _cleanup.Add(network.Owner);
        Assert.That(zLevels.TryAddMapsIntoZNetwork(network,
            _levels.ToDictionary(level => level.Value.Owner, level => level.Key)), Is.True);

        if (upperPreview)
            SEntMan.SpawnEntity("CMUMultiZStairs", new EntityCoordinates(_levels[0], 0.5f, -0.5f));

        _camera = SEntMan.SpawnEntity(null, new EntityCoordinates(_levels[0], 0.5f, 0.5f));
        _cleanup.Add(_camera);
        SEntMan.EnsureComponent<EyeComponent>(_camera);
        Server.System<ViewSubscriberSystem>().AddViewSubscriber(_camera, ServerSession!);
        config.SetCVar(Robust.Shared.CVars.ProfEnabled, true);
    }

    private void RefreshAndAssert(int[] expectedDepths, int visibleChecks, int nearbyChecks)
    {
        var profiler = Server.ResolveDependency<ProfManager>();
        Assert.That(profiler.IsEnabled, Is.True);
        var start = profiler.Buffer.LogWriteOffset;
        Server.System<CMUZLevelsSystem>().RefreshZLevelViewer(_camera);
        var end = profiler.Buffer.LogWriteOffset;
        Assert.That(end - start, Is.LessThanOrEqualTo(profiler.Buffer.LogBuffer.LongLength),
            "The synchronous refresh must fit in the profiler buffer for exact query counts.");

        var groups = new Dictionary<string, int>();
        for (var i = start; i < end; i++)
        {
            var entry = profiler.Buffer.Log(i);
            if (entry.Type != ProfLogType.GroupEnd)
                continue;
            var name = profiler.GetString(entry.GroupEnd.StringId);
            groups[name] = groups.GetValueOrDefault(name) + 1;
        }

        Assert.Multiple(() =>
        {
            Assert.That(groups.GetValueOrDefault("CMU Z PVS VisibleOpening"), Is.EqualTo(visibleChecks),
                "The viewer's expensive first-floor visibility query must not repeat for deeper floors.");
            Assert.That(groups.GetValueOrDefault("CMU Z PVS FindOpeningCenters"), Is.EqualTo(visibleChecks));
            Assert.That(groups.GetValueOrDefault("CMU Z PVS OpeningNear"), Is.EqualTo(nearbyChecks),
                "Each traversed lower floor must be checked once, stopping at a closed or missing floor.");
        });

        var eyes = SComp<CMUZLevelViewerComponent>(_camera).Eyes;
        var expectedMaps = expectedDepths.Select(depth => (EntityUid?) _levels[depth].Owner).ToArray();
        Assert.That(eyes.Select(eye => SComp<TransformComponent>(eye).MapUid), Is.EquivalentTo(expectedMaps));
        foreach (var eye in eyes)
            Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(eye));
    }

    private async Task CleanupScenario()
    {
        await Server.WaitPost(() =>
        {
            if (SEntMan.EntityExists(_camera))
                Server.System<ViewSubscriberSystem>().RemoveViewSubscriber(_camera, ServerSession!);
            if (_originalSettings is not { } settings)
                return;
            Server.CfgMan.SetCVar(Robust.Shared.CVars.ProfEnabled, settings.Profiler);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, settings.Enabled);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.MaxRenderDepth, settings.Depth);
            Server.CfgMan.SetCVar(CMUZLevelsCVars.MaxViewProbesPerPlayer, settings.Probes);
        });
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
    }
}
