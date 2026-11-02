using System.Numerics;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Visor;
using Content.Shared.Buckle;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CMU14.Dropship.Integrity;
using Content.Shared.CMU14.Dropship.TacticalLand;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipCrewHudTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
- type: entity
  parent: ArmorHelmetM10
  id: CMUTestCrewHudHelmet
  components:
  - type: IntegratedVisors
    visorsToAdd: []
""";

    [Test]
    public async Task CrewVisorTracksOnlyInstalledGunsOnItsShipAndClearsItsLink()
    {
        var map = await Pair.CreateTestMap();
        var foreignMap = await Pair.CreateTestMap();
        EntityUid user = default, helmet = default, gun = default, otherGun = default;
        NetEntity userNet = default;
        await Server.WaitAssertion(() =>
        {
            var transforms = SEntMan.System<SharedTransformSystem>();
            SEntMan.EnsureComponent<DropshipComponent>(map.Grid.Owner);
            SEntMan.EnsureComponent<DropshipComponent>(foreignMap.Grid.Owner);
            var integrity = SEntMan.EnsureComponent<DropshipIntegrityComponent>(map.Grid.Owner);
            integrity.Integrity = 1200;
            integrity.ActiveMalfunctions.Add(DropshipMalfunction.SensorArrayFault);
            integrity.MasterAlarmSilenced = true;
            // Rotating the whole ship must not change its gun-side labels.
            transforms.SetLocalRotation(map.Grid.Owner, Angle.FromDegrees(90));

            gun = InstallGun("CMUGAU19MDoorGunDropship", map.GridCoords, Direction.East);
            otherGun = InstallGun("CMUM2CDoorGunDropship", map.GridCoords.Offset(new Vector2(4, 0)), Direction.West);
            InstallGun("RMCML66DNestDropship", map.GridCoords.Offset(new Vector2(8, 0)), Direction.South);
            InstallGun("CMUM2CDoorGunDropship", foreignMap.GridCoords, Direction.North);
            // A loose door-gun entity on the same grid is not an installed hardpoint.
            SEntMan.SpawnEntity("CMUUTESCDoorGunDropship", map.GridCoords.Offset(new Vector2(12, 0)));

            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            helmet = SEntMan.SpawnEntity("CMUTestCrewHudHelmet", map.GridCoords);
            Assert.That(SEntMan.System<InventorySystem>().TryEquip(user, helmet, "head", force: true), Is.True);
            SEntMan.System<SharedContainerSystem>().EnsureContainer<ContainerSlot>(helmet, "rmc_visor_one");
            SEntMan.SpawnInContainerOrDrop("CMUVisorGunshipCrew", helmet, "rmc_visor_one");
            userNet = SEntMan.GetNetEntity(user);
        });

        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<CMUGunshipCrewHudComponent>(user), Is.False);
            CycleVisor();
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<CMUGunshipCrewHudComponent>(user).Dropship, Is.Null);
            Assert.That(SEntMan.HasComponent<GunshipPilotHudComponent>(user), Is.False);
            // Crew linking does not require combat mode or a pilot seat.
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, gun), Is.True);
        });
        await Pair.RunTicksSync(20);
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            var hud = CEntMan.GetComponent<CMUGunshipCrewHudComponent>(CEntMan.GetEntity(userNet));
            Assert.That(hud.Ammo, Is.EqualTo(1500));
            Assert.That(hud.OtherGuns, Has.Count.EqualTo(2));
        });
        await Server.WaitAssertion(() =>
        {
            var hud = SEntMan.GetComponent<CMUGunshipCrewHudComponent>(user);
            Assert.That(hud.Dropship, Is.EqualTo(map.Grid.Owner));
            Assert.That(hud.Ammo, Is.EqualTo(1500));
            Assert.That(hud.Capacity, Is.EqualTo(1500));
            Assert.That(hud.OtherGuns, Has.Count.EqualTo(2));
            Assert.That(hud.OtherGuns.Select(g => g.Side), Is.EquivalentTo(new[] { Direction.West, Direction.South }));
            Assert.That(hud.OtherGuns.Single(g => g.Side == Direction.West).Ammo, Is.EqualTo(125));
            var integrity = SEntMan.GetComponent<DropshipIntegrityComponent>(map.Grid.Owner);
            Assert.That(hud.Integrity, Is.EqualTo(integrity.Integrity));
            Assert.That(hud.MaxIntegrity, Is.EqualTo(integrity.MaxIntegrity));
            Assert.That(hud.Malfunctions, Is.EqualTo(integrity.ActiveMalfunctions));
            Assert.That(hud.MasterAlarmSilenced, Is.EqualTo(integrity.MasterAlarmSilenced));

            Drain(gun, 5);
            Drain(otherGun, 25);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var hud = SEntMan.GetComponent<CMUGunshipCrewHudComponent>(user);
            Assert.That(hud.Ammo, Is.EqualTo(1495));
            Assert.That(hud.OtherGuns.Single(g => g.Side == Direction.West).Ammo, Is.EqualTo(100));
            var weapon = SEntMan.GetComponent<WeaponMountComponent>(otherGun).MountedEntity!.Value;
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryEject(weapon, "gun_magazine", null, out _), Is.True);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var empty = SEntMan.GetComponent<CMUGunshipCrewHudComponent>(user).OtherGuns.Single(g => g.Side == Direction.West);
            Assert.That(empty.Ammo, Is.Zero);
            Assert.That(empty.Capacity, Is.Zero);
            var weapon = SEntMan.GetComponent<WeaponMountComponent>(otherGun).MountedEntity!.Value;
            var magazine = SEntMan.SpawnEntity("RMCMagazineM2C", map.GridCoords);
            Assert.That(SEntMan.System<ItemSlotsSystem>().TryInsert(weapon, "gun_magazine", magazine, null), Is.True);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var reloaded = SEntMan.GetComponent<CMUGunshipCrewHudComponent>(user).OtherGuns.Single(g => g.Side == Direction.West);
            Assert.That(reloaded.Ammo, Is.EqualTo(125));
            Assert.That(reloaded.Capacity, Is.EqualTo(125));
            SEntMan.System<SharedBuckleSystem>().Unbuckle(user, user);
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() =>
        {
            var hud = SEntMan.GetComponent<CMUGunshipCrewHudComponent>(user);
            Assert.That(hud.Dropship, Is.Null);
            Assert.That(hud.OtherGuns, Is.Empty);
            Assert.That(hud.Alarms, Is.Empty);
            Assert.That(hud.Ammo, Is.Zero);
            // Cycling past empty slots raises the crew HUD and removes its display.
            CycleVisor();
        });
        await Pair.RunTicksSync(20);
        await Server.WaitAssertion(() => Assert.That(SEntMan.HasComponent<CMUGunshipCrewHudComponent>(user), Is.False));

        EntityUid InstallGun(string prototype, EntityCoordinates coordinates, Direction side)
        {
            var point = SEntMan.SpawnEntity("CMUGunshipHardpointAttachmentPoint", coordinates);
            var installed = SEntMan.SpawnEntity(prototype, coordinates);
            SEntMan.EnsureComponent<ActiveGunshipDoorGunnerComponent>(installed).AttachmentPoint = point;
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(installed, side.ToAngle());
            return installed;
        }

        void CycleVisor() => SEntMan.EventBus.RaiseLocalEvent(helmet, new CycleVisorActionEvent { Performer = user });

        void Drain(EntityUid mount, int count)
        {
            var weapon = SEntMan.GetComponent<WeaponMountComponent>(mount).MountedEntity!.Value;
            var take = new TakeAmmoEvent(count, new(), map.GridCoords, user);
            SEntMan.EventBus.RaiseLocalEvent(weapon, take);
            Assert.That(take.Ammo, Has.Count.EqualTo(count));
            foreach (var ammo in take.Ammo)
                if (ammo.Entity is { } entity)
                    SEntMan.DeleteEntity(entity);
        }
    }
}
