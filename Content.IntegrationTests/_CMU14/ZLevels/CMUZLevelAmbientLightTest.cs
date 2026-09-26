using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Lighting;
using Content.Server.GameTicking;
using Content.Shared._NC14.DayNightCycle;
using Content.Shared._RMC14.Light;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels;

[TestFixture]
public sealed class CMUZLevelAmbientLightTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
- type: gameMap
  id: CMUZLightingTestMap
  mapName: Z lighting regression
  mapPath: /Maps/Test/empty.yml
  minPlayers: 0
  maxPlayers: 100
  mapsBelow:
  - /Maps/Test/empty.yml
  mapsAbove:
  - /Maps/Test/empty.yml
  - /Maps/Test/empty.yml
  zLevelsComponentOverrides:
  - type: DayNightCycle
  - type: RMCAmbientLight
  - type: MapLight
  stations:
    Empty:
      stationProto: StandardNanotrasenStation
      components:
      - type: StationNameSetup
        mapNameTemplate: Z lighting regression
""";

    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task LoadedFloorsFollowGroundLightingAndKeepRoofShadows(bool animate)
    {
        EntityUid ground = default;
        EntityUid[] floors = [];
        NetEntity[] netFloors = [];
        var initial = new Color(0.15f, 0.25f, 0.35f);
        var final = new Color(0.75f, 0.65f, 0.55f);
        Entity<MapGridComponent, RoofComponent> roofGrid = default;
        var covered = Vector2i.Zero;
        var exposed = new Vector2i(1, 0);

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            Server.System<GameTicker>().LoadGameMap(SProtoMan.Index<GameMapPrototype>("CMUZLightingTestMap"),
                out var mapId, DeserializationOptions.Default with { InitializeMaps = false });
            maps.InitializeMap(mapId);
            ground = maps.GetMap(mapId);
            var networkId = SEntMan.GetComponent<CMUZLevelMapComponent>(ground).NetworkUid;
            var network = SEntMan.GetComponent<CMUZLevelsNetworkComponent>(networkId);
            floors = network.ZLevels.Values.Select(uid => uid!.Value).ToArray();
            netFloors = floors.Select(uid => SEntMan.GetNetEntity(uid)).ToArray();
            Assert.That(floors, Has.Length.EqualTo(4));
            Assert.That(SEntMan.HasComponent<DayNightCycleComponent>(ground), Is.True,
                "Loading extra floors must preserve the ground map's own lighting controller.");

            foreach (var floor in floors.Where(uid => uid != ground))
            {
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<DayNightCycleComponent>(floor), Is.False,
                        "Saved per-floor cycles must not override the planet sky.");
                    Assert.That(SEntMan.HasComponent<RMCAmbientLightComponent>(floor), Is.False);
                    Assert.That(SEntMan.GetComponent<CMUZLevelAmbientLightComponent>(floor).Source, Is.EqualTo(ground));
                });
            }

            SEntMan.RemoveComponent<DayNightCycleComponent>(ground);
            maps.SetAmbientLight(mapId, initial);
            if (animate)
            {
                var controller = SEntMan.GetComponent<RMCAmbientLightComponent>(ground);
                Server.System<RMCAmbientLightSystem>().SetColor((ground, controller), final, TimeSpan.FromSeconds(1));
            }

            var top = network.ZLevels[2]!.Value;
            var grid = maps.GetAllGrids(SEntMan.GetComponent<MapComponent>(top).MapId).Single();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            maps.SetTile(grid, covered, new Tile(tiles["Plating"].TileId));
            maps.SetTile(grid, exposed, new Tile(tiles["Plating"].TileId));
            var roof = SEntMan.EnsureComponent<RoofComponent>(grid);
            roofGrid = (grid.Owner, grid.Comp, roof);
            Server.System<SharedRoofSystem>().SetRoof(roofGrid, covered, true);
        });

        await Pair.RunTicksSync(10);
        Color expected = default;
        await Server.WaitAssertion(() =>
        {
            expected = SEntMan.GetComponent<MapLightComponent>(ground).AmbientLightColor;
            Assert.That(expected, animate ? Is.Not.EqualTo(initial) : Is.EqualTo(initial));
            Assert.That(expected, Is.Not.EqualTo(final), "The animation must still be in progress.");
            foreach (var floor in floors)
                Assert.That(SEntMan.GetComponent<MapLightComponent>(floor).AmbientLightColor, Is.EqualTo(expected));

            var roofs = Server.System<SharedRoofSystem>();
            Assert.That(roofs.GetColor(roofGrid, covered), Is.EqualTo(Color.Black), "Indoor roof shading remains intact.");
            Assert.That(roofs.GetColor(roofGrid, exposed), Is.Null, "Exposed rooftop tiles retain sky light.");
        });

        await Pair.RunTicksSync(90);
        await Server.WaitAssertion(() =>
        {
            expected = animate ? final : initial;
            foreach (var floor in floors)
                Assert.That(SEntMan.GetComponent<MapLightComponent>(floor).AmbientLightColor, Is.EqualTo(expected),
                    "The completed transition or fixed night must agree across every floor.");
        });
        await Client.WaitAssertion(() =>
        {
            foreach (var floor in netFloors)
                Assert.That(CEntMan.GetComponent<MapLightComponent>(CEntMan.GetEntity(floor)).AmbientLightColor,
                    Is.EqualTo(expected), "Clients must receive the same sky on every floor.");
        });
    }

    [Test]
    public async Task GroundDayNightCycleDrivesAdditionalFloors()
    {
        EntityUid source = default;
        EntityUid follower = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            source = maps.CreateMap(runMapInit: true);
            follower = maps.CreateMap(runMapInit: true);
            SEntMan.EnsureComponent<MapLightComponent>(source);
            SEntMan.EnsureComponent<DayNightCycleComponent>(source).CurrentCycleTime = 0.5f;
            SEntMan.EnsureComponent<DayNightCycleComponent>(follower).CurrentCycleTime = 0.9f;
            Server.System<CMUZLevelAmbientLightSystem>().FollowMap(follower, source);
        });

        await Pair.RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var color = SEntMan.GetComponent<MapLightComponent>(source).AmbientLightColor;
            Assert.That(color.R, Is.GreaterThan(0.9f), "The source should be near noon, not the follower's old night.");
            Assert.That(SEntMan.GetComponent<MapLightComponent>(follower).AmbientLightColor, Is.EqualTo(color));
            Assert.That(SEntMan.HasComponent<DayNightCycleComponent>(source), Is.True);
            Assert.That(SEntMan.HasComponent<DayNightCycleComponent>(follower), Is.False);
        });
    }

    [Test]
    public async Task FollowingDoesNotAddLightingToGroundOrTouchUnrelatedMaps()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var source = maps.CreateMap();
            var follower = maps.CreateMap();
            var unrelated = maps.CreateMap();
            SEntMan.EnsureComponent<DayNightCycleComponent>(unrelated);
            var lighting = Server.System<CMUZLevelAmbientLightSystem>();
            lighting.FollowMap(follower, source);
            Assert.That(SEntMan.HasComponent<MapLightComponent>(source), Is.False,
                "SpaceLightSystem must still recognize an unlit space map during loading.");
            Assert.That(SEntMan.HasComponent<DayNightCycleComponent>(unrelated), Is.True);
            lighting.FollowMap(source, source);
            Assert.That(SEntMan.HasComponent<CMUZLevelAmbientLightComponent>(source), Is.False);
        });
    }
}
