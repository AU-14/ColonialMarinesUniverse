using Content.Server._AU14.ZLevelBuilding;
using Content.Server._CMU14.ZLevels.Core;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Components;
using Content.Shared._AU14.ZLevelBuilding;
using Content.Shared._AU14.SavedBuilds;
using Content.Shared._CMU14.ZLevels.Core.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Shuttles.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests._AU14.ZLevelBuilding;

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
            var world = transforms.ToMapCoordinates(map.GridCoords).Position;

            Assert.That(building.EnsureNeighborLevel(sourceMap, -1, map.Grid.Owner, world, out var authoredMap, out var authoredGrid), Is.True);
            entities.EnsureComponent<BecomesStationComponent>(authoredGrid);
            var maps = entities.System<SharedMapSystem>();
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
}
