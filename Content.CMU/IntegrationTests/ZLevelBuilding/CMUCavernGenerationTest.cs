using System.Diagnostics;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevelBuilding.Pit;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Gravity;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Station.Components;
using Content.Server.Gravity;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.CMU.ZLevelBuilding;

public sealed class CMUCavernGenerationTest : GameTest
{
    [Test]
    public async Task LocalizedCaveFitsBesideMappedRoomAndSealsVoid()
    {
        var surface = await Pair.CreateTestMap();
        EntityUid lower = default;
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var building = Server.System<ZLevelBuildingSystem>();
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(surface.MapUid);
            settings.DepthBiomes.Clear();
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            for (var x = 0; x < 16; x++)
            for (var y = 0; y < 16; y++)
                maps.SetTile(surface.Grid, new Vector2i(x, y), surface.Tile.Tile);

            var entrance = new Vector2i(6, 5);
            var position = maps.GridTileToWorldPos(surface.Grid.Owner, surface.Grid.Comp, entrance);
            Assert.That(building.EnsureNeighborLevel(surface.MapUid, -1, surface.Grid.Owner, position,
                out lower, out var gridUid), Is.True);
            var grid = SEntMan.GetComponent<MapGridComponent>(gridUid);
            SEntMan.EnsureComponent<BecomesStationComponent>(gridUid);
            // A room intersects the same generation chunk as the entrance. Area markers also cover empty ground.
            var roomTile = new Vector2i(8, 5);
            maps.SetTile(gridUid, grid, roomTile, surface.Tile.Tile);
            var room = SEntMan.SpawnEntity("CMWallMetal", maps.GridTileToLocal(gridUid, grid, roomTile));
            var area = SEntMan.SpawnEntity("AU14AreaGarrisonOutofBounds", maps.GridTileToLocal(gridUid, grid, entrance));

            Assert.That(building.PrepareStoneForStair(surface.MapUid, surface.Grid.Owner, position,
                out var landingGrid, singleTile: true), Is.True);
            Assert.That(landingGrid, Is.EqualTo(gridUid));
            var stone = SEntMan.GetComponent<ZGeneratedStoneComponent>(lower);
            Assert.That(maps.GetTileRef(gridUid, grid, entrance).Tile.IsEmpty, Is.False,
                "An area marker must not leave the landing in void.");
            Assert.That(stone.GeneratedTiles.Contains(entrance), Is.True);
            Assert.That(SEntMan.EntityExists(area), Is.True);
            Assert.That(SEntMan.EntityExists(room), Is.True);
            Assert.That(maps.GetTileRef(gridUid, grid, roomTile).Tile, Is.EqualTo(surface.Tile.Tile));

            var digCoordinates = maps.GridTileToLocal(surface.Grid.Owner, surface.Grid.Comp, entrance);
            var miner = SEntMan.SpawnEntity("MobHuman", digCoordinates);
            var tool = SEntMan.SpawnEntity("Pickaxe", digCoordinates);
            Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(miner, tool), Is.True);
            Assert.That(Server.System<PitDiggingToolSystem>().TryStartDig(tool, miner, digCoordinates), Is.True,
                "The normal tool interaction must accept an entrance beside mapped terrain.");
            SEntMan.DeleteEntity(miner);
            if (SEntMan.EntityExists(tool))
                SEntMan.DeleteEntity(tool);

            foreach (var edge in new[] { new Vector2i(7, 5), new Vector2i(0, 5) })
            {
                var walls = maps.GetAnchoredEntities(gridUid, grid, edge).ToList();
                Assert.That(walls.Any(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUCavernBedrock"), Is.True);
                Assert.That(walls.All(uid => !SEntMan.HasComponent<DamageableComponent>(uid)), Is.True,
                    "The seal must survive mining, rather than become an exit into mapped terrain or void.");
                var edgePosition = maps.GridTileToWorldPos(surface.Grid.Owner, surface.Grid.Comp, edge);
                Assert.That(building.PrepareStoneForStair(surface.MapUid, surface.Grid.Owner, edgePosition,
                    out _, singleTile: true), Is.False, "Landing preparation must not clear the seal.");
            }

            building.EnsureChunkAt((lower, stone), lower, new Vector2i(-1, 5));
            Assert.That(maps.GetTileRef(gridUid, grid, new Vector2i(-1, 5)).Tile.IsEmpty, Is.True,
                "Streaming must stop at the footprint instead of extending into void.");
            Assert.That(building.PrepareStoneForStair(lower, gridUid, position, out var deeperGrid, singleTile: true), Is.True);
            var deeper = SEntMan.GetComponent<TransformComponent>(deeperGrid).MapUid!.Value;
            var deeperGridComp = SEntMan.GetComponent<MapGridComponent>(deeperGrid);
            Assert.That(maps.GetAnchoredEntities(deeperGrid, deeperGridComp, new Vector2i(7, 5))
                .Any(uid => SEntMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "CMUCavernBedrock"), Is.True,
                "Digging deeper must not provide a way around the seal.");
            maps.SetTile(gridUid, grid, entrance, Tile.Empty);
            building.EnsureChunkAt((lower, stone), lower, entrance);
            Assert.That(maps.GetTileRef(gridUid, grid, entrance).Tile.IsEmpty, Is.True,
                "Revisiting a clipped chunk must retain player excavation.");
            SEntMan.DeleteEntity(deeper);
            SEntMan.DeleteEntity(lower);
        });
    }

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestCavernFallingBody
  components:
  - type: Physics
    bodyType: Dynamic
  - type: CMUZPhysics
    bounciness: 0
  - type: GravityAffected
";

    [Test]
    public async Task OpeningHasGeneratedGroundBelowAndRevisitingRetainsChanges()
    {
        var map = await Pair.CreateTestMap();
        var levels = new List<EntityUid>();
        EntityUid body = default;
        EntityUid floorMap = default;
        EntityUid first = default;
        var landing = Vector2i.Zero;
        var field = new CMUCavernField(12345, new CMUCavernSettings { StartDepth = -1 });
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var building = Server.System<ZLevelBuildingSystem>();
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.Caverns = new CMUCavernSettings { StartDepth = -1 };
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.DepthBiomes.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            SEntMan.EnsureComponent<CMUCavernWorldComponent>(map.MapUid).Seed = 12345;
            landing = FindShaft(field);

            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out first, out var grid), Is.True);
            var stone = SEntMan.EnsureComponent<ZGeneratedStoneComponent>(first);
            stone.StoneGrid = grid;
            maps.SetTile(map.Grid, landing, map.Tile.Tile);
            building.PrefetchDigDestination(map.Grid.Owner,
                maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, landing), TimeSpan.FromSeconds(30));
            Assert.That(stone.GeneratedChunks, Is.Empty, "Starting to dig must queue terrain rather than spawn it all immediately.");
        });
        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var building = Server.System<ZLevelBuildingSystem>();
            var stone = SEntMan.GetComponent<ZGeneratedStoneComponent>(first);
            Assert.That(stone.GeneratedChunks.Count, Is.EqualTo(1), "Preparation must run while the digger is still above ground.");
            var timer = Stopwatch.StartNew();
            building.EnsureChunkAt((first, stone), first, landing);
            TestContext.Out.WriteLine($"Landing after dig prefetch: {timer.Elapsed.TotalMilliseconds:F1} ms");
            building.CancelDigPreparation(map.Grid.Owner);

            var current = first;
            var floorDepth = -3;
            while (field.IsOpen(landing, floorDepth - 1))
                floorDepth--;
            EntityUid upper = default;
            for (var depth = -1; depth >= -7; depth--)
            {
                levels.Add(current);
                var slice = SEntMan.GetComponent<ZGeneratedStoneComponent>(current);
                Assert.That(slice.GeneratedChunks.Count, Is.EqualTo(1), "A landing must not materialize the whole cavern.");
                if (depth == -3)
                    upper = current;
                if (depth == floorDepth)
                    floorMap = current;
                if (depth > -7)
                    current = SEntMan.GetComponent<CMUZLevelMapComponent>(current).MapBelow!.Value;
            }
            Assert.That(maps.GetTileRef(upper, SEntMan.GetComponent<MapGridComponent>(upper), landing).Tile.IsEmpty, Is.True);
            Assert.That(maps.GetTileRef(floorMap, SEntMan.GetComponent<MapGridComponent>(floorMap), landing).Tile.IsEmpty, Is.False);
            body = SEntMan.SpawnEntity("CMUTestCavernFallingBody",
                maps.GridTileToLocal(upper, SEntMan.GetComponent<MapGridComponent>(upper), landing));
            Server.System<GravitySystem>().RefreshGravity(upper);
            Assert.That(Server.System<SharedGravitySystem>().EntityGridOrMapHaveGravity((body, SEntMan.GetComponent<TransformComponent>(body))), Is.True,
                "Cave gravity is planetary and must survive generator refreshes.");
            var zPhysics = SEntMan.GetComponent<CMUZPhysicsComponent>(body);
            Assert.That(Server.System<CMUZLevelsSystem>().DistanceToGround((body, zPhysics), out _, 8), Is.GreaterThan(1f),
                "The ordinary fall system must see through multiple slices to generated ground.");

            // A player-built floor over a shaft and a removed floor must both survive another entrance request.
            var upperGrid = SEntMan.GetComponent<MapGridComponent>(upper);
            maps.SetTile(upper, upperGrid, landing, map.Tile.Tile);
            var removed = landing + new Vector2i(1, 0);
            var floorGrid = SEntMan.GetComponent<MapGridComponent>(floorMap);
            maps.SetTile(floorMap, floorGrid, removed, Tile.Empty);
            building.EnsureChunkAt((floorMap, SEntMan.GetComponent<ZGeneratedStoneComponent>(floorMap)), floorMap, landing);
            Assert.That(maps.GetTileRef(upper, upperGrid, landing).Tile, Is.EqualTo(map.Tile.Tile));
            Assert.That(maps.GetTileRef(floorMap, floorGrid, removed).Tile.IsEmpty, Is.True);
            maps.SetTile(upper, upperGrid, landing, Tile.Empty);
        });

        await RunSeconds(3);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(floorMap),
                "A body falling through the natural shaft must land rather than disappear into ungenerated space.");
            foreach (var level in levels)
                SEntMan.DeleteEntity(level);
        });
    }

    [Test]
    public async Task NaturalCaveStopsCollapseWithoutSupportingAdjacentExcavation()
    {
        var map = await Pair.CreateTestMap();
        EntityUid caveMap = default;
        EntityUid caveGrid = default;
        ZGeneratedStoneComponent stone = default!;
        var natural = new Vector2i(0, 0);
        var excavated = new Vector2i(1, 0);
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out caveMap, out caveGrid), Is.True);
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(caveGrid);
            stone = SEntMan.EnsureComponent<ZGeneratedStoneComponent>(caveMap);
            stone.StoneGrid = caveGrid;
            SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid).MaxRoofSpan = 1;
            for (var x = -8; x < 8; x++)
            for (var y = -8; y < 8; y++)
            {
                stone.GeneratedChunks.Add(new Vector2i(x < 0 ? -1 : 0, y < 0 ? -1 : 0));
                maps.SetTile(caveGrid, grid, new Vector2i(x, y), map.Tile.Tile);
            }
            stone.NaturalCaves.Add(natural);
            stone.DirtyTiles.Add(natural);
            stone.DirtyTiles.Add(excavated);
        });

        await RunSeconds(2);
        await Server.WaitAssertion(() =>
        {
            Assert.That(stone.PendingCollapse.ContainsKey(natural), Is.False);
            Assert.That(stone.PendingCollapse.ContainsKey(excavated), Is.True,
                "An empty natural cave must not count as a pillar supporting new excavation.");
            stone.CollapseQueue.Add(natural);
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(caveGrid);
            Assert.That(maps.GetTileRef(caveGrid, grid, natural).Tile.IsEmpty, Is.False);
            var anchored = maps.GetAnchoredEntities(caveGrid, grid, natural);
            Assert.That(anchored.MoveNext(), Is.False,
                "Even a stale burial request cannot fill a natural cavern with rock.");
            SEntMan.DeleteEntity(caveMap);
        });
    }

    private static Vector2i FindShaft(CMUCavernField field)
    {
        for (var x = 24; x < 72; x++)
        for (var y = 24; y < 72; y++)
        {
            var tile = new Vector2i(x, y);
            if (field.IsOpen(tile, -3) && field.IsOpen(tile, -4) && field.IsOpen(tile, -5))
                return tile;
        }
        throw new InvalidOperationException("The fixture requires a multi-level shaft.");
    }

    [Test]
    public async Task FloodedGeologyPlacesWaterOnGroundButNotInTheShaftAbove()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.Caverns = new CMUCavernSettings
            {
                StartDepth = -1,
                BiomeWeights = new() { [CMUCavernVariant.FloodedFork] = 1 },
            };
            settings.DepthBiomes.Clear();
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            SEntMan.EnsureComponent<CMUCavernWorldComponent>(map.MapUid).Seed = 12345;
            var field = new CMUCavernField(12345, settings.Caverns);
            var site = (from x in Enumerable.Range(0, 96)
                        from y in Enumerable.Range(0, 96)
                        from depth in Enumerable.Range(2, 5).Select(d => -d)
                        let tile = new Vector2i(x, y)
                        where field.IsOpen(tile, depth) && !field.IsOpen(tile, depth - 1) &&
                              field.IsOpen(tile, depth + 1) && field.SceneryAt(tile, depth) == CMUCavernScenery.Water
                        select (tile, depth)).First();
            var building = Server.System<ZLevelBuildingSystem>();
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out var first, out var grid), Is.True);
            var stone = SEntMan.EnsureComponent<ZGeneratedStoneComponent>(first);
            stone.StoneGrid = grid;
            building.EnsureChunkAt((first, stone), first, site.tile);
            var maps = Server.System<SharedMapSystem>();
            var levels = new List<EntityUid>();
            var current = first;
            for (var depth = -1; depth >= -7; depth--)
            {
                levels.Add(current);
                var slice = SEntMan.GetComponent<ZGeneratedStoneComponent>(current);
                var sliceGrid = SEntMan.GetComponent<MapGridComponent>(slice.StoneGrid);
                if (depth == site.depth)
                {
                    var water = false;
                    foreach (var entity in maps.GetAnchoredEntities(slice.StoneGrid, sliceGrid, site.tile))
                        water |= SEntMan.GetComponent<MetaDataComponent>(entity).EntityPrototype?.ID == "CMUCavernWater";
                    Assert.That(water, Is.True, "The generated environment must use its flooded profile.");
                    Assert.That(maps.GetTileRef(slice.StoneGrid, sliceGrid, site.tile).Tile.IsEmpty, Is.False);
                }
                if (depth == site.depth + 1)
                {
                    Assert.That(maps.GetTileRef(slice.StoneGrid, sliceGrid, site.tile).Tile.IsEmpty, Is.True);
                    Assert.That(maps.GetAnchoredEntities(slice.StoneGrid, sliceGrid, site.tile).MoveNext(), Is.False,
                        "Liquid and decoration must not float in a natural shaft.");
                }
                if (depth > -7)
                    current = SEntMan.GetComponent<CMUZLevelMapComponent>(current).MapBelow!.Value;
            }
            foreach (var level in levels)
                SEntMan.DeleteEntity(level);
        });
    }

    [Test]
    public async Task ExplorationGeneratesNearbyChunksGraduallyAndIgnoresGhosts()
    {
        var map = await Pair.CreateTestMap();
        EntityUid cave = default;
        EntityUid observer = default;
        EntityUid explorer = default;
        var levels = new List<EntityUid>();
        var session = Pair.Player!;
        var original = session.AttachedEntity;
        await Server.WaitAssertion(() =>
        {
            var building = Server.System<ZLevelBuildingSystem>();
            var settings = SEntMan.EnsureComponent<ZBuildableMapComponent>(map.MapUid);
            settings.Caverns = new CMUCavernSettings { StartDepth = -1 };
            settings.DepthBiomes.Clear();
            settings.StoneOreVeins.Clear();
            settings.StoneDeposits.Clear();
            settings.StoneCaveDecor.Clear();
            settings.StoneWater = null;
            Assert.That(building.EnsureNeighborLevel(map.MapUid, -1, map.Grid.Owner, Vector2.Zero, out cave, out var grid), Is.True);
            SEntMan.EnsureComponent<ZGeneratedStoneComponent>(cave).StoneGrid = grid;
            observer = SEntMan.SpawnEntity("MobObserver", new EntityCoordinates(grid, new Vector2(0.5f, 0.5f)));
            Server.PlayerMan.SetAttachedEntity(session, observer);
        });
        await RunSeconds(1);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ZGeneratedStoneComponent>(cave).GeneratedChunks, Is.Empty);
            explorer = SEntMan.SpawnEntity(null, new EntityCoordinates(cave, new Vector2(0.5f, 0.5f)));
            Server.PlayerMan.SetAttachedEntity(session, explorer);
        });
        await RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var current = cave;
            var total = 0;
            for (var depth = -1; depth >= -7; depth--)
            {
                levels.Add(current);
                var stone = SEntMan.GetComponent<ZGeneratedStoneComponent>(current);
                total += stone.GeneratedChunks.Count;
                Assert.That(stone.GeneratedChunks.All(chunk => Math.Abs(chunk.X) <= 3 && Math.Abs(chunk.Y) <= 3), Is.True);
                if (depth > -7)
                    current = SEntMan.GetComponent<CMUZLevelMapComponent>(current).MapBelow!.Value;
            }
            Assert.That(total, Is.GreaterThan(0));
            Assert.That(total, Is.LessThan(7 * 49), "The prefetch volume must be spread over updates.");
            TestContext.Out.WriteLine($"Exploration after 0.5 s: {total} of 343 nearby chunks materialized.");
            Server.PlayerMan.SetAttachedEntity(session, original);
            SEntMan.DeleteEntity(observer);
            SEntMan.DeleteEntity(explorer);
            foreach (var level in levels)
                SEntMan.DeleteEntity(level);
        });
    }
}
