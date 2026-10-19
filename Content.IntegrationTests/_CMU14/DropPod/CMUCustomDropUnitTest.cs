using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.DropPod;
using Content.Shared.CMU14.DropPod;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.DropPod;

[TestFixture]
[TestOf(typeof(CMUCustomDropUnitSystem))]
public sealed class CMUCustomDropUnitTest : GameTest
{
    private const int RoomSize = 5;
    private const int PlanetSize = 24;

    private EntityUid _ship, _planet, _core, _rider, _cornerWall, _gapWall;
    private SharedMapSystem _maps = null!;

    [SetUp]
    public async Task SetupRoom()
    {
        await Server.WaitAssertion(() =>
        {
            _maps = SEntMan.System<SharedMapSystem>();
            _ship = _maps.CreateMap(runMapInit: true);
            _planet = _maps.CreateMap(runMapInit: true);
            var shipGrid = SEntMan.EnsureComponent<MapGridComponent>(_ship);
            var planetGrid = SEntMan.EnsureComponent<MapGridComponent>(_planet);
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);

            for (var x = 0; x < PlanetSize; x++)
            for (var y = 0; y < PlanetSize; y++)
                _maps.SetTile(_planet, planetGrid, new Vector2i(x, y), floor);

            // A ring of walls with an airlock in the middle of one side, built the way players would mid-round.
            for (var x = 0; x < RoomSize; x++)
            for (var y = 0; y < RoomSize; y++)
            {
                _maps.SetTile(_ship, shipGrid, new Vector2i(x, y), floor);
                var edge = x == 0 || y == 0 || x == RoomSize - 1 || y == RoomSize - 1;
                if (!edge)
                    continue;

                var coords = new EntityCoordinates(_ship, new Vector2(x + 0.5f, y + 0.5f));
                var piece = SEntMan.SpawnEntity(x == RoomSize / 2 && y == 0 ? "CMAirlock" : "CMWallMetal", coords);
                if (x == 0 && y == 0)
                    _cornerWall = piece;
                else if (x == RoomSize - 1 && y == RoomSize / 2)
                    _gapWall = piece;
            }

            var center = new EntityCoordinates(_ship, new Vector2(RoomSize / 2 + 0.5f));
            _core = SEntMan.SpawnEntity("CMUCustomDropUnitCore", center);
            _rider = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(_ship, new Vector2(1.5f)));

            var unit = SComp<CMUCustomDropUnitComponent>(_core);
            unit.Phases.Clear();
            unit.ScatterRadius = 0;
            unit.RespectParadropAreas = false;
            unit.InjuryChance = 0;
            unit.AtmosphereBreakupChance = 0;
            unit.ImpactBreakupChance = 0;
        });
    }

    [TearDown]
    public async Task CleanupRoom()
    {
        await Pair.DeleteEntityTreeLeafFirst(_ship);
        await Pair.DeleteEntityTreeLeafFirst(_planet);
    }

    [Test]
    public async Task SealedRoomDropsWithEverythingInside()
    {
        await Server.WaitAssertion(() =>
        {
            var launched = SEntMan.System<CMUCustomDropUnitSystem>()
                .TryLaunch(_core, _planet, new Vector2i(PlanetSize / 2, PlanetSize / 2), out var reason);
            Assert.That(launched, Is.True, reason);
        });

        await Server.WaitRunTicks(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SComp<CMUCustomDropUnitComponent>(_core).State, Is.EqualTo(CMUDropPodState.Landed));
            foreach (var carried in new[] { _core, _rider, _cornerWall })
                Assert.That(SComp<TransformComponent>(carried).MapUid, Is.EqualTo(_planet), $"{SEntMan.ToPrettyString(carried)}");

            Assert.That(SComp<TransformComponent>(_cornerWall).Anchored, Is.True,
                "Walls must be re-anchored where the unit lands.");

            var shipGrid = SComp<MapGridComponent>(_ship);
            Assert.That(_maps.GetTileRef(_ship, shipGrid, new Vector2i(0, 0)).Tile.IsEmpty, Is.False,
                "The unit's old footprint must be left as floor, not a hole in the ship.");
        });
    }

    [Test]
    public async Task UnsealedRoomIsRefused()
    {
        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(_gapWall);
            var launched = SEntMan.System<CMUCustomDropUnitSystem>()
                .TryLaunch(_core, _planet, new Vector2i(PlanetSize / 2, PlanetSize / 2), out _);

            Assert.That(launched, Is.False);
            Assert.That(SComp<CMUCustomDropUnitComponent>(_core).State, Is.EqualTo(CMUDropPodState.Ready));
            Assert.That(SComp<TransformComponent>(_rider).MapUid, Is.EqualTo(_ship));
        });
    }
}
