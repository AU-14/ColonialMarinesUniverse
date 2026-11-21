using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// ARCH-05: a bounded enclosure fill over roofed floor, stopped by walls and closed doors.
/// </summary>
[TestFixture]
[TestOf(typeof(CMUZEnclosureSystem))]
public sealed class CMUZEnclosureTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestEnclosureWall
  components:
  - type: Transform
    anchored: true
  - type: ZLevelWallSupport
";

    // The room is x, y in 0..2. Its walls are the ring at -1 and 3, with an airlock in the east wall.
    private static readonly Vector2i DoorTile = new(3, 1);
    private static readonly Vector2i Middle = new(1, 1);

    private readonly List<EntityUid> _cleanup = new();

    [Test]
    public async Task ClosedRoofedRoomIsEnclosedUntilRoofDoorPitOrAreaBreaksIt()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var z = Server.System<CMUZLevelsSystem>();
            var enclosure = Server.System<CMUZEnclosureSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var plating = new Tile(tiles["Plating"].TileId);

            var levels = new Dictionary<EntityUid, int>();
            var grids = new Dictionary<int, Entity<MapGridComponent>>();
            for (var depth = -1; depth <= 1; depth++)
            {
                var map = maps.CreateMap(runMapInit: true);
                _cleanup.Add(map);
                var grid = SEntMan.EnsureComponent<MapGridComponent>(map);
                for (var x = -3; x <= 5; x++)
                for (var y = -3; y <= 5; y++)
                    maps.SetTile(map, grid, new Vector2i(x, y), plating);
                levels.Add(map, depth);
                grids.Add(depth, (map, grid));
            }

            var network = z.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(z.TryAddMapsIntoZNetwork(network, levels), Is.True);

            var room = grids[0];
            var roof = grids[1];
            EntityUid door = default;
            for (var x = -1; x <= 3; x++)
            for (var y = -1; y <= 3; y++)
            {
                if (x is >= 0 and <= 2 && y is >= 0 and <= 2)
                    continue;

                var tile = new Vector2i(x, y);
                var spawned = SEntMan.SpawnEntity(tile == DoorTile ? "Airlock" : "CMUTestEnclosureWall",
                    maps.GridTileToLocal(room.Owner, room.Comp, tile));
                Assert.That(SComp<TransformComponent>(spawned).Anchored, Is.True);
                if (tile == DoorTile)
                    door = spawned;
            }

            var result = new HashSet<Vector2i>();
            bool Enclosed(int maxArea = 20) => enclosure.TryGetEnclosure(room.Owner, room, Middle, maxArea, result);

            Assert.That(SComp<DoorComponent>(door).State, Is.EqualTo(DoorState.Closed));
            Assert.That(Enclosed(), Is.True, "A closed, roofed 3x3 room is enclosed.");
            Assert.That(result, Has.Count.EqualTo(9));
            Assert.That(result, Does.Not.Contain(DoorTile), "The closed door bounds the room.");

            Assert.That(Enclosed(maxArea: 9), Is.True);
            Assert.That(Enclosed(maxArea: 8), Is.False, "A room larger than maxArea is not an enclosure.");

            maps.SetTile(roof.Owner, roof.Comp, new Vector2i(2, 2), Tile.Empty);
            Assert.That(Enclosed(), Is.False, "One missing roof tile opens the room.");
            maps.SetTile(roof.Owner, roof.Comp, new Vector2i(2, 2), plating);
            Assert.That(Enclosed(), Is.True);

            // The level below is solid, so the pit does not dig a new level.
            var pit = SEntMan.SpawnEntity("RUMCPit", maps.GridTileToLocal(room.Owner, room.Comp, new Vector2i(0, 2)));
            Assert.That(Enclosed(), Is.False, "A pit inside opens the room.");
            SEntMan.DeleteEntity(pit);
            Assert.That(Enclosed(), Is.True);

            Server.System<SharedDoorSystem>().StartOpening(door);
            Assert.That(SComp<DoorComponent>(door).State, Is.AnyOf(DoorState.Opening, DoorState.Open));
            Assert.That(Enclosed(), Is.False, "An open door opens the room.");
        });
    }

    [TearDown]
    public async Task CleanupScenario()
    {
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
        _cleanup.Clear();
    }
}
