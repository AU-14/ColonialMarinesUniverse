using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Buckle.Components;
using Content.Shared.CMU14.Vehicle.Open;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Vehicle;

/// <summary>
/// From a jeep's seats the driver fires only a sidearm, a passenger anything, and the gun jeep's
/// gunner no hand gun at all. Only passengers can wield.
/// </summary>
[TestFixture]
public sealed class CMUJeepSeatGunsTest : GameTest
{
    [Test]
    public async Task EachSeatFiresItsOwnGuns()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jeep = default;
        EntityUid wielded = default;
        var riders = new EntityUid[3];
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

            jeep = SEntMan.SpawnEntity("CMUVehicleJeepGunner", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            for (var i = 0; i < riders.Length; i++)
            {
                riders[i] = SEntMan.SpawnEntity("CMMobHuman", new EntityCoordinates(map.Grid, 0.5f, 0.5f));
            }
        });
        await RunTicksSync(5);

        // The driver walks up holding a rifle in both hands.
        await Server.WaitPost(() =>
        {
            wielded = Hold(riders[0], "RMCWeaponRifleL24");
            Assert.That(SEntMan.System<SharedWieldableSystem>().TryWield(wielded, riders[0]), Is.True);
        });

        // The gun jeep's seats in order: driver, passenger, gunner.
        await Server.WaitPost(() =>
        {
            var seats = SEntMan.GetComponent<CMUVehicleSeatsComponent>(jeep).SeatEntities;
            for (var i = 0; i < riders.Length; i++)
            {
                SEntMan.System<CMUVehicleSeatSystem>().ClickSeat(seats[i], riders[i]);
            }
        });
        await RunSeconds(4f);

        var fires = new bool[3, 2];
        var wields = new bool[3];
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.GetComponent<WieldableComponent>(wielded).Wielded, Is.False, "Taking the wheel lets go with one hand.");
            for (var i = 0; i < riders.Length; i++)
            {
                Assert.That(SEntMan.GetComponent<BuckleComponent>(riders[i]).Buckled, Is.True);
                fires[i, 0] = Fires(riders[i], "RMCWeaponRifleL24");
                fires[i, 1] = Fires(riders[i], "RMCWeaponPistolB92FS");
                var rifle = Hold(riders[i], "RMCWeaponRifleL24");
                wields[i] = SEntMan.System<SharedWieldableSystem>().TryWield(rifle, riders[i]);
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(fires[0, 0], Is.False, "The driver can't fire a rifle.");
            Assert.That(fires[0, 1], Is.True, "The driver can fire a pistol.");
            Assert.That(fires[1, 0], Is.True, "A passenger can fire a rifle.");
            Assert.That(fires[1, 1], Is.True, "And a pistol.");
            Assert.That(fires[2, 0] || fires[2, 1], Is.False, "The gunner fires only the mounted gun.");
            Assert.That(wields[0], Is.False, "The driver keeps a hand on the wheel.");
            Assert.That(wields[1], Is.True, "A passenger can wield.");
            Assert.That(wields[2], Is.False, "The gunner's hands are on the mounted gun.");
        });
    }

    /// <summary>
    /// Puts this gun alone in the rider's hands.
    /// </summary>
    private EntityUid Hold(EntityUid rider, string prototype)
    {
        var hands = SEntMan.System<SharedHandsSystem>();
        foreach (var held in hands.EnumerateHeld(rider).ToList())
        {
            SEntMan.DeleteEntity(held);
        }

        var gun = SEntMan.SpawnEntity(prototype, SEntMan.GetComponent<TransformComponent>(rider).Coordinates);
        Assert.That(hands.TryPickupAnyHand(rider, gun), Is.True);
        return gun;
    }

    /// <summary>
    /// Whether the rider, holding only this gun, may fire it.
    /// </summary>
    private bool Fires(EntityUid rider, string prototype)
    {
        var gun = Hold(rider, prototype);
        var ev = new ShotAttemptedEvent { User = rider, Used = (gun, SEntMan.GetComponent<GunComponent>(gun)) };
        SEntMan.EventBus.RaiseLocalEvent(gun, ref ev);
        return !ev.Cancelled;
    }
}
