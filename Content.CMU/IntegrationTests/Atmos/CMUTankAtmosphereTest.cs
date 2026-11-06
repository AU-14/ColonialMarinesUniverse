using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.Atmos.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Atmos;

[TestFixture]
public sealed class CMUTankAtmosphereTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ClosedTankCabinKeepsItsAirWhenTheExteriorMoves()
    {
        var map = await Pair.CreateTestMap();
        EntityUid tank = default;
        EntityUid passenger = default;
        float initialPressure = default;
        await Server.WaitAssertion(() =>
        {
            tank = SEntMan.SpawnEntity("VehicleTank", map.GridCoords);
            Assert.That(Server.System<VehicleSystem>().TryGetInteriorEntryCoordinates(tank, 0, out var entry), Is.True);
            passenger = SEntMan.SpawnEntity("CMMobHuman", entry);
            initialPressure = Server.System<AtmosphereSystem>().GetContainingMixture(passenger)!.Pressure;
            Assert.That(initialPressure, Is.GreaterThan(50f), "the loaded tank cabin must start with breathable pressure");
            var transform = Server.System<SharedTransformSystem>();
            transform.SetCoordinates(tank, map.GridCoords.Offset(new Vector2(8, 0)));
            transform.SetWorldRotation(tank, Angle.FromDegrees(90));
        });

        await RunTicksSync(600);
        await Server.WaitAssertion(() =>
        {
            var cabinAir = Server.System<AtmosphereSystem>().GetContainingMixture(passenger)!;
            Assert.That(cabinAir.Pressure,
                Is.GreaterThan(initialPressure * 0.9f), "moving a closed tank must not vent its independent cabin");

            var interior = SEntMan.GetComponent<VehicleInteriorComponent>(tank);
            EntityUid? breachedWall = null;
            var walls = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
            while (walls.MoveNext(out var wall, out var metadata, out var transform))
            {
                if (transform.MapUid != interior.Map || metadata.EntityPrototype?.ID != "VehicleTankWall")
                    continue;

                breachedWall = wall;
                break;
            }

            Assert.That(breachedWall, Is.Not.Null, "the loaded cabin must have a real hull wall to breach");
            SEntMan.DeleteEntity(breachedWall!.Value);
        });

        await RunTicksSync(600);
        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<AtmosphereSystem>().GetContainingMixture(passenger)!.Pressure, Is.LessThan(1f),
                "a real hull breach must vent the finite cabin air supply");
            Assert.That(Server.System<VehicleSystem>().TryGetInteriorEntryCoordinates(tank, 0, out var reentry), Is.True);
            Server.System<SharedTransformSystem>().SetCoordinates(passenger, reentry);
            Assert.That(Server.System<AtmosphereSystem>().GetContainingMixture(passenger)!.Pressure, Is.LessThan(1f),
                "reentering an already loaded cabin must not replace air that was vented");

            var interior = SEntMan.GetComponent<VehicleInteriorComponent>(tank);
            var template = SEntMan.GetComponent<MapAtmosphereComponent>(interior.Grid);
            Assert.That(template.Mixture.Pressure, Is.GreaterThan(50f),
                "mutating a cabin tile must not mutate its authored atmosphere template");
            SEntMan.DeleteEntity(tank);
        });
    }
}
