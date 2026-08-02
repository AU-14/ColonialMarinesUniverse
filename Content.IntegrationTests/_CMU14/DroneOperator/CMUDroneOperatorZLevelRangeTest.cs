using System.Collections.Generic;
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._CMU14.DroneOperator;

[TestFixture]
public sealed class CMUDroneOperatorZLevelRangeTest
{
    [Test]
    public async Task AdjacentLevelDistanceUsesShortestOpeningRoute()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var lowerMap = await pair.CreateTestMap();
        var upperMap = await pair.CreateTestMap();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var map = server.System<SharedMapSystem>();
            var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();
            var zLevels = server.System<CMUZLevelsSystem>();
            var plating = new Tile(tileDefinitions["Plating"].TileId);

            for (var x = -8; x <= 18; x++)
            {
                for (var y = -8; y <= 8; y++)
                {
                    map.SetTile(upperMap.Grid.Owner, upperMap.Grid.Comp, new Vector2i(x, y), plating);
                }
            }

            map.SetTile(upperMap.Grid.Owner, upperMap.Grid.Comp, new Vector2i(-1, 0), Tile.Empty);
            map.SetTile(upperMap.Grid.Owner, upperMap.Grid.Comp, new Vector2i(5, 0), Tile.Empty);

            var network = zLevels.CreateZNetwork();
            Assert.That(
                zLevels.TryAddMapsIntoZNetwork(
                    network,
                    new Dictionary<EntityUid, int>
                    {
                        [lowerMap.MapUid] = 0,
                        [upperMap.MapUid] = 1,
                    }),
                Is.True);

            var found = zLevels.TryGetDistanceViaAdjacentLevelOpening(
                upperMap.MapUid,
                new Vector2(0.5f, 0.5f),
                lowerMap.MapUid,
                new Vector2(10.5f, 0.5f),
                15f,
                out var distance);

            Assert.That(found, Is.True);
            Assert.That(distance, Is.EqualTo(10f).Within(0.001f));
        });

        await pair.CleanReturnAsync();
    }
}
