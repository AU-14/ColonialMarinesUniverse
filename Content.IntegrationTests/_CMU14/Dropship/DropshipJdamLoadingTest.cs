using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Weapon;
using Content.Shared._RMC14.PowerLoader;
using Content.Shared._RMC14.PowerLoader.Events;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Robust.Shared.Containers;

namespace Content.IntegrationTests._CMU14.Dropship;

#pragma warning disable RA0002 // Complete real loading do-afters after changing flight or occupancy.

[TestFixture]
public sealed class DropshipJdamLoadingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Dirty = true };

    [TestCase(false)]
    [TestCase(true)]
    public async Task PowerloaderLoadingRejectsFlightAndTakeoffDuringLoading(bool internalMount)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var ship = SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var containers = Server.System<SharedContainerSystem>();
            var point = SEntMan.SpawnEntity(internalMount ? "AU14InteriorWeaponAttachmentPoint" : "CMAlamoWall61", map.GridCoords);
            ship.AttachmentPoints.Add(point);
            var weaponSlotId = internalMount
                ? SEntMan.GetComponent<DropshipUtilityPointComponent>(point).UtilitySlotId
                : SEntMan.GetComponent<DropshipWeaponPointComponent>(point).WeaponContainerSlotId;
            var ammoSlotId = internalMount
                ? SEntMan.GetComponent<DropshipUtilityPointComponent>(point).AmmoContainerSlotId
                : SEntMan.GetComponent<DropshipWeaponPointComponent>(point).AmmoContainerSlotId;
            var weapon = SEntMan.SpawnEntity(internalMount ? "CMUDropshipAttachmentJDAMBombClamp" : "RMCDropshipAttachmentRocketPod", map.GridCoords);
            Assert.That(containers.Insert(weapon, containers.EnsureContainer<ContainerSlot>(point, weaponSlotId)), Is.True);
            var ammoSlot = containers.EnsureContainer<ContainerSlot>(point, ammoSlotId);
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            var ammo = SEntMan.SpawnEntity(internalMount ? "CMUDropshipAmmoJDAMHE" : "RMCDropshipAttachmentAmmoRocketMiniMike", map.GridCoords);
            var attachable = SEntMan.GetComponent<PowerLoaderAttachableComponent>(ammo);
            attachable.AttachDelay = TimeSpan.FromSeconds(1);
            var ftl = SEntMan.EnsureComponent<FTLComponent>(map.Grid);
            ftl.State = ship.State = FTLState.Travelling;
            var request = new GetAttachmentSlotEvent(SEntMan.GetNetEntity(loader), SEntMan.GetNetEntity(ammo));
            SEntMan.EventBus.RaiseLocalEvent(point, request);
            Assert.That(request.CanUse, Is.False, "All mounts reject airborne loading.");

            ftl.State = ship.State = FTLState.Available;
            SEntMan.EventBus.RaiseLocalEvent(ammo, new BeforeRangedInteractEvent(loader, ammo, point, map.GridCoords, true));
            var after = SEntMan.GetComponent<DoAfterComponent>(loader).DoAfters.Values.Single(d => !d.Completed && !d.Cancelled);
            ftl.State = ship.State = FTLState.Starting;
            after.StartTime = SGameTiming.CurTime - after.Args.Delay - TimeSpan.FromSeconds(1);
            Server.System<SharedDoAfterSystem>().Update(0);
            Assert.That(after.Completed, Is.True);
            Assert.That(ammoSlot.ContainedEntity, Is.Null, "Takeoff during a reload must prevent insertion.");
            ftl.State = ship.State = FTLState.Available;
            attachable.AttachDelay = TimeSpan.Zero;
            SEntMan.EventBus.RaiseLocalEvent(ammo, new BeforeRangedInteractEvent(loader, ammo, point, map.GridCoords, true));
            Assert.That(ammoSlot.ContainedEntity, Is.EqualTo(ammo), "Landing permits the same payload to load.");
            var payload = SEntMan.GetComponent<DropshipAmmoComponent>(ammo);
            payload.Rounds = 0;
            var reserve = SEntMan.SpawnEntity(internalMount ? "CMUDropshipAmmoJDAMHE" : "RMCDropshipAttachmentAmmoRocketMiniMike", map.GridCoords);
            ftl.State = ship.State = FTLState.Travelling;
            var refill = new PowerLoaderInteractEvent(loader, ammo, reserve, []);
            SEntMan.EventBus.RaiseLocalEvent(reserve, ref refill);
            Assert.That(payload.Rounds, Is.Zero, "Refilling a mounted pack must not bypass the loading restriction.");
            ftl.State = ship.State = FTLState.Available;
            refill = new PowerLoaderInteractEvent(loader, ammo, reserve, []);
            SEntMan.EventBus.RaiseLocalEvent(reserve, ref refill);
            Assert.That(payload.Rounds, Is.EqualTo(payload.MaxRounds));
            SEntMan.RemoveComponent<FTLComponent>(map.Grid);
        });
    }

    [Test]
    public async Task InternalWeaponLimitAllowsTwoAndExemptsDedicatedPoints()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var ship = SEntMan.EnsureComponent<DropshipComponent>(map.Grid);
            var containers = Server.System<SharedContainerSystem>();
            var powerLoader = Server.System<PowerLoaderSystem>();
            var loader = SEntMan.SpawnEntity("RMCMechPowerLoader", map.GridCoords);
            var points = new EntityUid[4];
            var slots = new ContainerSlot[4];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = SEntMan.SpawnEntity("RMCUtilityAttachmentPoint", map.GridCoords);
                ship.AttachmentPoints.Add(points[i]);
                var point = SEntMan.GetComponent<DropshipUtilityPointComponent>(points[i]);
                slots[i] = containers.EnsureContainer<ContainerSlot>(points[i], point.UtilitySlotId);
            }

            var weapons = new EntityUid[2];
            for (var i = 0; i < weapons.Length; i++)
            {
                weapons[i] = SEntMan.SpawnEntity("CMUDropshipAttachmentJDAMBombClamp", map.GridCoords);
                Assert.That(containers.Insert(weapons[i], slots[i]), Is.True);
            }

            var candidate = SEntMan.SpawnEntity("CMUDropshipAttachmentJDAMBombClamp", map.GridCoords);
            Assert.That(powerLoader.CanAttachDropshipPayload(loader, points[3], candidate), Is.False,
                "Only two ordinary internal weapons may be installed.");

            var dedicated = SEntMan.SpawnEntity("AU14InteriorWeaponAttachmentPoint", map.GridCoords);
            ship.AttachmentPoints.Add(dedicated);
            var dedicatedPoint = SEntMan.GetComponent<DropshipUtilityPointComponent>(dedicated);
            var dedicatedSlot = containers.EnsureContainer<ContainerSlot>(dedicated, dedicatedPoint.UtilitySlotId);
            Assert.That(powerLoader.CanAttachDropshipPayload(loader, dedicated, candidate), Is.True,
                "Dedicated internal weapon points are exempt from the ship limit.");
            Assert.That(containers.Insert(candidate, dedicatedSlot), Is.True);

            Assert.That(containers.Remove(weapons[0], slots[0]), Is.True);
            var replacement = SEntMan.SpawnEntity("CMUDropshipAttachmentJDAMBombClamp", map.GridCoords);
            Assert.That(powerLoader.CanAttachDropshipPayload(loader, points[3], replacement), Is.True,
                "A weapon on a dedicated point does not consume the ordinary internal weapon allowance.");
            Assert.That(containers.Insert(replacement, slots[3]), Is.True);
            var fourth = SEntMan.SpawnEntity("CMUDropshipAttachmentJDAMBombClamp", map.GridCoords);
            Assert.That(powerLoader.CanAttachDropshipPayload(loader, points[0], fourth), Is.False,
                "The dedicated weapon must not increase the two-weapon limit for ordinary points.");
        });
    }

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
