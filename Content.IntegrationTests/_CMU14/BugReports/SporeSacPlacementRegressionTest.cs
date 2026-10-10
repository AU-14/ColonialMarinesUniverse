using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Actions.Components;
using Content.Shared.CMU14.Xenomorphs.Pathogen.SporeSac;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU14.BugReports;

[TestFixture]
public sealed class SporeSacPlacementRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    // popper sits on (0,0); x tiles 1-3 are floor, a wall goes on (1,0) when asked, y = 2 is space
    [TestCase(2, 0, false, true, TestName = "OpenFloorInSight")]
    [TestCase(2, 0, true, false, TestName = "FloorBehindWall")]
    [TestCase(1, 0, true, false, TestName = "OnTheWallItself")]
    [TestCase(0, 2, false, false, TestName = "IntoSpace")]
    public async Task PopperOnlyPlantsSacsWhereItCanReach(int x, int y, bool wall, bool expected)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var i = 1; i <= 3; i++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(i, 0), floor);

            if (wall)
                SEntMan.SpawnEntity("WallSolid", map.GridCoords.Offset(new Vector2(1, 0)));

            var popper = SEntMan.SpawnEntity("CMU14XenoPopper", map.GridCoords);
            var action = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var actionComp = SEntMan.AddComponent<ActionComponent>(action);
            var ev = new CMUXenoSporeSacActionEvent
            {
                Performer = popper,
                Target = map.GridCoords.Offset(new Vector2(x, y)),
                Action = (action, actionComp),
            };
            SEntMan.EventBus.RaiseLocalEvent(popper, ev);

            Assert.That(ev.Handled, Is.EqualTo(expected));
            Assert.That(SEntMan.GetComponent<CMUXenoSporeSacComponent>(popper).PendingCoords.HasValue, Is.EqualTo(expected));

            // don't let the placement doafter finish during pool teardown
            SEntMan.DeleteEntity(popper);
            SEntMan.DeleteEntity(action);
        });
    }
}
