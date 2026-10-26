using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14;

[TestFixture]
public sealed class CMUVehiclePushDirectionTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = """
        - type: entity
          id: CMUVehicleSlideTestObstacle
          components:
          - type: Physics
            bodyType: Static
          - type: Fixtures
            fixtures:
              wall:
                shape: !type:PhysShapeAabb
                  bounds: '-0.05,-0.05,0.05,0.05'
                hard: true
                mask: [MobMask]
                layer: [Impassable]
        """;

    [TestCase("CMXenoRunner", 45, 0.8f)]
    [TestCase("CMXenoRunner", 45, -0.8f)]
    [TestCase("CMXenoRunner", 0, 0.8f)]
    [TestCase("CMMobHuman", 45, 0.8f)]
    public async Task ReversingContactSlidesXenosToTheNearSideAndKeepsHumanRunover(
        string prototype, int degrees, float side)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var transform = SEntMan.System<SharedTransformSystem>();
            var rotation = Angle.FromDegrees(degrees);
            transform.SetLocalRotation(vehicle, rotation);
            var mover = SEntMan.GetComponent<GridVehicleMoverComponent>(vehicle);
            var vehicleTransform = SEntMan.GetComponent<TransformComponent>(vehicle);
            var grid = vehicleTransform.GridUid!.Value;
            var start = transform.WithEntityId(vehicleTransform.Coordinates, grid).Position;
#pragma warning disable RA0002
            mover.Position = start;
#pragma warning restore RA0002
            var target = start + rotation.RotateVec(new Vector2(0, 0.2f));
            var mob = SEntMan.SpawnEntity(prototype,
                new EntityCoordinates(grid, target + rotation.RotateVec(new Vector2(side, 1.5f))));
            var before = transform.GetWorldPosition(mob);
            var collision = typeof(GridVehicleMoverSystem).GetMethod("CanOccupyTransform", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var clear = (bool) collision.Invoke(SEntMan.System<GridVehicleMoverSystem>(),
                [vehicle, mover, grid, target, rotation, 0f, true, false, null, null, null, null])!;

            Assert.That(clear, Is.True);
            var displacement = (-rotation).RotateVec(transform.GetWorldPosition(mob) - before);
            if (prototype == "CMMobHuman")
            {
                Assert.That(displacement.Length(), Is.LessThan(0.001f), "Humans must keep their existing run-over behavior.");
                Assert.That(SEntMan.HasComponent<VehicleRunoverComponent>(mob), Is.True);
                return;
            }

            Assert.That(displacement.X * Math.Sign(side), Is.GreaterThan(0.1f),
                $"The xeno must slide toward the nearer side (start {start}, target {target}, mob {before}, displacement {displacement}).");
            Assert.That(displacement.Y, Is.EqualTo(0).Within(0.01f), "Reversing must slide the xeno along the rear bumper.");
            Assert.That(displacement.Length(), Is.LessThan(1.5f), "Do not send a corner contact across the hull.");
        });
    }

    [TestCase(1.4f)]
    [TestCase(2.3f)]
    public async Task SideSlideRefusesObstaclesAlongPathOrAtDestination(float obstacleX)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var vehicle = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var mob = SEntMan.SpawnEntity("CMXenoRunner", map.GridCoords.Offset(new Vector2(0.5f, -2.1f)));
            SEntMan.SpawnEntity("CMUVehicleSlideTestObstacle", map.GridCoords.Offset(new Vector2(obstacleX, -2.1f)));
            var transform = SEntMan.System<SharedTransformSystem>();
            var hull = new Box2Rotated(Box2.CenteredAround(transform.GetWorldPosition(vehicle), new Vector2(4, 4)));
            var mobBounds = Box2.CenteredAround(transform.GetWorldPosition(mob), new Vector2(0.6f, 0.6f));
            var method = typeof(GridVehicleMoverSystem).GetMethod("TryGetMobPush", BindingFlags.Instance | BindingFlags.NonPublic)!;
            object[] args = [vehicle, mob, hull, mobBounds, new Vector2(0, -0.1f), EntityCoordinates.Invalid];

            Assert.That(method.Invoke(SEntMan.System<GridVehicleMoverSystem>(), args), Is.False,
                "A blocked near-side slide must not cross the obstacle or fall back to the far side.");
            Assert.That((EntityCoordinates) args[5], Is.EqualTo(EntityCoordinates.Invalid));
        });
    }
}
