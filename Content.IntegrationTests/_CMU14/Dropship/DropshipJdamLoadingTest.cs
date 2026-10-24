using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Robust.Shared.Containers;

namespace Content.IntegrationTests._CMU14.Dropship;

[TestFixture]
public sealed class DropshipJdamLoadingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase("CMUDropshipAmmoJDAMHE")]
    [TestCase("CMUDropshipAmmoJDAMIncendiary")]
    [TestCase("CMUDropshipAmmoJDAMAP")]
    [TestCase("CMUDropshipAmmoJDAMCluster")]
    [TestCase("CMUDropshipAmmoJDAMNapalm")]
    public async Task PowerloaderLoadsAndUnloadsInternalBombClamp(string prototype)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var containers = SEntMan.System<SharedContainerSystem>();
            var point = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", map.GridCoords);
            var mount = SEntMan.GetComponent<DropshipUtilityPointComponent>(point);
            var clamp = SEntMan.SpawnEntity("CMUDropshipAttachmentJDAMBombClamp", map.GridCoords);
            var weaponSlot = containers.EnsureContainer<ContainerSlot>(point, mount.UtilitySlotId);
            Assert.That(containers.Insert(clamp, weaponSlot), Is.True);
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            var bomb = SEntMan.SpawnEntity(prototype, map.GridCoords);
            SEntMan.GetComponent<PowerLoaderAttachableComponent>(bomb).AttachDelay = TimeSpan.Zero;

            var interact = new BeforeRangedInteractEvent(loader, bomb, point, map.GridCoords, true);
            SEntMan.EventBus.RaiseLocalEvent(bomb, interact);
            Assert.That(interact.Handled, Is.True, "The actual loading interaction must accept internal points.");
            var ammoSlot = containers.GetContainer(point, mount.AmmoContainerSlotId);
            Assert.That(ammoSlot.Contains(bomb), Is.True, "The loading interaction must insert the bomb.");
            Assert.That(weaponSlot.Contains(clamp), Is.True);

            var secondBomb = SEntMan.SpawnEntity(prototype, map.GridCoords);
            var request = new GetAttachmentSlotEvent(SEntMan.GetNetEntity(loader), SEntMan.GetNetEntity(secondBomb));
            SEntMan.EventBus.RaiseLocalEvent(point, request);
            Assert.That(request.CanUse, Is.False, "The clamp holds only one physical bomb.");

            request = new GetAttachmentSlotEvent(SEntMan.GetNetEntity(loader), SEntMan.GetNetEntity(bomb))
            {
                BeingAttached = false,
            };
            SEntMan.EventBus.RaiseLocalEvent(point, request);
            Assert.That(request.CanUse, Is.True);
            Assert.That(request.SlotId, Is.EqualTo(mount.AmmoContainerSlotId));
            var detach = new DropshipDetachDoAfterEvent(
                SEntMan.GetNetEntity(point), SEntMan.GetNetEntity(bomb), request.SlotId);
            Assert.That(SEntMan.System<SharedDoAfterSystem>().TryStartDoAfter(
                new DoAfterArgs(SEntMan, loader, TimeSpan.Zero, detach, point, point)), Is.True);
            Assert.That(ammoSlot.Contains(bomb), Is.False);
            Assert.That(weaponSlot.Contains(clamp), Is.True);
        });
    }
}
