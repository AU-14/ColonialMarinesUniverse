using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Spreader;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests;

[TestFixture]
[TestOf(typeof(SpreaderSystem))]
public sealed class SpreaderFractionalRateTest : GameTest
{
    private static readonly EntProtoId HalfRate = "SpreaderMergeHalfRate";
    private static readonly EntProtoId OneAndHalfRate = "SpreaderMergeOneAndHalfRate";
    private static readonly EntProtoId DefaultRate = "SpreaderMergeDefaultRate";
    private static readonly EntProtoId ZeroRate = "SpreaderMergeZeroRate";

    [TestPrototypes]
    private const string Prototypes = @"
- type: edgeSpreader
  id: SpreaderMergeBudget
  updatesPerSecond: 2
  preventSpreadOnSpaced: false

- type: entity
  abstract: true
  id: SpreaderMergeBase
  components:
  - type: Transform
    anchored: true
  - type: ActiveEdgeSpreader
  - type: SpreaderFractionalRateProbe

- type: entity
  parent: SpreaderMergeBase
  id: SpreaderMergeHalfRate
  components:
  - type: EdgeSpreader
    id: SpreaderMergeBudget
    updatesPerSecond: 0.5

- type: entity
  parent: SpreaderMergeBase
  id: SpreaderMergeOneAndHalfRate
  components:
  - type: EdgeSpreader
    id: SpreaderMergeBudget
    updatesPerSecond: 1.5

- type: entity
  parent: SpreaderMergeBase
  id: SpreaderMergeDefaultRate
  components:
  - type: EdgeSpreader
    id: SpreaderMergeBudget
    updatesPerSecond: -1

- type: entity
  parent: SpreaderMergeBase
  id: SpreaderMergeZeroRate
  components:
  - type: EdgeSpreader
    id: SpreaderMergeBudget
    updatesPerSecond: 0
";

    [Test]
    public async Task FractionalOverridesCarryAndSameRateEntitiesShareBudget()
    {
        EntityUid gridUid = default;
        EntityUid[] halfRate = [];
        EntityUid[] oneAndHalfRate = [];
        EntityUid[] prototypeRate = [];

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var tileDefinitions = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tileDefinitions["FloorSteel"].TileId);

            map.CreateMap(out var mapId);
            var grid = map.CreateGridEntity(mapId);
            gridUid = grid.Owner;

            for (var x = 0; x < 7; x++)
                map.SetTile(grid, new Vector2i(x, 0), floor);

            halfRate =
            [
                SpawnAnchored(HalfRate, grid, new Vector2i(0, 0)),
                SpawnAnchored(HalfRate, grid, new Vector2i(1, 0)),
            ];
            oneAndHalfRate =
            [
                SpawnAnchored(OneAndHalfRate, grid, new Vector2i(2, 0)),
                SpawnAnchored(OneAndHalfRate, grid, new Vector2i(3, 0)),
                SpawnAnchored(OneAndHalfRate, grid, new Vector2i(4, 0)),
            ];
            prototypeRate =
            [
                SpawnAnchored(DefaultRate, grid, new Vector2i(5, 0)),
                SpawnAnchored(ZeroRate, grid, new Vector2i(6, 0)),
            ];
        });

        await Server.WaitAssertion(() =>
        {
            _ = Server.System<SpreaderFractionalRateProbeSystem>();
            var spreader = Server.System<SpreaderSystem>();
            var grid = SEntMan.GetComponent<SpreaderGridComponent>(gridUid);

            RunCycle(spreader, grid);
            AssertGroupTotals(halfRate, 0, oneAndHalfRate, 1, prototypeRate, 2,
                "first cycle: 0.5 carries, 1.5 spends one, and non-positive overrides use the prototype budget");

            RunCycle(spreader, grid);
            AssertGroupTotals(halfRate, 1, oneAndHalfRate, 3, prototypeRate, 4,
                "second cycle: 0.5 releases one carried update and 1.5 releases two");

            RunCycle(spreader, grid);
            AssertGroupTotals(halfRate, 1, oneAndHalfRate, 4, prototypeRate, 6,
                "third cycle repeats the fractional carry phase");

            RunCycle(spreader, grid);
            AssertGroupTotals(halfRate, 2, oneAndHalfRate, 6, prototypeRate, 8,
                "fourth cycle repeats the release phase without multiplying budgets per entity");
        });
    }

    private EntityUid SpawnAnchored(EntProtoId prototype, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var map = SEntMan.System<SharedMapSystem>();
        return SEntMan.SpawnEntity(prototype, map.GridTileToLocal(grid, grid.Comp, tile));
    }

    private static void RunCycle(SpreaderSystem spreader, SpreaderGridComponent grid)
    {
        grid.UpdateAccumulator = 0;
        spreader.Update(0);
    }

    private void AssertGroupTotals(
        EntityUid[] halfRate,
        int expectedHalf,
        EntityUid[] oneAndHalfRate,
        int expectedOneAndHalf,
        EntityUid[] prototypeRate,
        int expectedPrototype,
        string message)
    {
        Assert.Multiple(() =>
        {
            Assert.That(TotalConsumed(halfRate), Is.EqualTo(expectedHalf), message);
            Assert.That(TotalConsumed(oneAndHalfRate), Is.EqualTo(expectedOneAndHalf), message);
            Assert.That(TotalConsumed(prototypeRate), Is.EqualTo(expectedPrototype), message);
        });
    }

    private int TotalConsumed(EntityUid[] entities)
    {
        return entities.Sum(uid => SEntMan.GetComponent<SpreaderFractionalRateProbeComponent>(uid).UpdatesConsumed);
    }
}

[RegisterComponent]
public sealed partial class SpreaderFractionalRateProbeComponent : Component
{
    public int UpdatesConsumed;
}

public sealed class SpreaderFractionalRateProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpreaderFractionalRateProbeComponent, SpreadNeighborsEvent>(OnSpread);
    }

    private static void OnSpread(
        Entity<SpreaderFractionalRateProbeComponent> ent,
        ref SpreadNeighborsEvent args)
    {
        if (args.Updates < 1)
            return;

        ent.Comp.UpdatesConsumed++;
        args.Updates--;
    }
}
