#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.Rules;
using Content.Shared.Maps;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Evacuation;

// crashing lifeboats used to drop the hull's corner on one landable tile, so near the edge the rest hung over the void
[TestFixture]
public sealed class LifeboatCrashSiteTest : GameTest
{
    [Test]
    public async Task WholeHullLandsOnThePlanet()
    {
        EntityUid planetUid = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var tiles = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = new Tile(tiles["FloorSteel"].TileId);

            // a small planet, so plenty of rolls land near an edge
            var planet = maps.CreateMap(out _);
            var planetGrid = SEntMan.EnsureComponent<MapGridComponent>(planet);
            var areaGrid = SEntMan.EnsureComponent<AreaGridComponent>(planet);
            var areas = Server.System<AreaSystem>();
            for (var x = -40; x < 40; x++)
            for (var y = -40; y < 40; y++)
            {
                maps.SetTile(planet, planetGrid, new Vector2i(x, y), floor);
                areas.ReplaceArea(areaGrid, new Vector2i(x, y), "RMCAreaBosenmoriBashoMountainClearing");
            }
            SEntMan.EnsureComponent<RMCPlanetComponent>(planet);
            planetUid = planet;
        });

        // let the planet's bounds settle so the picker rolls inside the real map
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var planet = planetUid;
            var planetGrid = SComp<MapGridComponent>(planet);
            maps.CreateMap(out var holding);
            Assert.That(Server.System<MapLoaderSystem>().TryLoadGrid(holding, new ResPath("/Maps/_RMC14/Shuttles/lifeboat.yml"), out var boat), Is.True);
            var boatGrid = SComp<MapGridComponent>(boat!.Value);
            var footprint = maps.GetAllTiles(boat.Value, boatGrid).Select(t => t.GridIndices).ToList();
            Assert.That(footprint, Is.Not.Empty);

            var crashLand = Server.System<SharedCrashLandSystem>();
            Assert.Multiple(() =>
            {
                for (var i = 0; i < 50; i++)
                {
                    Assert.That(crashLand.TryGetCrashLandLocation(boat.Value, out var location), Is.True);
                    var anchor = maps.LocalToTile(planet, planetGrid, location);
                    var overhang = footprint.Count(offset =>
                        !maps.TryGetTileRef(planet, planetGrid, anchor + offset, out var tile) || tile.Tile.IsEmpty);
                    Assert.That(overhang, Is.Zero, $"roll {i}: {overhang} hull tiles hang off the planet at {anchor}");
                }
            });
        });
    }
}
