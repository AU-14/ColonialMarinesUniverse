using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Vehicle;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.CMU14.Vehicle;

[TestFixture]
public sealed class CMUVehicleInteriorPhysicsTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task LoadedTankCabinResistsMotionWithoutFreezingTheExterior()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tank = default;
        EntityUid cabin = default;
        Vector2 position = default;
        Angle rotation = default;
        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            var exterior = SEntMan.GetComponent<PhysicsComponent>(tank).BodyType;
            Assert.That(Server.System<VehicleSystem>().TryGetInteriorEntryCoordinates(tank, 0, out _), Is.True);
            cabin = SEntMan.GetComponent<VehicleInteriorComponent>(tank).Grid;
            Assert.That(SEntMan.GetComponent<PhysicsComponent>(tank).BodyType, Is.EqualTo(exterior));

            var transform = Server.System<SharedTransformSystem>();
            position = transform.GetWorldPosition(cabin);
            rotation = transform.GetWorldRotation(cabin);
            var physics = Server.System<SharedPhysicsSystem>();
            physics.ApplyAngularImpulse(cabin, 100f);
            physics.ApplyLinearImpulse(cabin, new Vector2(100f, 0));
            transform.SetWorldRotation(tank, Angle.FromDegrees(90));
        });
        await RunTicksSync(30);
        await Server.WaitAssertion(() =>
        {
            var transform = Server.System<SharedTransformSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(transform.GetWorldPosition(cabin), Is.EqualTo(position));
                Assert.That(transform.GetWorldRotation(cabin), Is.EqualTo(rotation));
                Assert.That(transform.GetWorldRotation(tank), Is.EqualTo(Angle.FromDegrees(90)));
            });
            SEntMan.DeleteEntity(tank);
        });
    }
}
