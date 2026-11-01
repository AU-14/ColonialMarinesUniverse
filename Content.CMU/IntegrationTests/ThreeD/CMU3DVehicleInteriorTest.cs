using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.ThreeD;
using Content.Shared._RMC14.Vehicle;
using Content.Shared.CMU14.ThreeD;

namespace Content.IntegrationTests.CMU14.ThreeD;

[TestFixture]
[TestOf(typeof(CMU3DVehicleInteriorSystem))]
public sealed class CMU3DVehicleInteriorTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    [Test]
    public async Task LoadedCabinFollowsVehicleAcrossMapsIncludingParentGridTransfer()
    {
        var start = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var maps = Server.System<SharedMapSystem>();
            var transform = Server.System<SharedTransformSystem>();
            var vehicles = Server.System<VehicleSystem>();
            var redux = maps.CreateMap(runMapInit: true);
            SEntMan.AddComponent<CMU3DMapComponent>(redux);
            var vehicle = SEntMan.SpawnEntity("VehicleHumvee", start.GridCoords);
            try
            {
                Assert.That(vehicles.TryGetInteriorEntryCoordinates(vehicle, 0, out _), Is.True);
                Assert.That(vehicles.TryGetInteriorMapId(vehicle, out var id), Is.True);
                var cabin = maps.GetMap(id);
                Assert.That(SEntMan.HasComponent<CMU3DMapComponent>(cabin), Is.False);

                // Moving the parent grid must update eligibility even though the vehicle's parent is unchanged.
                var grid = start.GridCoords.EntityId;
                var original = SComp<TransformComponent>(grid).Coordinates;
                transform.SetCoordinates(grid, new EntityCoordinates(redux, Vector2.Zero));
                Assert.That(SEntMan.HasComponent<CMU3DMapComponent>(cabin), Is.True);
                transform.SetCoordinates(grid, original);
                Assert.That(SEntMan.HasComponent<CMU3DMapComponent>(cabin), Is.False);

                transform.SetCoordinates(vehicle, new EntityCoordinates(redux, Vector2.Zero));
                Assert.That(SEntMan.HasComponent<CMU3DMapComponent>(cabin), Is.True);
                Assert.That(vehicles.TryGetInteriorMapId(vehicle, out var reused), Is.True);
                Assert.That(reused, Is.EqualTo(id));
                SEntMan.DeleteEntity(vehicle);
                Assert.That(SEntMan.Deleted(cabin), Is.True, "The cabin remains owned by the vehicle lifecycle.");
            }
            finally
            {
                SEntMan.DeleteEntity(vehicle);
                SEntMan.DeleteEntity(redux);
            }
        });
    }
}
