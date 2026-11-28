using System.Collections.Generic;
using System.Numerics;
using Content.Server._RMC14.Dropship.Weapon;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevels.Core.EntitySystems;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.ZLevels.Ordnance;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.ZLevels.Ordnance;

[TestFixture]
public sealed class CMUDropshipBreachTest
{
    private const int Plate = 3;

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public async Task BreachOpensOnlyItsOwnLevelWithinRadius(int radius)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>();
            var transform = entMan.System<SharedTransformSystem>();
            var zLevels = entMan.System<CMUZLevelsSystem>();
            var ordnance = entMan.System<CMUTopDownOrdnanceSystem>();
            var breaches = entMan.System<DropshipZLevelBreachSystem>();
            var tileDefinitions = server.ResolveDependency<ITileDefinitionManager>();

            var maps = new EntityUid[5];
            var mapIds = new MapId[5];
            var grids = new Entity<MapGridComponent>[5];
            var network = zLevels.CreateZNetwork();

            try
            {
                for (var depth = 0; depth < maps.Length; depth++)
                {
                    maps[depth] = map.CreateMap(out mapIds[depth]);
                    if (depth == 0)
                        continue;

                    grids[depth] = map.CreateGridEntity(mapIds[depth]);
                    for (var x = -Plate; x <= Plate; x++)
                    {
                        for (var y = -Plate; y <= Plate; y++)
                            map.SetTile(grids[depth], new Vector2i(x, y), new Tile(tileDefinitions["Plating"].TileId));
                    }
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
                {
                    [maps[0]] = 0,
                    [maps[1]] = 1,
                    [maps[2]] = 2,
                    [maps[3]] = 3,
                    [maps[4]] = 4,
                }), Is.True);

                var center = new Vector2(0.5f, 0.5f);
                breaches.CarveBreach(new MapCoordinates(center, mapIds[1]), radius);

                for (var depth = 1; depth < maps.Length; depth++)
                {
                    for (var x = -Plate; x <= Plate; x++)
                    {
                        for (var y = -Plate; y <= Plate; y++)
                        {
                            var withinRadius = radius > 0 && depth == 1 && Math.Abs(x) <= radius && Math.Abs(y) <= radius;
                            var coordinates = new MapCoordinates(center + new Vector2(x, y), mapIds[depth]);
                            Assert.That(
                                ordnance.IsOpening(coordinates),
                                Is.EqualTo(withinRadius),
                                $"Tile ({x}, {y}) at depth {depth} should be {(withinRadius ? "open" : "intact")}");
                        }
                    }
                }

                var markerCount = 0;
                var markers = entMan.EntityQueryEnumerator<DropshipZLevelBreachComponent, TransformComponent>();
                while (markers.MoveNext(out _, out _, out var markerTransform))
                {
                    var markerCoordinates = transform.ToMapCoordinates(markerTransform.Coordinates);
                    if (markerCoordinates.MapId == mapIds[1])
                        markerCount++;
                }

                var expectedMarkers = radius == 0 ? 0 : (2 * radius + 1) * (2 * radius + 1);
                Assert.That(markerCount, Is.EqualTo(expectedMarkers));
            }
            finally
            {
                if (!entMan.Deleted(network))
                    entMan.DeleteEntity(network);

                for (var i = 0; i < mapIds.Length; i++)
                    map.DeleteMap(mapIds[i]);
            }
        });

        await pair.CleanReturnAsync();
    }
}
