using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleDamageRulesTest
{
    // CMU14 method: exercise configurable fault thresholds independently of balance defaults.

    [Test]
    public void ContinuousContactCannotChargeAgainUntilTheChassisMovesClear()
    {
        var contacts = new VehicleCollisionContactTracker();
        var vehicle = new EntityUid(1);
        var obstacle = new EntityUid(2);
        var secondObstacle = new EntityUid(3);
        var chassis = new Box2(-1, -1, 1, 1);
        var wall = new Box2(1.05f, -1, 2, 1);

        Assert.That(contacts.TryStart(vehicle, obstacle, chassis, wall), Is.True);
        for (var i = 0; i < 600; i++)
        {
            contacts.Update(vehicle, chassis);
            Assert.That(contacts.TryStart(vehicle, obstacle, chassis, wall), Is.False);
        }

        Assert.That(contacts.TryStart(vehicle, secondObstacle, chassis, wall), Is.True);
        contacts.Update(vehicle, new Box2(-5, -1, -3, 1));
        Assert.That(contacts.TryStart(vehicle, obstacle, chassis, wall), Is.True,
            "a fresh approach after driving clear must count as a new impact");
        contacts.RemoveVehicle(vehicle);
        Assert.That(contacts.TryStart(vehicle, obstacle, chassis, wall), Is.True);
    }
}
