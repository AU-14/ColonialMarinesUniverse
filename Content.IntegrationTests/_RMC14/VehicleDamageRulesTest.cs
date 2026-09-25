using Content.Shared._RMC14.Vehicle;
using Content.Shared.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
public sealed class VehicleDamageRulesTest
{
    [TestCase(90f, 10f, 0f)]
    [TestCase(41f, 20f, 0f)]
    [TestCase(40f, 0.1f, 0f)]
    [TestCase(10f, 7.9f, 0f)]
    [TestCase(40f, 8f, 0.05f)]
    [TestCase(10f, 50f, 0.05f)]
    [TestCase(0f, 50f, 0f)]
    public void OnlySubstantialHitsToSeriouslyDamagedPartsCanCauseFaults(float remaining, float damage, float chance)
    {
        var integrity = new HardpointIntegrityComponent { MaxIntegrity = 100f, Integrity = remaining };
        Assert.That(VehicleFailureRules.GetChance(integrity, damage), Is.EqualTo(chance));
    }

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
