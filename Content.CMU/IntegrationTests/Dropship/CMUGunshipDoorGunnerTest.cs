using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Emplacements;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Weapons.Ranged.Battery;
using Content.Shared.CMU14.Dropship.AttachmentPoint;
using Content.Shared.CMU14.Emplacements;
using Content.Shared.Buckle;
using Content.Shared.CombatMode;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipDoorGunnerTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task GAU19MCanShootMountedWithoutBatteryOrSmartgunEquipment()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var mount = SEntMan.SpawnEntity("CMUGAU19MDoorGunDropship", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            SEntMan.System<SkillsSystem>().SetSkill(user, "RMCSkillEngineer", 1);
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(mount, Direction.East.ToAngle());
            Assert.That(SEntMan.System<SharedBuckleSystem>().TryBuckle(user, user, mount), Is.True);

            var weapon = SEntMan.GetComponent<WeaponMountComponent>(mount).MountedEntity!.Value;
            Assert.That(SEntMan.HasComponent<GunDrainBatteryOnShootComponent>(weapon), Is.False);
            Assert.That(SEntMan.System<SharedWeaponMountSystem>().TryGetWeaponAmmo(mount, out var ammo, out _), Is.True);
            Assert.That(ammo, Is.EqualTo(1500));

            var shoot = new AttemptShootEvent(user, null,
                SEntMan.GetComponent<TransformComponent>(weapon).Coordinates,
                map.GridCoords.Offset(new Vector2(5, 0)));
            SEntMan.EventBus.RaiseLocalEvent(weapon, ref shoot);
            Assert.That(shoot.Cancelled, Is.False);
        });
    }

    [Test]
    public async Task SeatedOperatorRequiresCombatModeAndLosesControlOnDismount()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var gun = SEntMan.SpawnEntity("CMUM2CDoorGunDropship", map.GridCoords);
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var aim = SEntMan.System<CMUDoorGunAimSystem>();
            var buckle = SEntMan.System<SharedBuckleSystem>();
            var transforms = SEntMan.System<SharedTransformSystem>();
            var combat = SEntMan.System<SharedCombatModeSystem>();
            combat.SetInCombatMode(user, false);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
            Assert.That(buckle.TryBuckle(user, user, gun), Is.True);
            Assert.That(combat.IsInCombatMode(user), Is.False);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
            combat.SetInCombatMode(user, true);
            Assert.That(aim.TryGetMount(user, out var controlled, out _), Is.True);
            Assert.That(controlled, Is.EqualTo(gun));

            var position = transforms.GetWorldPosition(gun);
            var forward = transforms.GetWorldRotation(gun).ToWorldVec();
            Assert.That(aim.TryGetAimOffset(gun, new EntityCoordinates(map.MapUid, position + forward * 5), out _), Is.True);
            Assert.That(aim.TryGetAimOffset(gun, new EntityCoordinates(map.MapUid, position - forward * 5), out _), Is.False);
            combat.SetInCombatMode(user, false);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
            combat.SetInCombatMode(user, true);
            buckle.Unbuckle(user, user);
            Assert.That(aim.TryGetMount(user, out _, out _), Is.False);
        });
    }

    [Test]
    public async Task DoorPlacementOffsetsAndLocksAllDoorGunVariantsOnBothSides()
    {
        var map = await Pair.CreateTestMap();
        var guns = new List<NetEntity>();
        var deployedAttachments = new List<EntityUid>();
        string[] attachments =
        [
            "RMCDropshipAttachmentDoorGun",
            "CMUDropshipAttachmentDoorGunM2C",
            "CMUDropshipAttachmentDoorGunUTESC",
            "CMUDropshipAttachmentDoorGunGAU19M", // Exercise the fourth outward direction too.
        ];
        Vector2[] directions = [Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY];

        await Server.WaitAssertion(() =>
        {
            SEntMan.AddComponent<CMUBlockEmplacementDeploymentComponent>(map.Grid.Owner);
            var maps = SEntMan.System<SharedMapSystem>();
            for (var x = -2; x <= 15; x++)
            for (var y = -2; y <= 2; y++)
                maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);

            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords.Offset(new Vector2(6, 0)));
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            for (var i = 0; i < attachments.Length; i++)
            {
                var coords = map.GridCoords.Offset(new Vector2(i * 4 + 0.5f, 0.5f));
                var point = SEntMan.SpawnEntity("CMUGunshipHardpointAttachmentPoint", coords);
                Assert.That(SEntMan.GetComponent<TransformComponent>(point).GridUid, Is.EqualTo(map.Grid.Owner));
                Assert.That(SEntMan.GetComponent<GunshipHardpointAttachmentPointComponent>(point)
                    .AllowedAttachments.Contains(attachments[i]), Is.True);
                var slotId = SEntMan.GetComponent<DropshipUtilityPointComponent>(point).UtilitySlotId;
                SEntMan.System<SharedContainerSystem>().EnsureContainer<ContainerSlot>(point, slotId);
                var attachment = SEntMan.SpawnInContainerOrDrop(attachments[i], point, slotId);
                Assert.That(SEntMan.GetComponent<TransformComponent>(attachment).ParentUid, Is.EqualTo(point));
                var deployer = SEntMan.GetComponent<RMCEquipmentDeployerComponent>(attachment);
                Assert.That(deployer.DeployPrototype, Is.Not.Null);
                var gun = SEntMan.SpawnEntity(deployer.DeployPrototype!.Value.Id, coords);
                var mount = SEntMan.GetComponent<WeaponMountComponent>(gun);
                var mounts = SEntMan.System<SharedWeaponMountSystem>();

                SEntMan.EventBus.RaiseLocalEvent(attachment, new RMCEquipmentDeployedEvent(true, gun));
                Assert.That(mount.RotationLocked, Is.False, "A gun without a door keeps its normal rotation.");
                var before = SEntMan.GetComponent<TransformComponent>(gun).LocalRotation;
                mounts.RotateMount((gun, mount), null);
                Assert.That(SEntMan.GetComponent<TransformComponent>(gun).LocalRotation, Is.Not.EqualTo(before));

                var door = SEntMan.SpawnEntity("CMNormandyDoorSinglePort", coords.Offset(directions[i]));
                Assert.That(SEntMan.GetComponent<TransformComponent>(door).ParentUid, Is.EqualTo(map.Grid.Owner));
                var expected = SEntMan.GetComponent<TransformComponent>(door).LocalPosition + directions[i] * 0.9f;
                maps.SetTile(map.Grid, new Vector2i((int) MathF.Floor(expected.X), (int) MathF.Floor(expected.Y)), Tile.Empty);
                SEntMan.System<Content.Shared.Doors.Systems.SharedDoorSystem>()
                    .SetBoltsDown((door, SEntMan.GetComponent<DoorBoltComponent>(door)), false);
                SEntMan.EventBus.RaiseLocalEvent(attachment, new RMCEquipmentDeployedEvent(true, gun));
                var xform = SEntMan.GetComponent<TransformComponent>(gun);
                Assert.That(Vector2.Distance(xform.LocalPosition, expected), Is.LessThan(0.001f));
                Assert.That(xform.ParentUid, Is.EqualTo(map.Grid.Owner), "Outboard guns must stay attached beyond the hull tiles.");
                Assert.That(Math.Abs(Angle.ShortestDistance(xform.LocalRotation, directions[i].ToWorldAngle()).Degrees), Is.LessThan(0.01));
                Assert.That(mount.RotationLocked, Is.True);
                before = xform.LocalRotation;
                mounts.RotateMount((gun, mount), null);
                Assert.That(xform.LocalRotation, Is.EqualTo(before));
                guns.Add(SEntMan.GetNetEntity(gun));
                deployedAttachments.Add(attachment);
            }
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            foreach (var net in guns)
            {
                var gun = CEntMan.GetEntity(net);
                var mount = CEntMan.GetComponent<WeaponMountComponent>(gun);
                Assert.That(mount.RotationLocked, Is.True, "The client must also reject predicted rotation.");
                var xform = CEntMan.GetComponent<TransformComponent>(gun);
                Assert.That(xform.ParentUid, Is.EqualTo(map.CGridUid));
                var before = xform.LocalRotation;
                CEntMan.System<SharedWeaponMountSystem>().RotateMount((gun, mount), null);
                Assert.That(xform.LocalRotation, Is.EqualTo(before));
            }
        });

        await Server.WaitAssertion(() =>
        {
            for (var i = 0; i < guns.Count; i++)
            {
                var gun = SEntMan.GetEntity(guns[i]);
                SEntMan.EventBus.RaiseLocalEvent(deployedAttachments[i], new RMCEquipmentDeployedEvent(false, gun));
                Assert.That(SEntMan.GetComponent<WeaponMountComponent>(gun).RotationLocked, Is.False);
            }
        });
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() =>
        {
            foreach (var net in guns)
                Assert.That(CEntMan.GetComponent<WeaponMountComponent>(CEntMan.GetEntity(net)).RotationLocked, Is.False);
        });
    }

    [Test]
    public async Task HeldDoorRejectsClosingOnServerAndClientUntilReleased()
    {
        var map = await Pair.CreateTestMap();
        EntityUid door = default;
        NetEntity doorNet = default;
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, user);
            door = SEntMan.SpawnEntity(null, map.GridCoords);
            var held = SEntMan.AddComponent<GunshipDoorGunnerHeldOpenComponent>(door);
            held.HeldOpen = true;
            SEntMan.Dirty(door, held);
            doorNet = SEntMan.GetNetEntity(door);
            AssertClosingCancelled(SEntMan, door, true);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClosingCancelled(CEntMan, CEntMan.GetEntity(doorNet), true));

        await Server.WaitAssertion(() =>
        {
            var held = SEntMan.GetComponent<GunshipDoorGunnerHeldOpenComponent>(door);
            held.HeldOpen = false;
            SEntMan.Dirty(door, held);
            AssertClosingCancelled(SEntMan, door, false);
        });

        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => AssertClosingCancelled(CEntMan, CEntMan.GetEntity(doorNet), false));
    }

    private static void AssertClosingCancelled(IEntityManager entities, EntityUid door, bool expected)
    {
        var closing = new BeforeDoorClosedEvent(false);
        entities.EventBus.RaiseLocalEvent(door, closing);
        Assert.That(closing.Cancelled, Is.EqualTo(expected));
        var autoClose = new BeforeDoorAutoCloseEvent();
        entities.EventBus.RaiseLocalEvent(door, autoClose);
        Assert.That(autoClose.Cancelled, Is.EqualTo(expected));
    }
}
