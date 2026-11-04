using System.Numerics;
using Content.Shared._RMC14.Xenonids.Construction;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Xenonids;

// BUG-778: destroyable multi-z roofs are isSpace (they share the outdoor air), which made resin refuse them
[TestFixture]
[TestOf(typeof(SharedXenoConstructionSystem))]
public sealed class CMUXenoRoofConstructionTest
{
    [TestCase("CMURoofDestroyable", true)]
    [TestCase("AU14LatticeTile2", true)]
    [TestCase("Plating", true)]
    [TestCase("Lattice", false)]
    public async Task ResinTreatsOpenAirFloorsAsFloor(string tileId, bool buildable)
    {
        await using var pair = await PoolManager.GetServerClient();
        var map = await pair.CreateTestMap();
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var construction = entities.System<SharedXenoConstructionSystem>();
            var drone = entities.SpawnEntity("CMXenoDrone", map.GridCoords);
            var target = map.GridCoords.Offset(new Vector2(1, 0));
            var tiles = pair.Server.ResolveDependency<ITileDefinitionManager>();
            entities.System<SharedMapSystem>().SetTile(map.Grid, map.Grid.Comp, target, new Tile(tiles[tileId].TileId));

            var comp = entities.GetComponent<XenoConstructionComponent>(drone);
            var result = construction.CanSecreteOnTilePopup((drone, comp), null, target, false, false, popup: false);
            Assert.That(result, Is.EqualTo(buildable), $"resin placement on {tileId}");
        });
        await pair.CleanReturnAsync();
    }
}
