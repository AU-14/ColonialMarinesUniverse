using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Spreader;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.ZLevels;

/// <summary>
/// FLOW-05: an edge spreader that opts into cmuBlockZOpenings does not move onto holes to the level below.
/// </summary>
[TestFixture]
[TestOf(typeof(SpreaderSystem))]
public sealed class CMUZSpreadOpeningTest : GameTest
{
    private const string Blocked = "CMUTestZSpreadBlockOpenings";
    private const string Control = "CMUTestZSpreadOpeningControl";

    [TestPrototypes]
    private const string Prototypes = @"
- type: edgeSpreader
  id: CMUTestZSpreadBlockOpenings
  updatesPerSecond: 1
  cmuBlockZOpenings: true

- type: edgeSpreader
  id: CMUTestZSpreadOpeningControl
  updatesPerSecond: 1

- type: entity
  id: CMUTestZSpreadBlockOpeningsEntity
  components:
  - type: Transform
    anchored: true
  - type: EdgeSpreader
    id: CMUTestZSpreadBlockOpenings

- type: entity
  id: CMUTestZSpreadOpeningControlEntity
  components:
  - type: Transform
    anchored: true
  - type: EdgeSpreader
    id: CMUTestZSpreadOpeningControl
";

    private static readonly Vector2i Origin = new(0, 0);
    private static readonly Vector2i PitTile = new(1, 0);
    private static readonly Vector2i OpenAirTile = new(-1, 0);
    private static readonly Vector2i FloorNorth = new(0, 1);
    private static readonly Vector2i FloorSouth = new(0, -1);

    private readonly List<EntityUid> _cleanup = new();

    [Test]
    public async Task FlaggedSpreaderSkipsPitsAndFallThroughTilesButControlDoesNot()
    {
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var plating = new Tile(tiles["Plating"].TileId);

            var lower = maps.CreateMap(runMapInit: true);
            _cleanup.Add(lower);
            var upper = maps.CreateMap(runMapInit: true);
            _cleanup.Add(upper);
            var lowerGrid = SEntMan.EnsureComponent<MapGridComponent>(lower);
            var upperGrid = SEntMan.EnsureComponent<MapGridComponent>(upper);
            for (var x = -2; x <= 2; x++)
            for (var y = -2; y <= 2; y++)
            {
                maps.SetTile(lower, lowerGrid, new Vector2i(x, y), plating);
                maps.SetTile(upper, upperGrid, new Vector2i(x, y), plating);
            }

            var z = Server.System<CMUZLevelsSystem>();
            var network = z.CreateZNetwork();
            _cleanup.Add(network.Owner);
            Assert.That(z.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [upper] = 1 }), Is.True);

            maps.SetTile(upper, upperGrid, OpenAirTile, new Tile(tiles["CMUFloorOpenAirSealed"].TileId));
            // The pit sits on a solid tile: only its entity makes it a hole. The floor below keeps it from digging.
            SEntMan.SpawnEntity("RUMCPit", maps.GridTileToLocal(upper, upperGrid, PitTile));

            var spreader = Server.System<SpreaderSystem>();
            var blockedFree = FreeTiles(spreader, "CMUTestZSpreadBlockOpeningsEntity", Blocked, upper, upperGrid);
            var controlFree = FreeTiles(spreader, "CMUTestZSpreadOpeningControlEntity", Control, upper, upperGrid);

            Assert.Multiple(() =>
            {
                Assert.That(blockedFree, Does.Not.Contain(PitTile), "A flagged spreader must not spread into a pit.");
                Assert.That(blockedFree, Does.Not.Contain(OpenAirTile), "A flagged spreader must not spread onto a fall-through tile.");
                Assert.That(blockedFree, Does.Contain(FloorNorth).And.Contain(FloorSouth), "Plain floor stays open to it.");
                Assert.That(controlFree, Is.EquivalentTo(new[] { PitTile, OpenAirTile, FloorNorth, FloorSouth }),
                    "A spreader without the flag keeps the old behaviour.");
            });
        });
    }

    private HashSet<Vector2i> FreeTiles(SpreaderSystem spreader, string entity, string prototype, EntityUid map, MapGridComponent grid)
    {
        var uid = SEntMan.SpawnEntity(entity, Server.System<SharedMapSystem>().GridTileToLocal(map, grid, Origin));
        var xform = SComp<TransformComponent>(uid);
        spreader.GetNeighbors(uid, xform, prototype, out var freeTiles, out _, out _);
        var result = new HashSet<Vector2i>();
        foreach (var (_, tileRef) in freeTiles)
            result.Add(tileRef.GridIndices);
        return result;
    }

    [TearDown]
    public async Task CleanupScenario()
    {
        for (var i = _cleanup.Count - 1; i >= 0; i--)
            await Pair.DeleteEntityTreeLeafFirst(_cleanup[i]);
        _cleanup.Clear();
    }
}
