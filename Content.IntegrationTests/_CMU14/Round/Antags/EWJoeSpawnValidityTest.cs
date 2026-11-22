using System.Numerics;
using Content.Server.CMU14.Round.Antags.EarlyWarning;
using Content.Server.CMU14.Spawning;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Rules;
using Content.Shared.CMU14.Falling;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests._CMU14.Round.Antags;

/// <summary>
/// FLOW-01 / TEST-01: the Distress Signal Joe only appears on a safe planet tile. The validator turns down
/// anything off a grid, off the planet, on space, blocked, outside an RMC area or next to a fall trigger,
/// and the host search never offers a spawn point that fails it.
/// </summary>
[TestFixture]
public sealed class EWJoeSpawnValidityTest
{
    private const string Area = "RMCAreaKutjevoExterior";
    private const string Wall = "WallSolid";
    private const string Lattice = "Lattice";
    private const string ColonyJoeJob = "AU14JobColonyWorkingJoe";

    [Test]
    public async Task OnlySafePlanetTilesPass()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;

        await server.WaitAssertion(() =>
        {
            var safeSpawn = entities.System<CMUSafeSpawnSystem>();
            var maps = entities.System<SharedMapSystem>();
            var areas = entities.System<AreaSystem>();
            var xforms = entities.System<SharedTransformSystem>();
            var tiles = server.ResolveDependency<ITileDefinitionManager>();

            var grid = map.Grid;
            var areaGrid = entities.EnsureComponent<AreaGridComponent>(grid.Owner);

            // Lay out every tile contiguous with a tile the test map really has: disconnected floor islands are
            // split off onto new grids (without area data) by the engine's grid splitting on the next tile edit.
            var origin = map.Tile.GridIndices;
            EntityCoordinates At(int x, bool area = true)
            {
                var indices = origin + new Vector2i(x, 0);
                if (x != 0)
                    maps.SetTile(grid, indices, map.Tile.Tile);
                if (area)
                    areas.ReplaceArea(areaGrid, indices, Area);
                return maps.GridTileToLocal(grid.Owner, grid.Comp, indices);
            }

            var floor = At(0);
            var nextToFloor = At(1);
            var bare = At(2, area: false);
            var walled = At(3);
            var latticeIndices = origin + new Vector2i(4, 0);
            maps.SetTile(grid, latticeIndices, new Tile(tiles[Lattice].TileId));
            areas.ReplaceArea(areaGrid, latticeIndices, Area);

            Assert.That(safeSpawn.GetUnsafeReason(floor), Is.EqualTo(CMUUnsafeSpawnReason.NotPlanet), "a map without RMCPlanet is not the planet");
            entities.EnsureComponent<RMCPlanetComponent>(map.MapUid);
            Assert.That(safeSpawn.GetUnsafeReason(floor), Is.Null, "a floor tile inside an area on the planet passes");

            Assert.Multiple(() =>
            {
                Assert.That(safeSpawn.IsSafeSpawnTile(EntityCoordinates.Invalid), Is.False, "invalid coordinates");
                Assert.That(safeSpawn.IsSafeSpawnTile(new EntityCoordinates(map.MapUid, new Vector2(500, 500))), Is.False,
                    "off-grid, in the void");
                Assert.That(safeSpawn.GetUnsafeReason(bare), Is.EqualTo(CMUUnsafeSpawnReason.NoArea), "a floor tile the area map does not cover");
                Assert.That(safeSpawn.GetUnsafeReason(maps.GridTileToLocal(grid.Owner, grid.Comp, latticeIndices)),
                    Is.EqualTo(CMUUnsafeSpawnReason.SpaceTile), "lattice over space");
            });

            // a wall on an otherwise good tile
            Assert.That(safeSpawn.GetUnsafeReason(walled), Is.Null, "the open tile passes");
            var wall = entities.SpawnEntity(Wall, walled);
            Assert.That(safeSpawn.GetUnsafeReason(walled), Is.EqualTo(CMUUnsafeSpawnReason.Blocked), "a wall blocks it");
            entities.DeleteEntity(wall);

            // a fall trigger one tile away
            var trigger = entities.SpawnEntity(null, nextToFloor);
            entities.EnsureComponent<CMUFallingComponent>(trigger);
            Assert.That(safeSpawn.GetUnsafeReason(floor), Is.EqualTo(CMUUnsafeSpawnReason.FallTrigger), "a fall trigger within 1.5 tiles");
            entities.DeleteEntity(trigger);
            Assert.That(safeSpawn.GetUnsafeReason(floor), Is.Null, "safe again once the trigger is gone");

            // the host search offers the safe colony Joe point and never the unsafe one
            var good = entities.SpawnEntity(null, floor);
            entities.AddComponent<SpawnPointComponent>(good).Job = ColonyJoeJob;
            var bad = entities.SpawnEntity(null, bare);
            entities.AddComponent<SpawnPointComponent>(bad).Job = ColonyJoeJob;

            var spots = entities.System<EWJoeRuleSystem>().FindHostSpots(map.MapId);
            Assert.That(spots, Has.Count.EqualTo(1), "only the safe point is offered");
            Assert.That((spots[0].Position - xforms.GetWorldPosition(good)).Length(), Is.LessThan(0.01f));
            Assert.That(safeSpawn.IsSafeSpawnTile(spots[0]));

            entities.DeleteEntity(good);
            entities.DeleteEntity(bad);
        });

        await pair.CleanReturnAsync();
    }
}
