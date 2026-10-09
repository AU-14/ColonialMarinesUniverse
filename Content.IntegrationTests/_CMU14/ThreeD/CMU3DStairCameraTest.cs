using Content.Client.CMU14.ThreeD;
using Content.Client.CMU14.ThreeD.Scene;
using Content.Client.CMU14.ZLevels.Core;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.CMU14.ThreeD;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
public sealed class CMU3DStairCameraTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: cmu3DElevation
          id: CMUTestStairCameraElevation
          regions:
          - bounds: 0, 1, 2, 3
            height: 1.39
          ramps:
          - tile: 0, 0
            direction: 0, 1
            bottom: 0.39
            top: 1.39
        """;

    [Test]
    public async Task ObserverFollowsRaisedTerrainWhilePhysicalCameraRetainsAirborneHeight()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitPost(() => Server.System<SharedMapSystem>().SetTile(
            map.Grid.Owner, map.Grid.Comp, new Vector2i(0, 1), map.Tile.Tile));
        await Pair.RunTicksSync(1);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var grid = ToClientUid(map.Grid);
            CEntMan.EnsureComponent<CMU3DElevationComponent>(grid).Profile = "CMUTestStairCameraElevation";
            var elevation = Client.System<CMU3DElevationSystem>();
            var z = Client.System<CMUClientZLevelsSystem>();
            var transform = Client.System<SharedTransformSystem>();
            var observer = CEntMan.SpawnEntity("MobObserver", new EntityCoordinates(grid, new Vector2(.5f)));
            var body = CEntMan.SpawnEntity(null, new EntityCoordinates(grid, new Vector2(.5f)));
            CEntMan.EnsureComponent<CMUZPhysicsComponent>(body);
            foreach (var (point, ground) in new[] { (new Vector2(.5f), .89f), (new Vector2(.5f, 1.5f), 1.39f) })
            {
                transform.SetCoordinates(observer, new EntityCoordinates(grid, point));
                transform.SetCoordinates(body, new EntityCoordinates(grid, point));
                z.SetZLocalPosition(observer, .1f);
                z.SetZLocalPosition(body, .2f);
                Assert.Multiple(() =>
                {
                    Assert.That(elevation.CameraHeight(observer, 0), Is.EqualTo(ground).Within(.0001f),
                        "The observer must follow the authored ramp and raised landing without retained support height.");
                    Assert.That(elevation.CameraHeight(body, 0), Is.EqualTo(ground + .6f).Within(.0001f),
                        "A physical camera must keep its airborne height above the same terrain.");
                });
            }
        });
    }

    [Test]
    public async Task ObserverReturnsToEyeHeightAfterAscendingAndDescendingStairs()
    {
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid observer = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            lower = maps.CreateMap(runMapInit: true);
            upper = maps.CreateMap(runMapInit: true);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            foreach (var map in new[] { lower, upper })
            {
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                for (var y = -3; y <= 3; y++)
                    maps.SetTile(map, grid, new Vector2i(0, y), map == upper && y == 0 ? Tile.Empty : floor);
            }
            var z = Server.System<CMUZLevelsSystem>();
            Assert.That(z.TryAddMapsIntoZNetwork(z.CreateZNetwork(), new() { [lower] = 0, [upper] = 1 }), Is.True);
            var stair = SEntMan.SpawnEntity("CMUMultiZStairs", new EntityCoordinates(lower, .5f, .5f));
            // The prototype anchors on spawn; anchoring again duplicates its snap-grid entry.
            Assert.That(SComp<TransformComponent>(stair).Anchored, Is.True);
            observer = SEntMan.SpawnEntity("MobObserver", new EntityCoordinates(lower, .5f, -1.5f));
        });

        // Traverse the real sticky support in small steps, then walk clear of its edge support.
        await Walk(-1.5f, .5f, .1f);
        await Client.WaitAssertion(() => Assert.That(
            Client.System<CMU3DElevationSystem>().CameraHeight(ToClientUid(observer), 0),
            Is.EqualTo(2.4375f).Within(.0001f),
            "An observer must still follow the physical stair before reaching its landing."));
        await Walk(.5f, 1.5f, .1f);
        await AssertLanding(upper);
        await Walk(1.5f, -1.5f, -.1f);
        await AssertLanding(lower);

        async Task Walk(float start, float end, float step)
        {
            var count = (int) MathF.Round((end - start) / step);
            for (var i = 1; i <= count; i++)
            {
                var position = new Vector2(.5f, start + i * step);
                await Server.WaitPost(() => Server.System<SharedTransformSystem>().SetWorldPosition(observer, position));
                await Pair.RunTicksSync(1);
            }
            await Pair.RunUntilSynced();
        }

        async Task AssertLanding(EntityUid expectedMap)
        {
            await Server.WaitAssertion(() => Assert.That(SComp<TransformComponent>(observer).MapUid, Is.EqualTo(expectedMap),
                "The route must cross a logical floor through the stair."));
            await Client.WaitAssertion(() =>
            {
                var uid = ToClientUid(observer);
                var elevation = Client.System<CMU3DElevationSystem>();
                var depth = elevation.Depth(CComp<TransformComponent>(uid).MapUid);
                var height = elevation.CameraHeight(uid, depth);
                var camera = CMU3DFirstPersonCamera.Frame(Vector2.Zero, 0, 0, new Vector2(800, 600), groundHeight: height);
                Assert.That(camera.Origin.Z, Is.EqualTo(CMU3DFirstPersonCamera.EyeHeight).Within(.0001f),
                    "Stair support must not leave the observer camera floating above a flat landing.");
            });
        }
    }
}
