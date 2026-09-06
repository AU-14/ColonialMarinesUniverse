using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class GridVehicleMotionSimulatorTest
{
    [TestCase(1, 2, 1, 1)]
    [TestCase(-1, 2, 1, -1)]
    [TestCase(1, -2, -1, -1)]
    [TestCase(-1, -2, -1, 1)]
    [TestCase(1, 2, -1, 1)]
    [TestCase(1, -2, 1, -1)]
    [TestCase(1, 0, -1, -1)]
    [TestCase(-1, 0, -1, 1)]
    [TestCase(1, 0, 0, 1)]
    public void SteeringFollowsTravelDirection(
        float steering,
        float currentSpeed,
        float throttle,
        float expected)
    {
        var actual = GridVehicleMotionSimulator.GetEffectiveSteering(steering, currentSpeed, throttle);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(true, 0, true)]
    [TestCase(true, 2, true)]
    [TestCase(true, -2, true)]
    [TestCase(false, 0, false)]
    [TestCase(false, 0.01f, false)]
    [TestCase(false, -0.01f, false)]
    [TestCase(false, 0.011f, true)]
    [TestCase(false, -0.011f, true)]
    public void StationarySteeringRequiresTurnInPlace(
        bool turnInPlace,
        float currentSpeed,
        bool expected)
    {
        var actual = GridVehicleMotionSimulator.CanSteer(turnInPlace, currentSpeed);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [TestCase(-1f, 0f, 2f, 0f, true)]
    [TestCase(1f, 0f, 2f, 0f, false)]
    [TestCase(0f, -1f, 0f, 2f, true)]
    [TestCase(0f, 1f, 0f, 2f, false)]
    [TestCase(0f, 0f, 2f, 0f, false)]
    public void EmbeddedVehicleCanOnlyMoveAwayFromObstacle(
        float moveX,
        float moveY,
        float obstacleX,
        float obstacleY,
        bool expected)
    {
        Assert.That(
            GridVehicleMotionSimulator.IsMovingAwayFromObstacle(
                new Vector2(moveX, moveY),
                Vector2.Zero,
                new Vector2(obstacleX, obstacleY)),
            Is.EqualTo(expected));
    }

    [TestCase(0f, -2f, 0f, true)]
    [TestCase(0f, 2f, 0f, false)]
    [TestCase(2f, 0f, 0f, false)]
    [TestCase(2f, 0f, 90f, true)]
    [TestCase(-2f, 0f, 90f, false)]
    [TestCase(0f, 2f, 90f, false)]
    [TestCase(2f, -2f, 45f, true)]
    [TestCase(-2f, 2f, 45f, false)]
    [TestCase(0.75f, -2f, 0f, true)]
    [TestCase(2f, -1f, 0f, false)]
    public void PlowBonusRequiresFrontImpact(float obstacleX, float obstacleY, float rotationDegrees, bool expected)
    {
        var vehicleBounds = new Box2(-1f, -2f, 1f, 2f);
        var obstacleBounds = Box2.CenteredAround(new Vector2(obstacleX, obstacleY), new Vector2(0.5f));
        var rotation = Angle.FromDegrees(rotationDegrees);

        Assert.That(
            GridVehicleMotionSimulator.IsFrontImpact(Vector2.Zero, rotation, vehicleBounds, obstacleBounds),
            Is.EqualTo(expected));
    }

    [TestCase(2000f, 0.5f, 1f, 1000f)]
    [TestCase(2000f, 0.5f, 0.5f, 500f)]
    [TestCase(2000f, 0.5f, 2f, 1000f)]
    [TestCase(-1f, 0.5f, 1f, 0f)]
    [TestCase(2000f, -1f, 1f, 0f)]
    public void PoweredDemolitionDamageScalesWithIntervalAndPlowCondition(
        float damagePerSecond,
        float interval,
        float performance,
        float expected)
    {
        Assert.That(
            GridVehicleMotionSimulator.GetPoweredDemolitionDamage(damagePerSecond, interval, performance),
            Is.EqualTo(expected));
    }

    [TestCase(10f, 4f, 9.165151f)]
    [TestCase(10f, 10f, 0f)]
    [TestCase(3f, 5f, 0f)]
    [TestCase(-10f, 4f, 9.165151f)]
    public void RemainingImpactSpeedConservesSquaredSpeedBudget(
        float availableSpeed,
        float requiredSpeed,
        float expected)
    {
        var remaining = Content.Shared._CMU14.Destruction.ImpactEnergySolver.GetRemainingSpeed(
            availableSpeed,
            requiredSpeed);

        Assert.That(remaining, Is.EqualTo(expected).Within(0.0001f));
        Assert.That(remaining, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void PoweredDemolitionChassisProvidesAudibleFeedbackByDefault()
    {
        var chassis = new VehiclePlowChassisComponent();

        Assert.Multiple(() =>
        {
            Assert.That(chassis.PoweredDemolitionSound, Is.Not.Null);
            Assert.That(chassis.PoweredDemolitionSoundCooldown, Is.GreaterThan(0f));
        });
    }

    [Test]
    public void WallCollisionCooldownsAreScopedPerVehicleAndTarget()
    {
        var cooldowns = new VehicleCollisionCooldownTracker();
        var vehicle = new EntityUid(1);
        var otherVehicle = new EntityUid(2);
        var firstWall = new EntityUid(3);
        var secondWall = new EntityUid(4);
        var now = TimeSpan.FromSeconds(10);

        cooldowns.Start(vehicle, firstWall, now, TimeSpan.FromSeconds(0.5));

        Assert.Multiple(() =>
        {
            Assert.That(cooldowns.IsActive(vehicle, firstWall, now), Is.True);
            Assert.That(cooldowns.IsActive(vehicle, secondWall, now), Is.False);
            Assert.That(cooldowns.IsActive(otherVehicle, firstWall, now), Is.False);
            Assert.That(cooldowns.IsActive(vehicle, firstWall, now + TimeSpan.FromSeconds(0.5)), Is.False);
        });
    }
}
