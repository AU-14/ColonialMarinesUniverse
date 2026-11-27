using Content.Server.CMU14.ZLevelBuilding;
using Content.Server.CMU14.ZLevels.Core;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Shared.CMU14.ZLevelBuilding;
// CMU14: using Content.Shared.CMU14.SavedBuilds;
using Content.Shared.CMU14.Construction;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.ZLevelBuilding;

[TestFixture]
public sealed class TileApplierSystemTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: AU14TestAnchoredTileDependent
  components:
  - type: Transform
    anchored: true

- type: entity
  id: AU14TestStructuralAnchor
  components:
  - type: Transform
    anchored: true
  - type: StructuralSupport
    isAnchor: true
    isVerticalSupport: true
    cantileverSpan: 3

- type: entity
  id: AU14TestStructuralWall
  components:
  - type: Transform
    anchored: true
  - type: ZLevelWallSupport

- type: entity
  id: AU14TestZPhysicsEntity
  components:
  - type: Transform
  - type: Physics
    bodyType: Dynamic
  - type: CMUZPhysics

- type: entity
  id: CMUTestFootprintStructure
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
";

    [Test]
    public async Task DeletingFloorSupportDefersTileRemovalPastTermination()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid support = default;
        EntityUid dependent = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapSystem = entities.System<SharedMapSystem>();
            mapSystem.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(1, 0), map.Tile.Tile);
            support = entities.SpawnEntity("AU14TileFloorSupport", map.GridCoords);
            dependent = entities.SpawnEntity("AU14TestAnchoredTileDependent", map.GridCoords);

            Assert.That(entities.HasComponent<TileFloorSupportComponent>(support), Is.True);
            Assert.DoesNotThrow(() => entities.DeleteEntity(support));
        });

        await pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mapSystem = entities.System<SharedMapSystem>();
            var tile = mapSystem.GetTileRef(map.Grid.Owner, map.Grid.Comp, map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(tile.Tile.IsEmpty, Is.True);
                Assert.That(entities.Deleted(support), Is.True);
                Assert.That(entities.Deleted(dependent), Is.False);
                Assert.That(entities.GetComponent<TransformComponent>(dependent).Anchored, Is.False);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task ColocatedSupportsRemainValidWhenEitherIsRemoved()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid first = default;
        EntityUid second = default;
        await server.WaitAssertion(() =>
        {
            first = server.EntMan.SpawnEntity("AU14TestStructuralAnchor", map.GridCoords);
            second = server.EntMan.SpawnEntity("AU14TestStructuralAnchor", map.GridCoords);

            var supports = server.EntMan.System<ZLevelSupportSystem>();
            supports.RecomputeGrid(map.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(first).Supported, Is.True);
                Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(second).Supported, Is.True);
            });

            server.EntMan.DeleteEntity(first);
            supports.RecomputeGrid(map.Grid);
            Assert.That(server.EntMan.GetComponent<StructuralSupportComponent>(second).Supported, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingLastBeamCollapsesDependentUpperFloor()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        EntityUid upperGrid = default;
        Vector2i upperTile = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transforms = entities.System<SharedTransformSystem>();
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            var mapUid = entities.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
            var world = transforms.ToMapCoordinates(map.GridCoords).Position;

            var beam = entities.SpawnEntity("AU14NavalisSupportBeamGreen1Tile", map.GridCoords);
            Assert.That(building.EnsureNeighborLevel(mapUid, 1, map.Grid.Owner, world, out var upperMap, out upperGrid), Is.True);

            var upperMapComp = entities.GetComponent<MapComponent>(upperMap);
            var upperCoords = transforms.ToCoordinates(upperGrid, new MapCoordinates(world, upperMapComp.MapId));
            entities.SpawnEntity("AU14TileApplierPlating", upperCoords);

            var upperGridComp = entities.GetComponent<MapGridComponent>(upperGrid);
            upperTile = maps.TileIndicesFor(upperGrid, upperGridComp, upperCoords);
            var authoredTile = upperTile + new Vector2i(1, 0);
            maps.SetTile(upperGrid, upperGridComp, authoredTile, map.Tile.Tile);
            entities.SpawnEntity("AU14TileApplierPlating", maps.GridTileToLocal(upperGrid, upperGridComp, authoredTile));
            entities.DeleteEntity(beam);
        });

        // Five-second warning plus scheduling slack.
        await pair.RunTicksSync(400);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var upperGridComp = entities.GetComponent<MapGridComponent>(upperGrid);
            Assert.That(maps.GetTileRef(upperGrid, upperGridComp, upperTile).Tile.IsEmpty, Is.True);
            var resurfaced = upperTile + new Vector2i(1, 0);
            Assert.That(maps.GetTileRef(upperGrid, upperGridComp, resurfaced).Tile.IsEmpty, Is.False,
                "Replacing the finish on an authored ceiling must not make it collapse with its neighbor.");
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WallSupportsPlayerBuiltFloorAbove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transforms = entities.System<SharedTransformSystem>();
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            var supportSystem = entities.System<ZLevelSupportSystem>();
            var sourceMap = entities.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
            var world = transforms.ToMapCoordinates(map.GridCoords).Position;

            var wall = entities.SpawnEntity("AU14TestStructuralWall", map.GridCoords);
            Assert.That(entities.HasComponent<StructuralSupportComponent>(wall), Is.False);
            Assert.That(entities.HasComponent<ZLevelWallSupportComponent>(wall), Is.True);
            Assert.That(building.EnsureNeighborLevel(sourceMap, 1, map.Grid.Owner, world, out var upperMap, out var upperGrid), Is.True);

            var upperMapComp = entities.GetComponent<MapComponent>(upperMap);
            var upperGridComp = entities.GetComponent<MapGridComponent>(upperGrid);
            var upperCoords = transforms.ToCoordinates(upperGrid, new MapCoordinates(world, upperMapComp.MapId));
            var upperTile = maps.TileIndicesFor(upperGrid, upperGridComp, upperCoords);
            maps.SetTile(upperGrid, upperGridComp, upperTile, map.Tile.Tile);
            var marker = entities.SpawnEntity("AU14TileFloorSupport", upperCoords);

            supportSystem.RecomputeGrid((upperGrid, upperGridComp));
            Assert.That(entities.GetComponent<StructuralSupportComponent>(marker).Supported, Is.True);

            entities.DeleteEntity(wall);
            supportSystem.RecomputeGrid((upperGrid, upperGridComp));
            Assert.That(entities.GetComponent<StructuralSupportComponent>(marker).Supported, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PlayerBuiltWallJoinsCollapsibleSupportGraph()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var mappedWall = entities.SpawnEntity("CMWallMetal", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<ZLevelWallSupportComponent>(mappedWall), Is.True);
                Assert.That(entities.HasComponent<StructuralSupportComponent>(mappedWall), Is.False);
            });

            var menuWall = entities.SpawnEntity("AU14BuildWallMetal", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<ZLevelWallSupportComponent>(menuWall), Is.True);
                Assert.That(entities.HasComponent<StructuralSupportComponent>(menuWall), Is.True);
            });

            var wall = entities.SpawnEntity("AU14TestStructuralWall", map.GridCoords);
            Assert.That(entities.HasComponent<StructuralSupportComponent>(wall), Is.False);

            entities.EnsureComponent<PlayerBuiltComponent>(wall);

            Assert.That(entities.TryGetComponent<StructuralSupportComponent>(wall, out var support), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(support!.IsVerticalSupport, Is.True);
                Assert.That(support.CantileverSpan, Is.EqualTo(ZLevelWallSupportComponent.CantileverSpan));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task UpperLevelGirderDoesNotSupportItsOwnLevel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transforms = entities.System<SharedTransformSystem>();
            var maps = entities.System<SharedMapSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            var sourceMap = entities.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
            var world = transforms.ToMapCoordinates(map.GridCoords).Position;

            Assert.That(building.EnsureNeighborLevel(sourceMap, 1, map.Grid.Owner, world, out var upperMap, out var upperGrid), Is.True);
            var upperMapComp = entities.GetComponent<MapComponent>(upperMap);
            var upperGridComp = entities.GetComponent<MapGridComponent>(upperGrid);
            var centerCoords = transforms.ToCoordinates(upperGrid, new MapCoordinates(world, upperMapComp.MapId));
            var centerTile = maps.TileIndicesFor(upperGrid, upperGridComp, centerCoords);
            var neighborTile = centerTile + new Vector2i(1, 0);
            maps.SetTile(upperGrid, upperGridComp, centerTile, map.Tile.Tile);
            maps.SetTile(upperGrid, upperGridComp, neighborTile, map.Tile.Tile);

            entities.SpawnEntity("AU14NavalisSupportBeamGreen1Tile", centerCoords);
            entities.SpawnEntity("AU14TileFloorSupport", centerCoords);
            var neighborMarker = entities.SpawnEntity(
                "AU14TileFloorSupport",
                maps.GridTileToLocal(upperGrid, upperGridComp, neighborTile));

            entities.System<ZLevelSupportSystem>().RecomputeGrid((upperGrid, upperGridComp));
            Assert.That(entities.GetComponent<StructuralSupportComponent>(neighborMarker).Supported, Is.False);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task WallBelowActsAsWalkableGroundForOpeningAbove()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transforms = entities.System<SharedTransformSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            var sourceMap = entities.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
            var world = transforms.ToMapCoordinates(map.GridCoords).Position;

            entities.SpawnEntity("AU14TestStructuralWall", map.GridCoords);
            Assert.That(building.EnsureNeighborLevel(sourceMap, 1, map.Grid.Owner, world, out var upperMap, out _), Is.True);

            var upperMapComp = entities.GetComponent<MapComponent>(upperMap);
            var entity = entities.SpawnEntity("AU14TestZPhysicsEntity", new MapCoordinates(world, upperMapComp.MapId));
            var physics = entities.GetComponent<PhysicsComponent>(entity);
            entities.System<SharedPhysicsSystem>().SetBodyStatus(entity, physics, BodyStatus.InAir);
            entities.EnsureComponent<CMUZFallingComponent>(entity);

            var zLevels = entities.System<CMUZLevelsSystem>();
            var distance = zLevels.DistanceToGround((entity, null), out var stickyGround);
            zLevels.WakeZPhysics((entity, null));
            var movementGround = new IsVirtualGroundForMovementEvent();
            entities.EventBus.RaiseLocalEvent(entity, ref movementGround);

            Assert.Multiple(() =>
            {
                Assert.That(distance, Is.Zero.Within(0.001f));
                Assert.That(stickyGround, Is.True);
                Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround));
                Assert.That(movementGround.Grounded, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task DownStairGeneratesLocalizedCaveOnAuthoredStationLevel()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var transforms = entities.System<SharedTransformSystem>();
            var building = entities.System<ZLevelBuildingSystem>();
            var sourceMap = entities.GetComponent<TransformComponent>(map.Grid.Owner).MapUid!.Value;
            var maps = entities.System<SharedMapSystem>();
            // CMU14: the cavern seal beds rock around the generated footprint, so the stair needs a
            // source patch with its landing off the footprint edge
            for (var x = 0; x < 5; x++)
            for (var y = 0; y < 5; y++)
                maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
            var world = transforms.ToMapCoordinates(new EntityCoordinates(map.Grid.Owner, 2.5f, 2.5f)).Position;

            Assert.That(building.EnsureNeighborLevel(sourceMap, -1, map.Grid.Owner, world, out var authoredMap, out var authoredGrid), Is.True);
            entities.EnsureComponent<BecomesStationComponent>(authoredGrid);
            var authoredGridComp = entities.GetComponent<MapGridComponent>(authoredGrid);
            var landingTile = maps.WorldToTile(authoredGrid, authoredGridComp, world);
            var protectedTile = landingTile + new Vector2i(2, 0);
            maps.SetTile(authoredGrid, authoredGridComp, protectedTile, map.Tile.Tile);

            Assert.That(building.PrepareStoneForStair(sourceMap, map.Grid.Owner, world, out var targetGrid), Is.True);
            var stone = entities.GetComponent<ZGeneratedStoneComponent>(authoredMap);
            Assert.Multiple(() =>
            {
                Assert.That(targetGrid, Is.EqualTo(authoredGrid));
                Assert.That(stone.LocalizedToAuthoredLevel, Is.True);
                Assert.That(stone.StoneGrid, Is.EqualTo(authoredGrid));
                Assert.That(stone.GeneratedTiles, Does.Contain(landingTile));
                Assert.That(stone.GeneratedTiles, Does.Not.Contain(protectedTile));
                Assert.That(maps.GetTileRef(authoredGrid, authoredGridComp, protectedTile).Tile, Is.EqualTo(map.Tile.Tile));
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RotatedDownStairRotatesCompanionAndBeamPlacement()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var transforms = entities.System<SharedTransformSystem>();
            var rotation = Angle.FromDegrees(90);
            var stair = entities.Spawn("AU14ZStairDown", map.MapCoords, rotation: rotation);
            var sourceMap = entities.GetComponent<TransformComponent>(stair).MapUid!.Value;
            var belowMap = entities.GetComponent<CMUZLevelMapComponent>(sourceMap).MapBelow!.Value;
            var belowGrid = entities.GetComponent<MapGridComponent>(belowMap);
            var world = transforms.GetWorldPosition(stair);
            var stairTile = maps.WorldToTile(belowMap, belowGrid, world);
            var beamTile = stairTile + new Vector2i(-1, 0);

            EntityUid? companion = null;
            foreach (var anchored in maps.GetAnchoredEntities(belowMap, belowGrid, stairTile))
            {
                if (entities.GetComponent<MetaDataComponent>(anchored).EntityPrototype?.ID == "AU14ZStairPure")
                    companion = anchored;
            }

            var hasBeam = false;
            foreach (var anchored in maps.GetAnchoredEntities(belowMap, belowGrid, beamTile))
            {
                if (entities.GetComponent<MetaDataComponent>(anchored).EntityPrototype?.ID == "AU14NavalisSupportBeamBlue1Tile")
                    hasBeam = true;
            }

            Assert.Multiple(() =>
            {
                Assert.That(companion, Is.Not.Null);
                Assert.That(entities.GetComponent<TransformComponent>(companion!.Value).LocalRotation.Theta,
                    Is.EqualTo(rotation.Theta).Within(0.001));
                Assert.That(hasBeam, Is.True);
            });
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task CompromisedShuttleIsRejectedByGenericFtlGate()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            server.EntMan.EnsureComponent<ShuttleComponent>(map.Grid.Owner);
            server.EntMan.EnsureComponent<ZCollapseCompromisedComponent>(map.Grid.Owner);

            var shuttle = server.EntMan.System<ShuttleSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(shuttle.CanFTL(map.Grid.Owner, out var reason), Is.False);
                Assert.That(reason, Is.Not.Empty);
            });
        });

        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-03. A plating over open air on Z+1 with nothing below is refused before materials go.
    [Test]
    public async Task FloorConditionRefusesUnsupportedFloorOverOpenAir()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            var maps = entities.System<SharedMapSystem>();
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, System.Numerics.Vector2.Zero, out _, out var upperGrid), Is.True);
            var upper = new Entity<MapGridComponent>(upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            var coords = maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero);
            Assert.That(maps.GetTileRef(upper, Vector2i.Zero).Tile.IsEmpty, Is.True);
            Assert.That(new ZFloorWouldBeSupported().Condition(EntityUid.Invalid, coords, Direction.South), Is.False,
                "Nothing would hold this floor up.");
        });

        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-03. With a wall below, the same plating is allowed, and once laid it stands.
    [Test]
    public async Task FloorConditionAllowsFloorOverWallAndTheFloorStands()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            var maps = entities.System<SharedMapSystem>();
            entities.SpawnEntity("AU14TestStructuralWall", map.GridCoords);
            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, System.Numerics.Vector2.Zero, out _, out var upperGrid), Is.True);
            upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            var coords = maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero);
            Assert.That(new ZFloorWouldBeSupported().Condition(EntityUid.Invalid, coords, Direction.South), Is.True);

            // What the completed construction spawns: the applier lays the tile and its support marker.
            entities.SpawnEntity("AU14TileApplierPlating", coords);
        });

        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.That(maps.GetTileRef(upper, Vector2i.Zero).Tile.IsEmpty, Is.False, "The supported floor must not drop.");
        });

        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit ARCH-01. A wall enrolled without a player holds the floor above within its span; when it
    // goes, only the floor it held collapses, and it never became PlayerBuilt.
    [Test]
    public async Task EnrolledNonPlayerWallHoldsFloorAndCollapsesOnlyItsSpan()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;
        EntityUid enrolled = default;
        EntityUid near = default;
        EntityUid far = default;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            var maps = entities.System<SharedMapSystem>();
            for (var x = 0; x <= 5; x++)
                maps.SetTile(map.Grid, new Vector2i(x, 0), map.Tile.Tile);

            enrolled = entities.SpawnEntity("AU14TestStructuralWall", map.GridCoords);
            entities.System<ZLevelSupportSystem>().Enroll(enrolled, ZLevelWallSupportComponent.CantileverSpan, true);
            entities.SpawnEntity("AU14TestStructuralWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, new Vector2i(5, 0)));

            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, System.Numerics.Vector2.Zero, out _, out var upperGrid), Is.True);
            upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            // One contiguous upper floor; only x = 0, 1 and 5 are player floors with markers.
            for (var x = 0; x <= 5; x++)
                maps.SetTile(upper, new Vector2i(x, 0), map.Tile.Tile);
            entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero));
            near = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, new Vector2i(1, 0)));
            far = entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, new Vector2i(5, 0)));
        });

        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.Multiple(() =>
            {
                Assert.That(entities.HasComponent<PlayerBuiltComponent>(enrolled), Is.False);
                Assert.That(entities.HasComponent<ZEnrolledSupportComponent>(enrolled), Is.True);
                Assert.That(entities.GetComponent<StructuralSupportComponent>(enrolled).Supported, Is.True);
                Assert.That(entities.GetComponent<StructuralSupportComponent>(near).Supported, Is.True);
                Assert.That(entities.GetComponent<StructuralSupportComponent>(far).Supported, Is.True);
            });
            entities.DeleteEntity(enrolled);
        });

        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var maps = server.EntMan.System<SharedMapSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(upper, Vector2i.Zero).Tile.IsEmpty, Is.True);
                Assert.That(maps.GetTileRef(upper, new Vector2i(1, 0)).Tile.IsEmpty, Is.True);
                Assert.That(maps.GetTileRef(upper, new Vector2i(5, 0)).Tile.IsEmpty, Is.False,
                    "A floor held by another wall must not collapse.");
            });
        });

        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-06, written test-first. A 2x2 structure anchored on its origin falls when a covered
    // non-origin tile collapses. Without footprint cells only the entities anchored on the collapsing tile move, so
    // the structure is left floating; this test fails on that code.
    [Test]
    public async Task FootprintStructureFallsWhenACoveredTileCollapses()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;
        EntityUid upperMap = default;
        EntityUid wall = default;
        EntityUid structure = default;
        var covered = new Vector2i(1, 0);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            var maps = entities.System<SharedMapSystem>();
            maps.SetTile(map.Grid, covered, map.Tile.Tile);
            wall = entities.SpawnEntity("AU14TestStructuralWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, covered));

            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, System.Numerics.Vector2.Zero, out upperMap, out var upperGrid), Is.True);
            upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            // The origin and the top row are authored floor; the covered tile is a player floor held by the wall.
            foreach (var tile in new[] { Vector2i.Zero, covered, new Vector2i(0, 1), new Vector2i(1, 1) })
                maps.SetTile(upper, tile, map.Tile.Tile);
            entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, covered));
            structure = entities.SpawnEntity("CMUTestFootprintStructure", maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero));
            Assert.That(entities.GetComponent<ZFootprintComponent>(structure).Cells, Has.Count.EqualTo(3));
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => server.EntMan.DeleteEntity(wall));

        // Five-second warning plus scheduling slack.
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var cells = 0;
            foreach (var tile in new[] { new Vector2i(0, 1), new Vector2i(1, 1) })
            {
                foreach (var uid in maps.GetAnchoredEntities(upper.Owner, upper.Comp, tile))
                {
                    if (entities.HasComponent<ZFootprintCellComponent>(uid))
                        cells++;
                }
            }

            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(upper, covered).Tile.IsEmpty, Is.True, "The covered tile collapses.");
                Assert.That(maps.GetTileRef(upper, Vector2i.Zero).Tile.IsEmpty, Is.False, "The origin floor stands.");
                Assert.That(entities.Deleted(structure) || entities.GetComponent<TransformComponent>(structure).MapUid != upperMap,
                    Is.True, "The structure must fall with its covered tile, not float.");
                Assert.That(cells, Is.Zero, "The structure's other cells are cleaned up.");
            });
        });

        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-06 review. Two covered tiles of one structure collapse in the same tick; the structure
    // falls exactly once (it must not be moved and thrown again by the second tile's drop).
    [Test]
    public async Task FootprintStructureFallsOnceWhenTwoCoveredTilesCollapseTogether()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;
        EntityUid upperMap = default;
        EntityUid structure = default;
        var walls = new List<EntityUid>();
        var coveredA = new Vector2i(1, 0);
        var coveredB = new Vector2i(1, 1);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            var maps = entities.System<SharedMapSystem>();
            foreach (var tile in new[] { coveredA, coveredB })
            {
                maps.SetTile(map.Grid, tile, map.Tile.Tile);
                walls.Add(entities.SpawnEntity("AU14TestStructuralWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, tile)));
            }

            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, System.Numerics.Vector2.Zero, out upperMap, out var upperGrid), Is.True);
            upper = (upperGrid, entities.GetComponent<MapGridComponent>(upperGrid));
            foreach (var tile in new[] { Vector2i.Zero, coveredA, new Vector2i(0, 1), coveredB })
                maps.SetTile(upper, tile, map.Tile.Tile);
            // Both covered tiles of the right column are player floors, each held by its own wall.
            foreach (var tile in new[] { coveredA, coveredB })
                entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, tile));
            structure = entities.SpawnEntity("CMUTestFootprintStructure", maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero));
        });

        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            // Removing both walls in one tick schedules both floors with the same deadline.
            foreach (var wall in walls)
                server.EntMan.DeleteEntity(wall);
        });

        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(upper, coveredA).Tile.IsEmpty, Is.True);
                Assert.That(maps.GetTileRef(upper, coveredB).Tile.IsEmpty, Is.True);
                Assert.That(entities.GetComponent<TransformComponent>(structure).MapUid, Is.Not.EqualTo(upperMap),
                    "The structure must fall with its covered tiles.");
                Assert.That(entities.GetComponent<ZFallenDebrisComponent>(structure).FellFrom, Is.EquivalentTo(new[] { upperMap }),
                    "The structure must fall exactly once, from the upper level only.");
            });
        });

        await pair.CleanReturnAsync();
    }

    // CMU14: GraphAudit FLOW-06 review. A covered tile that is pried (emptied) loses its cell while the structure
    // stays; laying the tile again brings the cell back through the broadcast TileChangedEvent, so a later collapse
    // of that tile still takes the structure down. A child grid is used so the emptied tile really unanchors its cell.
    [Test]
    public async Task FootprintCellReturnsWhenACoveredTileIsLaidAgain()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        Entity<MapGridComponent> upper = default;
        EntityUid upperMap = default;
        EntityUid wall = default;
        EntityUid structure = default;
        var covered = new Vector2i(1, 0);

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var building = entities.System<ZLevelBuildingSystem>();
            var maps = entities.System<SharedMapSystem>();
            maps.SetTile(map.Grid, covered, map.Tile.Tile);
            wall = entities.SpawnEntity("AU14TestStructuralWall", maps.GridTileToLocal(map.Grid.Owner, map.Grid.Comp, covered));

            Assert.That(building.EnsureNeighborLevel(map.MapUid, 1, map.Grid.Owner, System.Numerics.Vector2.Zero, out upperMap, out _), Is.True);
            upper = maps.CreateGridEntity(entities.GetComponent<MapComponent>(upperMap).MapId);
            foreach (var tile in new[] { Vector2i.Zero, covered, new Vector2i(0, 1), new Vector2i(1, 1) })
                maps.SetTile(upper, tile, map.Tile.Tile);
            structure = entities.SpawnEntity("CMUTestFootprintStructure", maps.GridTileToLocal(upper.Owner, upper.Comp, Vector2i.Zero));
            Assert.That(entities.GetComponent<ZFootprintComponent>(structure).Cells, Has.Count.EqualTo(3));

            // Pry the covered tile: its cell goes, the structure stays.
            maps.SetTile(upper, covered, Tile.Empty);
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var footprint = entities.GetComponent<ZFootprintComponent>(structure);
            Assert.Multiple(() =>
            {
                Assert.That(entities.GetComponent<TransformComponent>(structure).Anchored, Is.True);
                Assert.That(footprint.Cells, Has.Count.EqualTo(2), "The pried tile's cell is gone.");
                Assert.That(CountFootprintCells(entities, upper, covered), Is.Zero);
            });

            // Lay the tile again, as a player floor held by the wall below.
            maps.SetTile(upper, covered, map.Tile.Tile);
            entities.SpawnEntity("AU14TileFloorSupport", maps.GridTileToLocal(upper.Owner, upper.Comp, covered));
        });

        await pair.RunTicksSync(2);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            var footprint = entities.GetComponent<ZFootprintComponent>(structure);
            EntityUid? cell = null;
            var cells = 0;
            foreach (var uid in maps.GetAnchoredEntities(upper.Owner, upper.Comp, covered))
            {
                if (!entities.HasComponent<ZFootprintCellComponent>(uid))
                    continue;

                cells++;
                cell = uid;
            }

            Assert.Multiple(() =>
            {
                Assert.That(cells, Is.EqualTo(1), "The covered tile gets exactly one cell back.");
                Assert.That(cell is { } refilled && footprint.Cells.Contains(refilled), Is.True,
                    "The new cell is listed by its owner.");
                Assert.That(footprint.Cells, Has.Count.EqualTo(3));
            });

            entities.DeleteEntity(wall);
        });

        // Five-second warning plus scheduling slack.
        await pair.RunTicksSync(400);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var maps = entities.System<SharedMapSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(maps.GetTileRef(upper, covered).Tile.IsEmpty, Is.True, "The re-laid tile collapses.");
                Assert.That(entities.GetComponent<TransformComponent>(structure).MapUid, Is.Not.EqualTo(upperMap),
                    "The structure falls with the re-laid covered tile.");
                Assert.That(entities.GetComponent<ZFallenDebrisComponent>(structure).FellFrom, Is.EquivalentTo(new[] { upperMap }));
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
}
