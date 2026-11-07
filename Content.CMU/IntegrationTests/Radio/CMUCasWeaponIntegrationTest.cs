using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.Cas;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMUCasWeaponIntegrationTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task AutoQueriesHonorBootFaultsFlightModeAndAvailableAmmo()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            _ = Server.System<CMUCasWeaponIntegrationSystem>();
            var ship = map.Grid.Owner;
            SEntMan.EnsureComponent<DropshipComponent>(ship);
            var flight = SEntMan.AddComponent<FTLComponent>(ship);
            var point = SEntMan.SpawnEntity(null, map.GridCoords);
            var mount = SEntMan.AddComponent<DropshipWeaponPointComponent>(point);
            var weapon = SEntMan.SpawnEntity("RMCDropshipAttachmentGau21Cannon", map.GridCoords);
            var containers = Server.System<SharedContainerSystem>();
            Assert.That(containers.Insert(weapon,
                containers.EnsureContainer<ContainerSlot>(point, mount.WeaponContainerSlotId)), Is.True);
            var ammoSlot = containers.EnsureContainer<ContainerSlot>(point, mount.AmmoContainerSlotId);
            var noAmmo = Query();
            Assert.That(noAmmo.Ready, Is.True);
            Assert.That(noAmmo.AvailableBursts, Is.Zero);
            Assert.That(noAmmo.InFlightMode, Is.False);
            var boot = SEntMan.AddComponent<DropshipFireControlBootComponent>(ship);
            Assert.That(Query().Ready, Is.False);
            boot.Complete = true;
            Assert.That(Query().Ready, Is.True);
            var ammo = SEntMan.SpawnEntity("RMCDropshipAttachmentAmmoGAU", map.GridCoords);
            Assert.That(containers.Insert(ammo, ammoSlot), Is.True);
#pragma warning disable RA0002 // Exercise optional integration query policy with controlled flight/ammunition state.
            flight.State = FTLState.Travelling;
            var payload = SEntMan.GetComponent<DropshipAmmoComponent>(ammo);
            payload.Rounds = payload.RoundsPerShot * 3;
#pragma warning restore RA0002
            var loaded = Query();
            Assert.That(loaded.AvailableBursts, Is.EqualTo(3));
            Assert.That(loaded.InFlightMode, Is.True);
            var fault = SEntMan.AddComponent<DropshipHardpointFaultComponent>(point);
            fault.Faulted = true;
            Assert.That(Query().Ready, Is.False);
            fault.Faulted = false;
#pragma warning disable RA0002
            payload.Rounds = 0;
#pragma warning restore RA0002
            Assert.That(Query().AvailableBursts, Is.Zero);

            CMUCasAutoWeaponQueryEvent Query()
            {
                var query = new CMUCasAutoWeaponQueryEvent(ship, weapon);
                SEntMan.EventBus.RaiseEvent(EventSource.Local, ref query);
                return query;
            }
        });
    }
}
