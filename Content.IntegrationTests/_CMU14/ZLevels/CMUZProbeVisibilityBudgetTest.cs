using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Examine;
using Content.Shared.CMU14.ZLevels;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZProbeVisibilityBudgetTest : GameTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task UncheckedApertureRetainsProbeUntilFloorCloses(bool addUncheckedVisibleAperture)
    {
        var cleanup = new List<EntityUid>();
        var openings = new List<Vector2i>();
        var camera = EntityUid.Invalid;
        var lower = EntityUid.Invalid;
        var upper = EntityUid.Invalid;
        MapGridComponent upperGrid = default!;
        var floor = Tile.Empty;
        bool? originalEnabled = null;
        EntityUid[] previousEyes = [];
        try
        {
            await Server.WaitAssertion(() =>
            {
                originalEnabled = Server.CfgMan.GetCVar(CMUZLevelsCVars.Enabled);
                Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, true);
                var maps = Server.System<SharedMapSystem>();
                var zLevels = Server.System<CMUZLevelsSystem>();
                var tiles = Server.ResolveDependency<ITileDefinitionManager>();
                lower = maps.CreateMap(runMapInit: true);
                cleanup.Add(lower);
                upper = maps.CreateMap(out var upperId, runMapInit: true);
                cleanup.Add(upper);
                upperGrid = SEntMan.EnsureComponent<MapGridComponent>(upper);
                floor = new Tile(tiles["Plating"].TileId);
                var aperture = new Tile(tiles["Lattice"].TileId);
                // No incidental map-edge openings lie within the production 24-tile search radius.
                for (var x = -25; x <= 25; x++)
                for (var y = -25; y <= 25; y++)
                    maps.SetTile(upper, upperGrid, new Vector2i(x, y), floor);

                for (var x = 4; x <= 10; x += 2)
                for (var y = -5; y <= 5; y += 2)
                    openings.Add(new Vector2i(x, y));
                Assert.That(openings, Has.Count.EqualTo(24));
                if (addUncheckedVisibleAperture)
                    openings.Add(new Vector2i(-15, 0));
                foreach (var tile in openings)
                    maps.SetTile(upper, upperGrid, tile, aperture);

                var blocker = SEntMan.SpawnEntity(null, new EntityCoordinates(upper, new Vector2(2.5f, 0.5f)));
                SEntMan.AddComponent<OccluderComponent>(blocker);
                Server.System<ServerOccluderSystem>().SetPolygon(blocker,
                [
                    new Vector2(-0.5f, 8f), new Vector2(0.5f, 8f),
                    new Vector2(0.5f, -8f), new Vector2(-0.5f, -8f),
                ]);

                var network = zLevels.CreateZNetwork();
                cleanup.Add(network.Owner);
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);
                var position = new Vector2(0.5f, 0.5f);
                camera = SEntMan.SpawnEntity(null, new EntityCoordinates(upper, position));
                cleanup.Add(camera);
                SEntMan.EnsureComponent<EyeComponent>(camera);

                // Validate the adversarial scene through the actual geometry and occlusion APIs,
                // then exercise probe creation through its public subscription lifecycle.
                var candidates = new List<(Vector2 Center, float Distance)>();
                var grids = new List<Entity<MapGridComponent>>();
                new CMUZLevelOpeningCache().FindOpeningCentersNear(upperId, position, 24f, candidates,
                    grids, maps, Server.System<SharedTransformSystem>(), tiles);
                Assert.That(candidates, Has.Count.EqualTo(openings.Count));
                var origin = new MapCoordinates(position, upperId);
                var examine = Server.System<ExamineSystem>();
                Assert.That(candidates.Count(candidate => candidate.Distance < 15f), Is.EqualTo(24));
                foreach (var candidate in candidates)
                {
                    var visible = examine.InRangeUnOccluded(origin, new MapCoordinates(candidate.Center, upperId), 0f, null);
                    Assert.That(visible, Is.EqualTo(candidate.Center.X < 0f), $"Aperture at {candidate.Center}");
                }

                Server.System<ViewSubscriberSystem>().AddViewSubscriber(camera, ServerSession!);
            });

            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
            {
                var viewer = SComp<CMUZLevelViewerComponent>(camera);
                var eyes = viewer.Eyes;
                previousEyes = eyes.ToArray();
                Assert.That(previousEyes.Length, Is.EqualTo(addUncheckedVisibleAperture ? 1 : 0));
                foreach (var eye in previousEyes)
                {
                    Assert.That(SComp<TransformComponent>(eye).MapUid, Is.EqualTo(lower));
                    Assert.That(ServerSession!.ViewSubscriptions, Does.Contain(eye));
                }

                var maps = Server.System<SharedMapSystem>();
                foreach (var tile in openings)
                    maps.SetTile(upper, upperGrid, tile, floor);
            });

            await Pair.RunTicksSync(20);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SComp<CMUZLevelViewerComponent>(camera).Eyes, Is.Empty,
                    "A complete closed floor must still release the conservative lower subscription.");
                foreach (var eye in previousEyes)
                {
                    Assert.That(SEntMan.Deleted(eye), Is.True);
                    Assert.That(ServerSession!.ViewSubscriptions, Does.Not.Contain(eye));
                }
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(camera))
                    Server.System<ViewSubscriberSystem>().RemoveViewSubscriber(camera, ServerSession!);
                if (originalEnabled is { } enabled)
                    Server.CfgMan.SetCVar(CMUZLevelsCVars.Enabled, enabled);
            });
            for (var i = cleanup.Count - 1; i >= 0; i--)
                await Pair.DeleteEntityTreeLeafFirst(cleanup[i]);
        }
    }
}
