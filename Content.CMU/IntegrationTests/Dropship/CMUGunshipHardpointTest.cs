using System.Diagnostics.CodeAnalysis;
using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.AttachmentPoint;
using Content.Shared._RMC14.Dropship.Utility.Components;
using Content.Shared._RMC14.Dropship.Utility.Systems;
using Content.Shared._RMC14.Emplacements;
using Content.Shared.CMU14.Weapons.ATGM;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Foldable;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Dropship;

[TestFixture]
public sealed class CMUGunshipHardpointTest : GameTest
{
    private const string MagazineSlot = "gun_magazine";
    private static readonly EntProtoId RackId = "CMUDropshipAttachmentAmmoRack";
    private static readonly EntProtoId HardpointPointId = "CMUGunshipHardpointAttachmentPoint";
    private static readonly EntProtoId UtilityPointId = "CMUGunshipUtilityAttachmentPoint";
    private static readonly EntProtoId M2CHardpointId = "CMUDropshipAttachmentDoorGunM2C";
    private static readonly EntProtoId M2CAmmoId = "CMUMagazineM2CGunship";

    public override PoolSettings PoolSettings => PsDisconnected;

    [Test]
    public async Task HardpointAmmoOnlyFitsHardpointWeapons()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            bool FitsMagazineSlot(EntProtoId weaponId, EntProtoId ammoId)
            {
                var weapon = SEntMan.SpawnAttachedTo(weaponId, map.GridCoords);
                var ammo = SEntMan.SpawnAttachedTo(ammoId, map.GridCoords);
                Assert.That(TryGetMagazineSlot(weapon, out var slot), Is.True, $"{weaponId} has no magazine slot.");
                return Server.System<ItemSlotsSystem>().CanInsert(weapon, slot!, ammo, null, swap: true);
            }

            bool FitsLauncher(EntProtoId mountId, EntProtoId missileId)
            {
                var mount = SEntMan.SpawnAttachedTo(mountId, map.GridCoords);
                Assert.That(Server.System<CMUATGMSystem>().TryGetLauncher(mount, out var launcher), Is.True);
                var missile = SEntMan.SpawnAttachedTo(missileId, map.GridCoords);
                var ballistic = SEntMan.GetComponent<BallisticAmmoProviderComponent>(launcher);
                return Server.System<SharedGunSystem>().CanInsertBallistic((launcher, ballistic), missile);
            }

            Assert.Multiple(() =>
            {
                Assert.That(FitsMagazineSlot("RMCSmartGunMounted", "CMUMagazineML66DGunship"), Is.False);
                Assert.That(FitsMagazineSlot("RMCSmartGunMountedStatic", "CMUMagazineML66DGunship"), Is.False);
                Assert.That(FitsMagazineSlot("CMUSmartGunMountedDropship", "CMUMagazineML66DGunship"), Is.True);
                Assert.That(FitsMagazineSlot("RMCMachineGunM2C", M2CAmmoId), Is.False);
                Assert.That(FitsMagazineSlot("CMUMachineGunM2CDropshipLoaded", M2CAmmoId), Is.True);
                Assert.That(FitsMagazineSlot("AU14AGLMK86", "CMUMagazineMk86FragGunship"), Is.False);
                Assert.That(FitsMagazineSlot("CMUAGLMK86DropshipLoaded", "CMUMagazineMk86FragGunship"), Is.True);
                Assert.That(FitsLauncher("CMUATGMMount", "CMUATGMMissileGunship"), Is.False);
                Assert.That(FitsLauncher("CMUATGMMountDropship", "CMUATGMMissileGunship"), Is.True);
            });
        });
    }

    [TestCase("RMCDropshipAttachmentDoorGun", "CMUMagazineML66DGunship")]
    [TestCase("CMUDropshipAttachmentDoorGunM2C", "CMUMagazineM2CGunship")]
    [TestCase("CMUDropshipAttachmentDoorGunMk86", "CMUMagazineMk86FragGunship")]
    [TestCase("CMUDropshipAttachmentDoorGunATGM", "CMUATGMMissileGunship")]
    public async Task InstalledRackReloadsDryHardpointWeapon(string hardpointId, string ammoId)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var guns = Server.System<SharedGunSystem>();
            var weapon = DeployHardpoint(hardpointId, map.GridCoords);
            EmptyWeapon(weapon);
            Assert.That(guns.GetAmmoCount(weapon), Is.Zero);

            var rack = InstallRack(map.GridCoords);
            var ammo = SEntMan.SpawnAttachedTo(ammoId, map.GridCoords);
            Assert.That(Server.System<SharedStorageSystem>().Insert(rack, ammo, out _, playSound: false), Is.True);

            Assert.That(Server.System<GunshipHardpointAutoloadSystem>().TryAutoload(weapon), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(guns.GetAmmoCount(weapon), Is.GreaterThan(0));
                Assert.That(SEntMan.GetComponent<StorageComponent>(rack).Container.ContainedEntities, Is.Empty);
                if (TryGetMagazineSlot(weapon, out var slot))
                    Assert.That(slot.Item, Is.EqualTo(ammo), "The spent magazine must make way for the racked one.");
            });
        });
    }

    [Test]
    public async Task RackOnlyFeedsWeaponsOnItsOwnGunship()
    {
        var otherShip = await Pair.CreateTestMap();
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var weapon = DeployHardpoint(M2CHardpointId, map.GridCoords);
            EmptyWeapon(weapon);
            var autoload = Server.System<GunshipHardpointAutoloadSystem>();

            var looseRack = SEntMan.SpawnAttachedTo(RackId, map.GridCoords);
            FillRack(looseRack, M2CAmmoId, 1, map.GridCoords);
            Assert.That(autoload.TryAutoload(weapon), Is.False, "An uninstalled rack must not feed hardpoints.");

            var otherRack = InstallRack(otherShip.GridCoords);
            FillRack(otherRack, M2CAmmoId, 1, otherShip.GridCoords);
            Assert.That(autoload.TryAutoload(weapon), Is.False, "A rack must only feed hardpoints on its own gunship.");

            FillRack(InstallRack(map.GridCoords), M2CAmmoId, 1, map.GridCoords);
            Assert.That(autoload.TryAutoload(weapon), Is.True);
        });
    }

    [Test]
    public async Task DryHardpointWeaponReloadsOnItsOwn()
    {
        var map = await Pair.CreateTestMap();
        var weapon = EntityUid.Invalid;
        var delay = TimeSpan.Zero;
        await Server.WaitAssertion(() =>
        {
            weapon = DeployHardpoint(M2CHardpointId, map.GridCoords);
            EmptyWeapon(weapon);
            FillRack(InstallRack(map.GridCoords), M2CAmmoId, 1, map.GridCoords);
            delay = SEntMan.GetComponent<GunshipHardpointAutoloadComponent>(weapon).ReloadDelay;
        });

        // The dry-weapon scan runs every second, so allow one scan on top of the reload delay.
        await RunSeconds((float) delay.TotalSeconds + 2f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(Server.System<SharedGunSystem>().GetAmmoCount(weapon), Is.GreaterThan(0),
                "A dry hardpoint weapon must reload from the rack without anyone touching it.");
        });
    }

    [Test]
    public async Task RackHoldsFifteenHardpointMagazines()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var storage = Server.System<SharedStorageSystem>();
            var rack = SEntMan.SpawnAttachedTo(RackId, map.GridCoords);
            EntProtoId[] ammo =
            [
                "CMUMagazineML66DGunship",
                M2CAmmoId,
                "CMUMagazineMk86FragGunship",
                "CMUATGMMissileGunship",
            ];

            for (var i = 0; i < 15; i++)
            {
                var magazine = SEntMan.SpawnAttachedTo(ammo[i % ammo.Length], map.GridCoords);
                Assert.That(storage.Insert(rack, magazine, out _, playSound: false), Is.True,
                    $"Hardpoint magazine {i + 1} of 15 did not fit.");
            }

            var groundAmmo = SEntMan.SpawnAttachedTo("RMCMagazineM2C", map.GridCoords);
            Assert.That(storage.CanInsert(rack, groundAmmo, out _), Is.False,
                "The rack only takes gunship hardpoint ammunition.");
        });
    }

    [Test]
    public async Task UsingHardpointAmmoOnInstalledPointLoadsRack()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var rack = InstallRack(map.GridCoords);
            var point = SEntMan.GetComponent<TransformComponent>(rack).ParentUid;
            var user = SEntMan.SpawnAttachedTo("CMMobHuman", map.GridCoords);
            var magazine = SEntMan.SpawnAttachedTo(M2CAmmoId, map.GridCoords);
            Assert.That(Server.System<SharedHandsSystem>().TryPickup(user, magazine), Is.True);

            var clicked = SEntMan.GetComponent<TransformComponent>(point).Coordinates;
            Assert.That(Server.System<SharedInteractionSystem>().InteractUsing(user, magazine, point, clicked), Is.True);
            Assert.That(SEntMan.GetComponent<StorageComponent>(rack).Container.Contains(magazine), Is.True,
                "Clicking the utility point with hardpoint ammo stores it in the installed rack.");
        });
    }

    [Test]
    public async Task DropshipATGMIsDeployedWithoutAnchoring()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var atgm = Server.System<CMUATGMSystem>();
            var box = SEntMan.SpawnAttachedTo("CMUDropshipAttachmentDoorGunATGM", map.GridCoords);
            Install(box, HardpointPointId, map.GridCoords);
            var mount = SEntMan.GetEntity(SEntMan.GetComponent<RMCEquipmentDeployerComponent>(box).DeployEntity);
            Assert.That(mount, Is.Not.Null);
            Assert.That(atgm.IsDeployed(mount!.Value), Is.False, "A launcher stowed in its hardpoint box is not deployed.");

            Assert.That(Server.System<SharedRMCEquipmentDeployerSystem>().TryDeploy(box, true), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<TransformComponent>(mount.Value).Anchored, Is.False);
                Assert.That(atgm.IsDeployed(mount.Value), Is.True);
            });

            // The ground launcher still has to be dug in.
            var ground = SEntMan.SpawnAttachedTo("CMUATGMMount", map.GridCoords);
            Server.System<FoldableSystem>().SetFolded(ground, SEntMan.GetComponent<FoldableComponent>(ground), false);
            Assert.That(atgm.IsDeployed(ground), Is.False);
            var groundXform = SEntMan.GetComponent<TransformComponent>(ground);
            Server.System<SharedTransformSystem>().AnchorEntity(ground, groundXform);
            Assert.That(groundXform.Anchored, Is.True);
            Assert.That(atgm.IsDeployed(ground), Is.True);
        });
    }

    private EntityUid DeployHardpoint(EntProtoId hardpointId, EntityCoordinates coords)
    {
        var box = SEntMan.SpawnAttachedTo(hardpointId, coords);
        Install(box, HardpointPointId, coords);
        Assert.That(Server.System<SharedRMCEquipmentDeployerSystem>().TryDeploy(box, true), Is.True);

        var mount = SEntMan.GetEntity(SEntMan.GetComponent<RMCEquipmentDeployerComponent>(box).DeployEntity);
        Assert.That(mount, Is.Not.Null);
        var weapon = SEntMan.GetComponent<WeaponMountComponent>(mount!.Value).MountedEntity;
        Assert.That(weapon, Is.Not.Null);
        return weapon!.Value;
    }

    // Leaves the spent magazine in its slot, which is how the M56D sits once its drum runs dry.
    private void EmptyWeapon(EntityUid weapon)
    {
        if (!TryGetMagazineSlot(weapon, out var slot) || slot.Item is not { } magazine)
            return;

        var ballistic = SEntMan.GetComponent<BallisticAmmoProviderComponent>(magazine);
        Server.System<SharedGunSystem>().SetBallisticUnspawned((magazine, ballistic), 0);
    }

    private bool TryGetMagazineSlot(EntityUid weapon, [NotNullWhen(true)] out ItemSlot? slot)
    {
        slot = null;
        return SEntMan.TryGetComponent(weapon, out ItemSlotsComponent? slots)
            && Server.System<ItemSlotsSystem>().TryGetSlot((weapon, slots), MagazineSlot, out slot);
    }

    private EntityUid InstallRack(EntityCoordinates coords)
    {
        var rack = SEntMan.SpawnAttachedTo(RackId, coords);
        Install(rack, UtilityPointId, coords);
        return rack;
    }

    private void FillRack(EntityUid rack, EntProtoId ammoId, int count, EntityCoordinates coords)
    {
        var storage = Server.System<SharedStorageSystem>();
        for (var i = 0; i < count; i++)
        {
            Assert.That(storage.Insert(rack, SEntMan.SpawnAttachedTo(ammoId, coords), out _, playSound: false), Is.True);
        }
    }

    private void Install(EntityUid attachment, EntProtoId pointId, EntityCoordinates coords)
    {
        var point = SEntMan.SpawnAttachedTo(pointId, coords);
        var slotId = SEntMan.GetComponent<DropshipUtilityPointComponent>(point).UtilitySlotId;
        var containers = Server.System<SharedContainerSystem>();
        var slot = containers.EnsureContainer<ContainerSlot>(point, slotId);
        Assert.That(containers.Insert(attachment, slot), Is.True);
    }
}
