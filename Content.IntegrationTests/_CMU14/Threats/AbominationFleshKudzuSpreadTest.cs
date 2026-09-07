using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Threats.Mobs.Abomination;
using Content.Server.Spreader;
using Content.Shared.CMU14.Threats.Mobs.Abomination;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._CMU14.Threats;

[TestFixture]
[TestOf(typeof(AbominationFleshKudzuSystem))]
public sealed class AbominationFleshKudzuSpreadTest : GameTest
{
    private static readonly EntProtoId TestKudzu = "AbominationFleshKudzuSpreadTestTarget";

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: AU14AbominationFleshKudzu
  id: AbominationFleshKudzuSpreadTestTarget
  components:
  - type: Kudzu
    growthLevel: 3
    spreadChance: 1
""";

    [Test]
    public async Task TendonsDoNotSpreadOntoWeedBlockedTiles()
    {
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var tileDefinitions = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tileDefinitions["FloorSteel"].TileId);

            map.CreateMap(out var mapId);
            var grid = map.CreateGridEntity(mapId);
            var sourceTile = Vector2i.Zero;
            var chasmTile = new Vector2i(1, 0);

            foreach (var tile in new[]
                     {
                         sourceTile,
                         chasmTile,
                         new Vector2i(-1, 0),
                         new Vector2i(0, 1),
                         new Vector2i(0, -1),
                     })
            {
                map.SetTile(grid, tile, floor);
            }

            var source = SEntMan.SpawnEntity(TestKudzu, map.GridTileToLocal(grid, grid.Comp, sourceTile));
            var chasm = SEntMan.SpawnEntity("FloorChasmEntity", map.GridTileToLocal(grid, grid.Comp, chasmTile));

            try
            {
                var spreader = Server.System<SpreaderSystem>();
                spreader.GetNeighbors(
                    source,
                    SEntMan.GetComponent<TransformComponent>(source),
                    "Kudzu",
                    out var freeTiles,
                    out _,
                    out var neighbors);

                Assert.That(freeTiles.ToArray().Any(neighbor => neighbor.Item2.GridIndices == chasmTile), Is.True,
                    "the generic spreader should expose the chasm tile so the tendon-specific weed rule is tested");

                var ev = new SpreadNeighborsEvent
                {
                    NeighborFreeTiles = freeTiles,
                    Neighbors = neighbors,
                    Updates = 4,
                };
                SEntMan.EventBus.RaiseLocalEvent(source, ref ev);

                var anchored = map.GetAnchoredEntities(grid, grid.Comp, chasmTile);
                while (anchored.MoveNext(out var entity))
                {
                    Assert.That(SEntMan.HasComponent<AbominationFleshKudzuComponent>(entity), Is.False,
                        "tendons must honor BlockWeeds just like xeno weeds do");
                }
            }
            finally
            {
                SEntMan.DeleteEntity(chasm);
                SEntMan.DeleteEntity(source);
                SEntMan.DeleteEntity(grid);
            }
        });
    }
}
