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
    [TestCase(1)]
    [TestCase(3)]
    public async Task PenetratingImpactCarvesPassableMarkedCeilings(int penetration)
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
                    map.SetTile(grids[depth], Vector2i.Zero, new Tile(tileDefinitions["Plating"].TileId));
                }

                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
                {
                    [maps[0]] = 0,
                    [maps[1]] = 1,
                    [maps[2]] = 2,
                    [maps[3]] = 3,
                    [maps[4]] = 4,
                }), Is.True);

                var openingPosition = new Vector2(0.5f, 0.5f);
                breaches.CarvePenetrationPath(new MapCoordinates(openingPosition, mapIds[0]), penetration);

                for (var depth = 1; depth < maps.Length; depth++)
                {
                    var coordinates = new MapCoordinates(openingPosition, mapIds[depth]);
                    var markerCount = 0;
                    var markers = entMan.EntityQueryEnumerator<DropshipZLevelBreachComponent, TransformComponent>();
                    while (markers.MoveNext(out _, out _, out var markerTransform))
                    {
                        var markerCoordinates = transform.ToMapCoordinates(markerTransform.Coordinates);
                        if (markerCoordinates.MapId == mapIds[depth] && markerCoordinates.Position == openingPosition)
                            markerCount++;
                    }

                    Assert.That(
                        ordnance.IsOpening(coordinates),
                        Is.EqualTo(depth <= penetration),
                        $"Expected ceiling at depth {depth} to be {(depth <= penetration ? "open" : "intact")}");
                    Assert.That(markerCount, Is.EqualTo(depth <= penetration ? 1 : 0));
                }
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
