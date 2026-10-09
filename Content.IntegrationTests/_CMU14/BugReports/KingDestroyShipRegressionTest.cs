using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Xenonids.Destroy;
using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class KingDestroyShipRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    // the Bush hull is noTunnel everywhere for burrowers, the Almayer CIC is noTunnel on purpose
    [TestCase("AU14AreaUSSGeorgeBushFlightdeck", true)]
    [TestCase("AU14AreaUSSGeorgeBushRequisitions", true)]
    [TestCase("RMCAreaAlmayerCommandCic", false)]
    public async Task KingCanLeapOnShipsButNotIntoProtectedRooms(string areaId, bool expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            var landing = new Vector2i(2, 0);
            for (var i = 1; i <= 2; i++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(i, 0), floor);

            var areas = SEntMan.EnsureComponent<AreaGridComponent>(map.Grid.Owner);
            SEntMan.System<AreaSystem>().ReplaceArea(areas, landing, areaId);

            var king = SEntMan.SpawnEntity("RMCXenoKing", map.GridCoords);
            var ev = new XenoDestroyActionEvent
            {
                Performer = king,
                Target = map.GridCoords.Offset(new Vector2(2, 0)),
            };
            SEntMan.EventBus.RaiseLocalEvent(king, ev);

            var leaping = SEntMan.TryGetComponent(king, out DoAfterComponent? doAfters) && doAfters.DoAfters.Count > 0;
            Assert.That(leaping, Is.EqualTo(expected));
        });
    }
}
