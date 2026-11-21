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

- type: entity
  id: CMUTestCaveFootprint
  components:
  - type: Transform
    anchored: true
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      test:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.45,-0.45,1.45,1.45'
        hard: true
        layer: [LowImpassable]
        mask: [LowImpassable]
  - type: ZFootprint
    size: 2,2

- type: entity
  id: CMUTestPlayerBarricade
  components:
  - type: Transform
    anchored: true

- type: entity
  id: CMUTestMappedFixture
  components:
  - type: Transform
    anchored: true
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

            // CMU14: Cave-ins transfer the blocks above these tiles into the cave.
            var surface = entities.System<SharedMapSystem>();
            foreach (var spot in new[] { first, second })
            {
                surface.SetTile(map.Grid.Owner, map.Grid.Comp, spot, map.Tile.Tile);
                entities.SpawnEntity("CMUTestCaveWall", surface.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, spot));
            }

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
            // CMU14: Debris on open cave floor retains collision; only embedded debris loses it.
            foreach (var fixture in entities.GetComponent<FixturesComponent>(contents).Fixtures.Values)
            {
                // CMU14: the asserts that were here (kept): the fixture is not hard and has no layer or mask.
                // CMU14: Assert.That(fixture.Hard, Is.False);
                // CMU14: Assert.That(fixture.CollisionLayer, Is.Zero);
                // CMU14: Assert.That(fixture.CollisionMask, Is.Zero);
                Assert.That(fixture.Hard, Is.True);
                Assert.That(fixture.CollisionLayer, Is.Not.Zero);
                Assert.That(fixture.CollisionMask, Is.Not.Zero);
            }
            var maps = entities.System<SharedMapSystem>();
            var caveGrid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
            Assert.That(maps.GetTileRef(cave.Comp.StoneGrid, caveGrid, Vector2i.Zero).Tile.IsEmpty, Is.False);
            Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile.IsEmpty, Is.True);
        });
        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-01. A cave-in under an authored level buries the cave tiles but never deletes the
    // authored walls or floors above them. A player-built floor in the same cave-in still gives way (control).
    [Test]
    public async Task CaveInUnderAuthoredLevelLeavesAuthoredCellsAbove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        var authored = Vector2i.Zero;
        var playerBuilt = new Vector2i(2, 0);
        EntityUid wall = default;
        Tile authoredTile = default;
        HashSet<EntityUid> authoredAnchored = default!;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, Vector2i.Zero);
            entities.EnsureComponent<ZAuthoredLevelComponent>(map.MapUid);

            // The surface is one contiguous floor, like a real authored level. Isolated single tiles would make the
            // grid split when the control floor is pulled, moving the authored tile onto a new grid.
            for (var x = -3; x <= 51; x++)
            {
                for (var y = -1; y <= 1; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            // Mapped content above the cave: a floor and a load-bearing wall, with no player markers.
            maps.SetTile(map.Grid, authored, map.Tile.Tile);
            wall = entities.SpawnEntity("CMUTestCaveWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, authored));
            authoredTile = maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, authored).Tile;
            authoredAnchored = new HashSet<EntityUid>(maps.GetAnchoredEntities(map.Grid.Owner, map.Grid.Comp, authored));
            Assert.That(authoredAnchored, Does.Contain(wall));

            maps.SetTile(map.Grid, playerBuilt, map.Tile.Tile);
            entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, playerBuilt));

            Assert.That(entities.System<ZLevelSupportSystem>().IsAuthoredCell(map.MapUid, map.Grid, authored), Is.True);
            Assert.That(entities.System<ZLevelSupportSystem>().IsAuthoredCell(map.MapUid, map.Grid, playerBuilt), Is.False);

            cave.Comp.CollapseQueue.Add(authored);
            cave.Comp.CollapseQueue.Add(playerBuilt);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(cave.Comp.CollapseQueue, Is.Empty, "The cave-in must have run to completion.");
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, playerBuilt).Tile.IsEmpty, Is.True,
                    "Control: the player-built floor above the same cave-in must still give way.");
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, authored).Tile, Is.EqualTo(authoredTile),
                    "The authored floor above a cave-in must not be removed.");
                Assert.That(entities.Deleted(wall), Is.False, "The authored wall above a cave-in must not be taken.");
                Assert.That(new HashSet<EntityUid>(maps.GetAnchoredEntities(map.Grid.Owner, map.Grid.Comp, authored)),
                    Is.EquivalentTo(authoredAnchored), "Anchored authored content above a cave-in must be unchanged.");
                var caveGrid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
                var caveProtos = new List<string?>();
                foreach (var uid in maps.GetAnchoredEntities(cave.Comp.StoneGrid, caveGrid, authored))
                    caveProtos.Add(entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID);
                Assert.That(caveProtos, Does.Not.Contain("CMUTestCaveWall"),
                    "Nothing authored may be pulled down into the cave.");
                var rock = entities.System<ZLevelBuildingSystem>().GetEffectiveRockEntity(
                    entities.GetComponent<ZBuildableMapComponent>(map.MapUid), cave.Owner);
                Assert.That(caveProtos, Does.Contain(rock),
                    "The cave tile under a spared authored cell is still buried, with plain rock.");
            });
        });
        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-01 review. A player-built wall on an authored floor is taken into the cave; the
    // authored floor under it is then spared, but the cave tile is filled once, by the wall, not also by rock.
    [Test]
    public async Task PlayerBuiltWallOnAuthoredFloorFillsTheCaveOnce()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        EntityUid wall = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, Vector2i.Zero);
            entities.EnsureComponent<ZAuthoredLevelComponent>(map.MapUid);
            maps.SetTile(map.Grid, Vector2i.Zero, map.Tile.Tile);
            wall = entities.SpawnEntity("CMUTestCaveWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero));
            entities.EnsureComponent<PlayerBuiltComponent>(wall);
            Assert.That(entities.System<ZLevelSupportSystem>().IsAuthoredCell(map.MapUid, map.Grid, Vector2i.Zero), Is.False);
            cave.Comp.CollapseQueue.Add(Vector2i.Zero);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var caveGrid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
            var rock = entities.System<ZLevelBuildingSystem>().GetEffectiveRockEntity(
                entities.GetComponent<ZBuildableMapComponent>(map.MapUid), cave.Owner);
            var protos = new List<string?>();
            foreach (var uid in maps.GetAnchoredEntities(cave.Comp.StoneGrid, caveGrid, Vector2i.Zero))
                protos.Add(entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID);

            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(wall), Is.True, "The player-built wall is taken into the cave.");
                Assert.That(CountWalls(entities, cave.Comp.StoneGrid, Vector2i.Zero), Is.EqualTo(1),
                    "The cave tile holds exactly one load-bearing block.");
                Assert.That(protos, Does.Not.Contain(rock), "No rock is added when the wall already fell in.");
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile.IsEmpty, Is.False,
                    "The authored floor under the wall survives.");
            });
        });
        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit ARCH-01. A wall enrolled by a non-player builder stands on an AUTHORED floor tile with no
    // floor marker, on an authored level above a cave-in. Only its ZEnrolledSupport marks the cell as non-authored, so
    // the wall is taken into the cave like any built block, while the authored floor under it survives.
    [Test]
    public async Task EnrolledWallOnAuthoredFloorIsTakenIntoTheCave()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        EntityUid wall = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var support = entities.System<ZLevelSupportSystem>();
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, Vector2i.Zero);
            entities.EnsureComponent<ZAuthoredLevelComponent>(map.MapUid);
            maps.SetTile(map.Grid, Vector2i.Zero, map.Tile.Tile);
            wall = entities.SpawnEntity("CMUTestCaveWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero));
            Assert.That(support.IsAuthoredCell(map.MapUid, map.Grid, Vector2i.Zero), Is.True,
                "Before enrolment the bare wall on an authored floor is authored content.");
            support.Enroll(wall, ZLevelWallSupportComponent.CantileverSpan, true);
            Assert.That(entities.HasComponent<PlayerBuiltComponent>(wall), Is.False);
            Assert.That(support.IsAuthoredCell(map.MapUid, map.Grid, Vector2i.Zero), Is.False,
                "The enrolled wall makes its cell non-authored.");
            cave.Comp.CollapseQueue.Add(Vector2i.Zero);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var caveGrid = entities.GetComponent<MapGridComponent>(cave.Comp.StoneGrid);
            var protos = new List<string?>();
            foreach (var uid in maps.GetAnchoredEntities(cave.Comp.StoneGrid, caveGrid, Vector2i.Zero))
                protos.Add(entities.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID);

            Assert.Multiple(() =>
            {
                Assert.That(entities.Deleted(wall), Is.True, "The enrolled wall is taken into the cave.");
                Assert.That(protos, Does.Contain("CMUTestCaveWall"), "The wall's block lands in the cave.");
                Assert.That(CountWalls(entities, cave.Comp.StoneGrid, Vector2i.Zero), Is.EqualTo(1));
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile.IsEmpty, Is.False,
                    "The authored floor under the wall survives.");
            });
        });
        await pair.CleanReturnAsync();
    }

    // CMU14: final review N1. A player-built object that holds nothing up (a barricade) on an authored colony floor
    // must not make that floor collapsible: over a cave-in the authored tile and everything on it stay (the floor is
    // still there, so nothing falls through it), and the cave tile is filled once, with rock.
    [Test]
    public async Task PlayerObjectOnAuthoredFloorDoesNotPullTheFloorIntoTheCave()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        EntityUid mapped = default;
        EntityUid barricade = default;
        Tile authoredTile = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var support = entities.System<ZLevelSupportSystem>();
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, Vector2i.Zero);
            entities.EnsureComponent<ZAuthoredLevelComponent>(map.MapUid);
            maps.SetTile(map.Grid, Vector2i.Zero, map.Tile.Tile);
            authoredTile = maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile;
            var coords = maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero);
            mapped = entities.SpawnEntity("CMUTestMappedFixture", coords);
            barricade = entities.SpawnEntity("CMUTestPlayerBarricade", coords);
            entities.EnsureComponent<PlayerBuiltComponent>(barricade);
            Assert.That(support.IsAuthoredFloor(map.MapUid, map.Grid, Vector2i.Zero), Is.True);
            cave.Comp.CollapseQueue.Add(Vector2i.Zero);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var mappedXform = entities.GetComponent<TransformComponent>(mapped);
            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile, Is.EqualTo(authoredTile),
                    "The authored floor stays.");
                Assert.That(entities.Deleted(mapped), Is.False);
                Assert.That(mappedXform.Anchored, Is.True, "The mapped entity stays anchored on its floor.");
                Assert.That(mappedXform.GridUid, Is.EqualTo(map.Grid.Owner));
                var barricadeXform = entities.GetComponent<TransformComponent>(barricade);
                Assert.That(barricadeXform.MapUid, Is.EqualTo(map.MapUid),
                    "The player-built object stays on the floor that stays (it must not be dropped into the rock).");
                Assert.That(barricadeXform.Anchored, Is.True);
                Assert.That(CountLoadBearing(entities, cave.Comp.StoneGrid, Vector2i.Zero), Is.EqualTo(1),
                    "The cave tile is filled once.");
            });
        });
        await pair.CleanReturnAsync();
    }

    private static int CountLoadBearing(IEntityManager entities, EntityUid gridUid, Vector2i tile)
    {
        var maps = entities.System<SharedMapSystem>();
        var grid = entities.GetComponent<MapGridComponent>(gridUid);
        var count = 0;
        foreach (var uid in maps.GetAnchoredEntities(gridUid, grid, tile))
        {
            if (entities.HasComponent<ZLevelWallSupportComponent>(uid) ||
                entities.TryGetComponent(uid, out StructuralSupportComponent? support) && (support.IsVerticalSupport || support.IsAnchor))
                count++;
        }

        return count;
    }

    // CMU14: GraphAudit FLOW-06 (cave-in path). A cave-in under a non-origin tile of a 2x2 structure brings the
    // whole structure down into the cave, instead of leaving it standing over the hole.
    [Test]
    public async Task CaveInUnderACoveredTileDropsTheWholeStructure()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<ZGeneratedStoneComponent> cave = default;
        EntityUid structure = default;
        var covered = new Vector2i(1, 0);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            cave = CreateCave(entities, map.MapUid, map.Grid.Owner, map.Tile.Tile, Vector2i.Zero);
            // One contiguous surface, so pulling a tile does not split the grid.
            for (var x = -3; x <= 51; x++)
            {
                for (var y = -1; y <= 2; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            structure = entities.SpawnEntity("CMUTestCaveFootprint", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero));
            Assert.That(entities.GetComponent<ZFootprintComponent>(structure).Cells, Has.Count.EqualTo(3));
            cave.Comp.CollapseQueue.Add(covered);
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, covered).Tile.IsEmpty, Is.True);
                Assert.That(entities.GetComponent<TransformComponent>(structure).MapUid, Is.EqualTo(cave.Owner),
                    "The structure must fall with its covered tile, not float over the hole.");
                Assert.That(entities.GetComponent<ZFootprintComponent>(structure).Cells, Is.Empty);
                Assert.That(CountFootprintCells(entities, map.Grid, new Vector2i(1, 1)), Is.Zero,
                    "The structure's other cells are cleaned up.");
            });
        });
        await pair.CleanReturnAsync();
    }

    private static int CountFootprintCells(IEntityManager entities, Entity<MapGridComponent> grid, Vector2i tile)
    {
        var count = 0;
        foreach (var uid in entities.System<SharedMapSystem>().GetAnchoredEntities(grid.Owner, grid.Comp, tile))
        {
            if (entities.HasComponent<ZFootprintCellComponent>(uid))
                count++;
        }

        return count;
    }

    // CMU14: GraphAudit STATE-02. A cave two levels below the source map reads its roof span from that map, as
    // generation does, instead of falling back to the default span.
    [Test]
    public async Task SecondDeepCaveReadsSpanFromSourceMap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        ZGeneratedStoneComponent stone = default!;
        var center = new Vector2i(4, 4);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            entities.EnsureComponent<ZBuildableMapComponent>(map.MapUid).MaxRoofSpan = 1;
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out var firstMap, out var firstGrid), Is.True);
            Assert.That(building.EnsureNeighborLevel(firstMap, -1, firstGrid, Vector2.Zero, out var secondMap, out var secondGrid), Is.True);
            Assert.That(entities.HasComponent<ZBuildableMapComponent>(firstMap), Is.False,
                "The fixture needs a level without settings between the source map and the cave.");

            // The centre's nearest rock is 3 tiles away: stable at the default span 3, unstable at the source's span 1.
            stone = FillOpenCave(entities, secondMap, secondGrid, map.Tile.Tile, center, 2);
            stone.DirtyTiles.Add(center);
        });
        await pair.RunTicksSync(80);
        await server.WaitAssertion(() => Assert.That(stone.PendingCollapse.ContainsKey(center), Is.True,
            "The second-deep level must use the source map's span of 1."));
        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-02. In a 21x21 open cave only the centre is unsupported (span 10). The cave-in buries
    // that tile and one ring around it, not the whole connected cavern.
    [Test]
    public async Task CaveInBuriesOnlyTheUnsupportedPatchAndOneRing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        const int radius = 10;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out var caveMap, out var caveGrid), Is.True);
            entities.EnsureComponent<ZBuildableMapComponent>(map.MapUid).MaxRoofSpan = radius;
            var stone = FillOpenCave(entities, caveMap, caveGrid, map.Tile.Tile, Vector2i.Zero, radius);

            // A floor over the whole cavern: each buried cave tile pulls the floor above it.
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            stone.DirtyTiles.Add(Vector2i.Zero);
            stone.PendingCollapse[Vector2i.Zero] = TimeSpan.Zero;
        });
        await pair.RunTicksSync(100);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            var removed = 0;
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    if (maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y)).Tile.IsEmpty)
                        removed++;
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(map.Grid.Owner, map.Grid.Comp, Vector2i.Zero).Tile.IsEmpty, Is.True,
                    "The unsupported centre must cave in.");
                Assert.That(removed, Is.InRange(1, 9), "Only the unsupported tile and one ring may fall, not the cavern.");
            });
        });
        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit ECS-01 and ECS-02. Mining a mapped rock with no support above it re-solves nothing;
    // mining the rock under a support re-solves only that support's grid (control).
    [Test]
    public async Task MiningRockReSolvesOnlySupportsAboveIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;
        EntityUid lonely = default;
        EntityUid held = default;
        EntityUid marker = default;
        var lonelyTile = Vector2i.Zero;
        var heldTile = new Vector2i(3, 0);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            for (var x = -1; x <= 4; x++)
            {
                for (var y = -1; y <= 1; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
            }

            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, Vector2.Zero, out _, out var upperGrid), Is.True);
            upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            maps.SetTile(upper, heldTile, map.Tile.Tile);
            lonely = entities.SpawnEntity("CMUTestCaveWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, lonelyTile));
            held = entities.SpawnEntity("CMUTestCaveWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, heldTile));
            marker = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, heldTile));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var support = entities.System<ZLevelSupportSystem>();
            Assert.That(entities.GetComponent<StructuralSupportComponent>(marker).Supported, Is.True);
            Assert.That(support.IsGridDirty(map.Grid.Owner), Is.False);
            Assert.That(support.IsGridDirty(upper.Owner), Is.False);

            entities.DeleteEntity(lonely);
            Assert.That(support.IsGridDirty(map.Grid.Owner), Is.False, "A wall never re-solves its own grid.");
            Assert.That(support.IsGridDirty(upper.Owner), Is.False, "Nothing above the mined rock rests on it.");
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var support = entities.System<ZLevelSupportSystem>();
            Assert.That(support.PendingCollapseCount(upper.Owner), Is.Zero);
            Assert.That(entities.GetComponent<StructuralSupportComponent>(marker).Supported, Is.True);

            entities.DeleteEntity(held);
            Assert.That(support.IsGridDirty(upper.Owner), Is.True, "Control: the floor above the mined rock must re-solve.");
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
            Assert.That(server.EntMan.System<ZLevelSupportSystem>().PendingCollapseCount(upper.Owner), Is.EqualTo(1),
                "Control: the floor that rested on the mined rock starts its warning."));
        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit ECS-01. Grids solve lowest level first and the levels above follow in the same tick, so a
    // restored root re-supports a three-level column in one tick instead of one tick per level.
    [Test]
    public async Task ThreeLevelColumnResolvesInOneTick()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
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
            Entity<MapGridComponent> middle = (middleGrid, entities.GetComponent<MapGridComponent>(middleGrid));
            Entity<MapGridComponent> upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            maps.SetTile(middle, Vector2i.Zero, map.Tile.Tile);
            maps.SetTile(upper, Vector2i.Zero, map.Tile.Tile);
            var middleCoords = maps.GridTileToLocal(middle.Owner, middle.Comp, Vector2i.Zero);
            middleFloor = entities.SpawnEntity("AU14TileFloorSupport", middleCoords);
            upperFloor = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero));
            entities.SpawnEntity("CMUTestCavePillar", middleCoords);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(upperFloor).Supported, Is.True);
            server.EntMan.DeleteEntity(root);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(middleFloor).Supported, Is.False);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(upperFloor).Supported, Is.False);
            root = server.EntMan.SpawnEntity("CMUTestCavePillar", map.GridCoords);
        });
        await pair.RunTicksSync(1);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(middleFloor).Supported, Is.True);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(upperFloor).Supported, Is.True,
                "All three levels must settle in the tick after the root returns.");
        });
        await pair.CleanReturnAsync();
    }

    // CMU14: an open localized cave of (2r+1)^2 generated tiles around center on an existing level.
    private static ZGeneratedStoneComponent FillOpenCave(IEntityManager entities, EntityUid caveMap, EntityUid caveGrid,
        Tile floor, Vector2i center, int radius)
    {
        var stone = entities.EnsureComponent<ZGeneratedStoneComponent>(caveMap);
        stone.StoneGrid = caveGrid;
        stone.LocalizedToAuthoredLevel = true;
        var maps = entities.System<SharedMapSystem>();
        var grid = entities.GetComponent<MapGridComponent>(caveGrid);
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                var tile = center + new Vector2i(x, y);
                stone.GeneratedTiles.Add(tile);
                stone.GeneratedChunks.Add(new Vector2i((int) Math.Floor(tile.X / 8.0), (int) Math.Floor(tile.Y / 8.0)));
                maps.SetTile(caveGrid, grid, tile, floor);
            }
        }

        return stone;
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
