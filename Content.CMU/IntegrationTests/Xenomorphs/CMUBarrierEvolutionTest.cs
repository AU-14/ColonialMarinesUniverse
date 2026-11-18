using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Climbing.Components;
using Content.Shared._RMC14.Xenonids.Evolution;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU.Xenomorphs;

public sealed class CMUBarrierEvolutionTest : GameTest
{
    [TestCase(-1)]
    [TestCase(1)]
    public async Task EvolutionPreservesSideOfMetalBarricade(int side)
    {
        var map = await Pair.CreateTestMap();
        EntityUid drone = default, evolved = default;
        float edgeY = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var floor = new Tile(Server.ResolveDependency<ITileDefinitionManager>()["Plating"].TileId);
            for (var x = -1; x <= 1; x++)
            for (var y = -1; y <= 1; y++)
                maps.SetTile(map.Grid, map.Grid.Comp, new Vector2i(x, y), floor);
            var center = maps.GridTileToLocal(map.Grid, map.Grid.Comp, Vector2i.Zero);
            SSpawnAtPosition("CMBarricadeMetal", center);
            // The default metal barricade fixture spans -0.45 to -0.30 on its tile.
            edgeY = center.Y - .375f;
            drone = SSpawnAtPosition("CMXenoDrone", center.Offset(new Vector2(0, -.375f + side * .34f)));
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            Assert.That((Server.Transform(drone).LocalPosition.Y - edgeY) * side, Is.Positive);
            evolved = Server.System<XenoEvolutionSystem>().EvolveTo(drone, "RMCXenoCrusher");
        });
        await RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            Assert.That((Server.Transform(evolved).LocalPosition.Y - edgeY) * side, Is.Positive);
            Assert.That(SEntMan.GetComponent<ClimbingComponent>(evolved).IsClimbing, Is.False);
        });
    }
}
