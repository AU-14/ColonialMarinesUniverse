using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Vehicle;
using Content.Shared.Vehicle.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// Taking the wheel starts the jeep's engine, which keeps running when the driver gets out until it is
/// switched off; switched off, the jeep won't drive.
/// </summary>
[TestFixture]
public sealed class CMUJeepEngineSwitchTest : GameTest
{
    [Test]
    public async Task EngineRunsOnUntilSwitchedOff()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid driver = default;
        await Server.WaitPost(() =>
        {
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -4; x <= 4; x++)
            {
                for (var y = -4; y <= 4; y++)
                {
                    maps.SetTile(map.Grid.Owner, map.Grid.Comp, new Vector2i(x, y), map.Tile.Tile);
                }
            }

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepCargo", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            driver = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 1.45f, 0.75f));
        });
        await RunTicksSync(5);

        EntityUid seat = default;
        await Server.WaitPost(() =>
        {
            seat = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities
                .First(s => SEntMan.GetComponent<CMUVehicleSeatComponent>(s).Driver);
            SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seat, driver);
        });
        await RunSeconds(4f);

        await Server.WaitPost(() =>
        {
            var engine = SEntMan.GetComponent<CMUVehicleEngineSoundComponent>(jeep);
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).BuckledTo, Is.EqualTo(seat));
            Assert.That(engine.Switched && engine.On, Is.True, "Taking the wheel starts the engine.");
            SEntMan.System<SharedBuckleSystem>().Unbuckle(driver, driver);
        });
        await RunSeconds(3f);

        await Server.WaitPost(() =>
        {
            var engine = SEntMan.GetComponent<CMUVehicleEngineSoundComponent>(jeep);
            Assert.That(SEntMan.GetComponent<VehicleComponent>(jeep).Operator, Is.Null, "The driver got out.");
            Assert.That(engine.On && engine.EngineRunning, Is.True, "The engine runs on without a driver.");
            SEntMan.System<CMUVehicleEngineSwitchSystem>().SetSwitched((jeep, engine), false);
        });
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var engine = SEntMan.GetComponent<CMUVehicleEngineSoundComponent>(jeep);
            Assert.That(engine.On || engine.EngineRunning, Is.False, "Switched off, it stops.");
            var ev = new VehicleCanRunEvent((jeep, SEntMan.GetComponent<VehicleComponent>(jeep)));
            SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
            Assert.That(ev.CanRun, Is.False, "Switched off, it won't drive.");
        });
    }
}
