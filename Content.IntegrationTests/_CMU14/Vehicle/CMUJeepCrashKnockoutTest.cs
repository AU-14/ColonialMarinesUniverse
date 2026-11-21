using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Vehicle.Open;
using Content.Shared._RMC14.Stun;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle;
using Content.Shared.CMU14.Vehicle.Open;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// A hard head on crash blacks the jeep's riders out and knocks them out, more surely the faster it
/// hit, and the horn blares while the driver is out on the wheel. The gun's close and distant shots and the horns load.
/// </summary>
[TestFixture]
public sealed class CMUJeepCrashKnockoutTest : GameTest
{
    private async Task<(EntityUid Jeep, EntityUid Driver)> SeatedJeep()
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
        await RunSeconds(3f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.GetComponent<BuckleComponent>(driver).BuckledTo, Is.EqualTo(seat), "The driver should be seated."));

        return (jeep, driver);
    }

    private void Crash(EntityUid jeep, bool frontal, float speed)
    {
        var ev = new CMUVehicleCrashedEvent(30f, frontal, speed);
        SEntMan.EventBus.RaiseLocalEvent(jeep, ref ev);
    }

    [Test]
    public async Task TopSpeedCrashKnocksTheDriverOutAndBlaresTheHorn()
    {
        var (jeep, driver) = await SeatedJeep();
        var knockout = SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep);

        await Server.WaitPost(() =>
        {
            Crash(jeep, false, 1f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "A side crash shouldn't knock anyone out.");
            Crash(jeep, true, 0.5f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "A slow crash shouldn't knock anyone out.");

            Crash(jeep, true, 1f);
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.True, "A top speed head on crash always knocks out.");
            Assert.That(SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep).Horn, Is.Not.Null, "The horn should blare.");
        });

        await RunSeconds((float) knockout.MinDuration.TotalSeconds - 1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.True, "Out for at least the shortest knockout.");
            Assert.That(SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep).Horn, Is.Not.Null);
        });

        await RunSeconds((float) (knockout.MaxDuration - knockout.MinDuration).TotalSeconds + 2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<RMCUnconsciousComponent>(driver), Is.False, "Back up by the longest knockout.");
            Assert.That(SEntMan.GetComponent<CMUVehicleCrashKnockoutComponent>(jeep).Horn, Is.Null, "The horn stops when the driver comes to.");
        });
    }

}
