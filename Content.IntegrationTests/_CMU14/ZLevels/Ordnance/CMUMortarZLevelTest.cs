using System.Collections.Generic;
using System.Numerics;
using Content.Server._CMU14.ZLevels.Core;
using Content.Shared._CMU14.ZLevels.Ordnance;
using Content.Shared._RMC14.Rules;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._CMU14.ZLevels.Ordnance;

[TestFixture]
public sealed class CMUMortarZLevelTest
{
    [Test]
    public async Task ConnectedPlanetLevelRequiresOpenLaunchColumn()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var map = entMan.System<SharedMapSystem>();
            var ordnance = entMan.System<CMUTopDownOrdnanceSystem>();
            var planet = entMan.System<RMCPlanetSystem>();
            var tile = server.ResolveDependency<ITileDefinitionManager>();
            var zLevels = entMan.System<CMUZLevelsSystem>();

            var planetMap = map.CreateMap(out var planetMapId);
            var mortarMap = map.CreateMap(out var mortarMapId);
            var roofMap = map.CreateMap(out var roofMapId);
            var network = zLevels.CreateZNetwork();

            try
            {
                Assert.That(zLevels.TryAddMapsIntoZNetwork(network, new Dictionary<EntityUid, int>
                {
                    [planetMap] = 0,
                    [mortarMap] = 1,
                    [roofMap] = 2,
                }), Is.True);

                var planetComp = entMan.EnsureComponent<RMCPlanetComponent>(planetMap);
                planetComp.Offset = new Vector2i(17, -9);

                var mortarCoordinates = new MapCoordinates(Vector2.Zero, mortarMapId);
                Assert.That(planet.TryGetPlanetSurfaceCoordinates(mortarCoordinates, out var planetCoordinates), Is.True);
                Assert.That(planetCoordinates.MapId, Is.EqualTo(planetMapId));
                Assert.That(planetCoordinates.Position, Is.EqualTo(mortarCoordinates.Position));
                Assert.That(planet.TryGetOffset(planetCoordinates, out var offset), Is.True);
                Assert.That(offset, Is.EqualTo(planetComp.Offset));

                Assert.That(ordnance.IsOpenToSky(mortarCoordinates), Is.True);

                var roofGrid = map.CreateGridEntity(roofMapId);
                map.SetTile(roofGrid, Vector2i.Zero, new Tile(tile["Plating"].TileId));
                Assert.That(ordnance.IsOpenToSky(mortarCoordinates), Is.False);

                map.SetTile(roofGrid, Vector2i.Zero, Tile.Empty);
                Assert.That(ordnance.IsOpenToSky(mortarCoordinates), Is.True);
            }
            finally
            {
                if (!entMan.Deleted(network))
                    entMan.DeleteEntity(network);

                map.DeleteMap(roofMapId);
                map.DeleteMap(mortarMapId);
                map.DeleteMap(planetMapId);
            }
        });

        await pair.CleanReturnAsync();
    }
}
