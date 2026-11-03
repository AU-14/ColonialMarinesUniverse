using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared.CMU14.Dropship.DirectFire;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.Map;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class GunshipBangaloreAmmoTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task BangaloreBoxFiresThreeProjectilesForFiveShots()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var containers = Server.System<SharedContainerSystem>();
            var point = SEntMan.SpawnEntity(null, map.GridCoords);
            var weaponPoint = SEntMan.EnsureComponent<DropshipWeaponPointComponent>(point);
            SEntMan.EnsureComponent<GunshipDirectFirePointComponent>(point);

            var weaponContainer = containers.EnsureContainer<ContainerSlot>(point, weaponPoint.WeaponContainerSlotId);
            var ammoContainer = containers.EnsureContainer<ContainerSlot>(point, weaponPoint.AmmoContainerSlotId);
            var weapon = SEntMan.SpawnEntity("CMUDropshipAttachmentM4332FlareLauncher", map.GridCoords);
            var ammo = SEntMan.SpawnEntity("CMUDropshipAttachmentAmmoBangalore", map.GridCoords);
            Assert.That(containers.Insert(weapon, weaponContainer, force: true), Is.True);
            Assert.That(containers.Insert(ammo, ammoContainer, force: true), Is.True);

            var ammoComponent = SEntMan.GetComponent<DropshipAmmoComponent>(ammo);
            Assert.That(ammoComponent.Rounds, Is.EqualTo(15));
            for (var shot = 1; shot <= 5; shot++)
            {
                var takeAmmo = new TakeAmmoEvent(1, [], map.GridCoords, null);
                SEntMan.EventBus.RaiseLocalEvent(weapon, takeAmmo);

                Assert.Multiple(() =>
                {
                    Assert.That(takeAmmo.Ammo, Has.Count.EqualTo(3), $"Shot {shot} launches three Bangalore charges.");
                    Assert.That(ammoComponent.Rounds, Is.EqualTo(15 - shot * 3),
                        $"Shot {shot} consumes three rounds.");
                });
            }

            var afterEmpty = new TakeAmmoEvent(1, [], map.GridCoords, null);
            SEntMan.EventBus.RaiseLocalEvent(weapon, afterEmpty);
            Assert.Multiple(() =>
            {
                Assert.That(ammoComponent.Rounds, Is.Zero, "Five shots exhaust all fifteen charges.");
                Assert.That(afterEmpty.Ammo, Is.Empty, "No sixth shot is available.");
            });
        });
    }
}
