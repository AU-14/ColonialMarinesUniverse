using System.Numerics;
using Content.Server.CMU14.ZLevels.Core;
using Content.Shared.CMU14.ZLevelBuilding;
using Content.Shared.CMU14.ZLevels.Core.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU.ZLevelBuilding;

[TestFixture]
public sealed class SupportActivationTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: CMUTestWakeWall
  components:
  - type: Transform
    anchored: true
  - type: ZLevelWallSupport

- type: entity
  id: CMUTestWakeHighGround
  components:
  - type: Transform
    anchored: true
  - type: CMUZLevelHighGround
    heightCurve: [1, 1]
    stick: true

- type: entity
  id: CMUTestWakeBody
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      body:
        shape:
          !type:PhysShapeAabb
          bounds: '-0.2,-0.2,0.2,0.2'
        hard: true
        layer: 0
        mask: 0
  - type: CMUZPhysics
    bounciness: 0

- type: entity
  id: CMUTestWakeVehicle
  parent: CMUTestWakeBody
  components:
  - type: CMUVehicleZTraversal
    edgeTipUnsupportedFraction: 0.9
  - type: Fixtures
    fixtures:
      body:
        shape:
          !type:PhysShapeAabb
          bounds: '-2.4,-0.2,0.4,0.2'
        hard: true
        layer: 0
        mask: 0
";

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public async Task LosingVirtualSupportWakesBodyOnMapAbove(bool highGround, bool unanchor)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid support = default;
        EntityUid body = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            (lower, upper) = CreateLayers(entities, testMap.Tile.Tile);
            support = entities.SpawnEntity(highGround ? "CMUTestWakeHighGround" : "CMUTestWakeWall", new EntityCoordinates(lower, 0.5f, 0.5f));
            body = entities.SpawnEntity("CMUTestWakeBody", new EntityCoordinates(upper, 0.5f, 0.5f));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.HasComponent<CMUZFallingComponent>(body), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(upper));
            entities.System<SharedPhysicsSystem>().SetAwake(body, entities.GetComponent<PhysicsComponent>(body), false);
            Assert.That(entities.GetComponent<PhysicsComponent>(body).Awake, Is.False);
            if (unanchor)
                entities.System<SharedTransformSystem>().Unanchor(support);
            else if (highGround)
                entities.RemoveComponent<CMUZLevelHighGroundComponent>(support);
            else
                entities.DeleteEntity(support);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<CMUZFallingComponent>(body), Is.True));
        await pair.RunTicksSync(80);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(lower));
            Assert.That(server.EntMan.HasComponent<CMUZFallingComponent>(body), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task RemovingHighGroundWakesBodyOnItsOwnMap()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        EntityUid lower = default;
        EntityUid support = default;
        EntityUid body = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            (lower, _) = CreateLayers(entities, testMap.Tile.Tile);
            support = entities.SpawnEntity("CMUTestWakeHighGround", new EntityCoordinates(lower, 0.5f, 0.5f));
            entities.GetComponent<CMUZLevelHighGroundComponent>(support).HeightCurve = [0.25f, 0.25f];
            body = entities.SpawnEntity("CMUTestWakeBody", new EntityCoordinates(lower, 0.5f, 0.5f));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.GetComponent<CMUZPhysicsComponent>(body).LocalPosition, Is.EqualTo(0.25f).Within(0.01));
            Assert.That(entities.HasComponent<CMUZFallingComponent>(body), Is.False);
            entities.System<SharedPhysicsSystem>().SetAwake(body, entities.GetComponent<PhysicsComponent>(body), false);
            entities.RemoveComponent<CMUZLevelHighGroundComponent>(support);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<CMUZFallingComponent>(body), Is.True);
            Assert.That(server.EntMan.GetComponent<CMUZPhysicsComponent>(body).LocalPosition, Is.LessThan(0.25f));
        });
        await pair.RunTicksSync(50);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<TransformComponent>(body).MapUid, Is.EqualTo(lower));
            Assert.That(server.EntMan.GetComponent<CMUZPhysicsComponent>(body).LocalPosition, Is.EqualTo(0).Within(0.05));
            Assert.That(server.EntMan.HasComponent<CMUZFallingComponent>(body), Is.False);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task LossOfEdgeSupportWakesVehicleWhoseOriginIsOutsideChangedTile()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        EntityUid lower = default;
        EntityUid upper = default;
        EntityUid wall = default;
        EntityUid vehicle = default;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            (lower, upper) = CreateLayers(entities, testMap.Tile.Tile);
            wall = entities.SpawnEntity("CMUTestWakeWall", new EntityCoordinates(lower, 0.5f, 0.5f));
            vehicle = entities.SpawnEntity("CMUTestWakeVehicle", new EntityCoordinates(upper, 2.5f, 0.5f));
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            Assert.That(entities.HasComponent<CMUZFallingComponent>(vehicle), Is.False);
            Assert.That(entities.GetComponent<TransformComponent>(vehicle).MapUid, Is.EqualTo(upper));
            Assert.That(entities.System<SharedTransformSystem>().GetWorldPosition(vehicle).X, Is.GreaterThan(2));
            entities.System<SharedPhysicsSystem>().SetAwake(vehicle, entities.GetComponent<PhysicsComponent>(vehicle), false);
            entities.DeleteEntity(wall);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<CMUZFallingComponent>(vehicle), Is.True));
        await pair.RunTicksSync(80);
        await server.WaitAssertion(() => Assert.That(server.EntMan.GetComponent<TransformComponent>(vehicle).MapUid, Is.EqualTo(lower)));
        await pair.CleanReturnAsync();
    }

    private static (EntityUid Lower, EntityUid Upper) CreateLayers(IEntityManager entities, Tile tile)
    {
        var maps = entities.System<SharedMapSystem>();
        var lower = maps.CreateMap(out _, runMapInit: true);
        var upper = maps.CreateMap(out _, runMapInit: true);
        var lowerGrid = entities.EnsureComponent<MapGridComponent>(lower);
        var upperGrid = entities.EnsureComponent<MapGridComponent>(upper);
        for (var x = 0; x < 3; x++)
            maps.SetTile(lower, lowerGrid, new Vector2i(x, 0), tile);
        maps.SetTile(upper, upperGrid, new Vector2i(8, 0), tile);
        var levels = entities.System<CMUZLevelsSystem>();
        Assert.That(levels.TryAddMapsIntoZNetwork(levels.CreateZNetwork(), new() { [lower] = 0, [upper] = 1 }), Is.True);
        return (lower, upper);
    }
}
