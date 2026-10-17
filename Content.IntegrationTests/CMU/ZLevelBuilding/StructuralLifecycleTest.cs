using System.Numerics;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.SavedBuilds;
using Content.Shared.CMU14.ZLevelBuilding;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.IntegrationTests.CMU.ZLevelBuilding;

[TestFixture]
public sealed class StructuralLifecycleTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestStructuralLoad
  components:
  - type: Transform
    anchored: true
  - type: StructuralSupport

- type: entity
  id: CMUTestCavePillar
  parent: CMUTestStructuralLoad
  components:
  - type: StructuralSupport
    isVerticalSupport: true

- type: entity
  id: CMUTestCaveWall
  components:
  - type: Transform
    anchored: true
  - type: ZLevelWallSupport

- type: entity
  id: CMUTestCollapsingFixture
  parent: CMUTestStructuralLoad
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      test:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.4,-0.4,0.4,0.4'
        hard: true
        layer: [LowImpassable]
        mask: [LowImpassable]
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeferredRemovalDoesNotDeleteReplacementFloor(bool sameTile)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Tile replacement = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var marker = entities.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            entities.DeleteEntity(marker);
            entities.SpawnEntity(sameTile ? "AU14TileApplierPlating" : "AU14TileApplierSteel", map.GridCoords);
            replacement = maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords).Tile;
            Assert.That(replacement.IsEmpty, Is.False);
        });

        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords).Tile, Is.EqualTo(replacement));
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task SurvivingMarkerRetainsFloorUntilItsOwnDeletion()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid survivor = default;
        await server.WaitAssertion(() =>
        {
            server.EntMan.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var first = server.EntMan.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            survivor = server.EntMan.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            server.EntMan.DeleteEntity(first);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords).Tile.IsEmpty, Is.False);
            server.EntMan.DeleteEntity(survivor);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords).Tile.IsEmpty, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UnanchoringCancelsCollapseAndReanchoringStartsANewWarning()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;
        EntityUid load = default;
        var originalTile = new Vector2i(0, 0);
        var movedTile = new Vector2i(3, 0);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, Vector2.Zero, out _, out var grid), Is.True);
            upper = (grid, entities.GetComponent<MapGridComponent>(grid));
            var maps = entities.System<SharedMapSystem>();
            maps.SetTile(upper, originalTile, map.Tile.Tile);
            maps.SetTile(upper, movedTile, map.Tile.Tile);
            // Floor markers distinguish constructed upper floors from mapped self-supporting floors.
            var original = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, originalTile));
            var moved = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, movedTile));
            entities.RemoveComponent<StructuralSupportComponent>(original);
            entities.RemoveComponent<StructuralSupportComponent>(moved);
            load = entities.SpawnEntity("CMUTestStructuralLoad", maps.GridTileToLocal(upper.Owner, upper.Comp, originalTile));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<StructuralSupportComponent>(load).Supported, Is.False);
            var transforms = entities.System<SharedTransformSystem>();
            transforms.Unanchor(load);
            transforms.SetCoordinates(load, entities.System<SharedMapSystem>().GridTileToLocal(upper.Owner, upper.Comp, movedTile));
        });
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(upper, originalTile).Tile.IsEmpty, Is.False);
                Assert.That(maps.GetTileRef(upper, movedTile).Tile.IsEmpty, Is.False);
                Assert.That(entities.HasComponent<StructuralSupportComponent>(load), Is.True);
            });
            Assert.That(entities.System<SharedTransformSystem>().AnchorEntity(load), Is.True);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(server.EntMan.System<SharedMapSystem>().GetTileRef(upper, movedTile).Tile.IsEmpty, Is.False));
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(upper, originalTile).Tile.IsEmpty, Is.False);
            Assert.That(maps.GetTileRef(upper, movedTile).Tile.IsEmpty, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("CMUTestStructuralLoad", false)]
    [TestCase("AU14TileFloorSupport", false)]
    [TestCase("CMUTestCavePillar", true)]
    [TestCase("CMUTestCaveWall", true)]
    public async Task CaveRoofRequiresLoadBearingCapability(string prototype, bool stable)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        var center = new Vector2i(4, 4);
        await server.WaitAssertion(() =>
        {
            cave = CreateCave(server.EntMan, map.MapUid, map.Grid.Owner, map.Tile.Tile, center);
            var maps = server.EntMan.System<SharedMapSystem>();
            var grid = server.EntMan.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
            server.EntMan.SpawnEntity(prototype, maps.GridTileToLocal(cave.Comp.StoneGrid, grid, center));
            cave.Comp.DirtyTiles.Add(center);
        });
        await pair.RunTicksSync(80);
        await server.WaitAssertion(() => Assert.That(cave.Comp.PendingCollapse.ContainsKey(center), Is.EqualTo(!stable)));
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UnsupportedColumnsDoNotProjectSupportAcrossThreeLevels(bool constructedWall)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> middle = default;
        Entity<MapGridComponent> upper = default;
        EntityUid root = default;
        EntityUid middleFloor = default;
        EntityUid upperFloor = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            root = entities.SpawnEntity("CMUTestCavePillar", map.GridCoords);
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, Vector2.Zero, out var middleMap, out var middleGrid), Is.True);
            Assert.That(building.EnsureNeighborLevel(middleMap, 1, middleGrid, Vector2.Zero, out _, out var upperGrid), Is.True);
            middle = (middleGrid, entities.GetComponent<MapGridComponent>(middleGrid));
            upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            maps.SetTile(middle, Vector2i.Zero, map.Tile.Tile);
            maps.SetTile(upper, Vector2i.Zero, map.Tile.Tile);
            var middleCoords = maps.GridTileToLocal(middle.Owner, middle.Comp, Vector2i.Zero);
            middleFloor = entities.SpawnEntity("AU14TileFloorSupport", middleCoords);
            upperFloor = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero));
            var column = entities.SpawnEntity(constructedWall ? "CMUTestCaveWall" : "CMUTestCavePillar", middleCoords);
            if (constructedWall)
                entities.EnsureComponent<PlayerBuiltComponent>(column);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(middleFloor).Supported, Is.True);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(upperFloor).Supported, Is.True);
            server.EntMan.DeleteEntity(root);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(middleFloor).Supported, Is.False);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(upperFloor).Supported, Is.False,
                "The upper floor must lose its load path during the middle column's warning, before that column disappears.");
            root = server.EntMan.SpawnEntity("CMUTestCavePillar", map.GridCoords);
        });
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(middleFloor).Supported, Is.True);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(upperFloor).Supported, Is.True);
            server.EntMan.DeleteEntity(root);
        });
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(middle, Vector2i.Zero).Tile.IsEmpty, Is.True);
            Assert.That(maps.GetTileRef(upper, Vector2i.Zero).Tile.IsEmpty, Is.True,
                "All dependent floors must collapse within one warning period, rather than one period per storey.");
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NetworkMembershipReevaluatesExistingFloors(bool detach)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid marker = default;
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            maps.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            marker = server.EntMan.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<StructuralSupportComponent>(marker).Supported, Is.True);
            var lower = entities.System<SharedMapSystem>().CreateMap(out _, runMapInit: true);
            var levels = entities.System<CMUZLevelsSystem>();
            var network = levels.CreateZNetwork();
            Assert.That(levels.TryAddMapsIntoZNetwork(network, new() { [lower] = 0, [map.MapUid] = 1 }), Is.True);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(marker).Supported, Is.False);
            if (detach)
                Assert.That(server.EntMan.System<CMUZLevelsSystem>().TryRemoveMapFromZNetwork(map.MapUid), Is.True);
        });
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords).Tile.IsEmpty, Is.EqualTo(!detach));
            if (detach)
                Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(marker).Supported, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DisjointCaveChangesSurviveAnActiveCollapse(bool changeDuringCollapse)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        var first = new Vector2i(4, 4);
        var second = new Vector2i(20, 4);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, first, second);
            if (changeDuringCollapse)
            {
                cave.Comp.CollapseQueue.Add(first);
                var maps = entities.System<SharedMapSystem>();
                var grid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
                var pillar = entities.SpawnEntity("CMUTestCavePillar", maps.GridTileToLocal(cave.Comp.StoneGrid, grid, second));
                entities.DeleteEntity(pillar);
                Assert.That(cave.Comp.DirtyTiles, Does.Contain(second), "Removal must be recorded while another region is collapsing.");
            }
            else
            {
                cave.Comp.DirtyTiles.Add(first);
                cave.Comp.DirtyTiles.Add(second);
                cave.Comp.PendingCollapse[first] = TimeSpan.Zero;
            }
        });
        await pair.RunTicksSync(800);
        await server.WaitAssertion(() =>
        {
            Assert.That(CountWalls(server.EntMan, cave.Comp.StoneGrid, first), Is.EqualTo(1));
            Assert.That(CountWalls(server.EntMan, cave.Comp.StoneGrid, second), Is.EqualTo(1));
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("CMUTestCavePillar")]
    [TestCase("CMUTestCaveWall")]
    public async Task QueuedBurialRevalidatesRescueAndExistingRock(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        var center = new Vector2i(0, 0);
        EntityUid rescue = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, center);
            cave.Comp.CollapseQueue.Add(center);
            var maps = entities.System<SharedMapSystem>();
            var grid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
            rescue = entities.SpawnEntity(prototype, maps.GridTileToLocal(cave.Comp.StoneGrid, grid, center));
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(cave.Comp.CollapseQueue, Is.Empty);
            Assert.That(entities.GetComponent<TransformComponent>(rescue).Anchored, Is.True);
            Assert.That(CountWalls(entities, cave.Comp.StoneGrid, center), Is.EqualTo(prototype == "CMUTestCaveWall" ? 1 : 0));
            Assert.That(entities.System<SharedMapSystem>().GetTileRef(map.Grid.Owner, map.Grid.Comp, center).Tile.IsEmpty, Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CaveCollapseTransfersChildGridContentsBeforeRemovingFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        EntityUid contents = default;
        EntityUid marker = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(map.Grid.Owner, Is.Not.EqualTo(map.MapUid), "This regression requires child-grid floor unanchoring.");
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, Vector2i.Zero);
            contents = entities.SpawnEntity("CMUTestCollapsingFixture", map.GridCoords);
            marker = entities.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            cave.Comp.CollapseQueue.Add(Vector2i.Zero);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var xform = entities.GetComponent<TransformComponent>(contents);
            Assert.Multiple(() =>
            {
                Assert.That(xform.MapUid, Is.EqualTo(cave.Owner));
                Assert.That(xform.Anchored, Is.False);
                Assert.That(entities.HasComponent<StructuralSupportComponent>(contents), Is.False);
                Assert.That(entities.HasComponent<ZFallenDebrisComponent>(contents), Is.True);
                Assert.That(entities.Deleted(marker), Is.True);
            });
            foreach (var fixture in entities.GetComponent<FixturesComponent>(contents).Fixtures.Values)
            {
                Assert.That(fixture.Hard, Is.False);
                Assert.That(fixture.CollisionLayer, Is.Zero);
                Assert.That(fixture.CollisionMask, Is.Zero);
            }
            var maps = entities.System<SharedMapSystem>();
            var caveGrid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
            Assert.That(maps.GetTileRef(cave.Comp.StoneGrid, caveGrid, Vector2i.Zero).Tile.IsEmpty, Is.False);
            Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile.IsEmpty, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    private static Entity<ZGeneratedStoneComponent> CreateCave(IEntityManager entities, EntityUid surfaceMap,
        EntityUid sourceGrid, Tile floor, params Vector2i[] centers)
    {
        var building = entities.System<ZLevelBuildingSystem>();
        Assert.That(building.EnsureNeighborLevel(surfaceMap, -1, sourceGrid, Vector2.Zero, out var caveMap, out var caveGrid), Is.True);
        entities.EnsureComponent<ZBuildableMapComponent>(surfaceMap).MaxRoofSpan = 1;
        var stone = entities.EnsureComponent<ZGeneratedStoneComponent>(caveMap);
        stone.StoneGrid = caveGrid;
        stone.LocalizedToAuthoredLevel = true;
        var maps = entities.System<SharedMapSystem>();
        var grid = entities.GetComponent<MapGridComponent>(caveGrid);
        maps.SetTile(sourceGrid, entities.GetComponent<MapGridComponent>(sourceGrid), new Vector2i(50, 0), floor);
        foreach (var center in centers)
        {
            for (var x = -2; x <= 2; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    var tile = center + new Vector2i(x, y);
                    stone.GeneratedTiles.Add(tile);
                    stone.GeneratedChunks.Add(new Vector2i((int) Math.Floor(tile.X / 8.0), (int) Math.Floor(tile.Y / 8.0)));
                    maps.SetTile(caveGrid, grid, tile, floor);
                }
            }
        }
        return (caveMap, stone);
    }

    private static int CountWalls(IEntityManager entities, EntityUid gridUid, Vector2i tile)
    {
        var maps = entities.System<SharedMapSystem>();
        var grid = entities.GetComponent<MapGridComponent>(gridUid);
        var count = 0;
        foreach (var uid in maps.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (entities.HasComponent<ZLevelWallSupportComponent>(uid))
                count++;
        }
        return count;
    }
}
