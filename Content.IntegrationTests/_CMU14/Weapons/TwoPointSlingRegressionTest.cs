using System.Numerics;
using Content.Client.Popups;
using Content.IntegrationTests.Fixtures;
using Content.Server.Hands.Systems;
using Content.Shared._RMC14.Armor.Magnetic;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests._CMU14.Weapons;

[TestFixture]
[TestOf(typeof(RMCMagneticSystem))]
public sealed class TwoPointSlingRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static PoolSettings ConnectedSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task M42A1SlingReturnsToSuitStorageWithoutMagneticArmor()
    {
        var map = await Pair.CreateTestMap();
        var server = Pair.Server;
        var gun = EntityUid.Invalid;
        var user = EntityUid.Invalid;

        await server.WaitAssertion(() =>
        {
            var hands = server.System<SharedHandsSystem>();
            var holders = server.System<AttachableHolderSystem>();
            user = server.EntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            gun = server.EntMan.SpawnEntity("WeaponShotgunM42A1", map.GridCoords);
            var sling = server.EntMan.SpawnEntity("RMCAttachmentTwoPointSling", map.GridCoords);
            var holder = server.EntMan.GetComponent<AttachableHolderComponent>(gun);

            Assert.That(holders.Attach((gun, holder), sling, user), Is.True);
            Assert.That(server.EntMan.HasComponent<RMCMagneticItemComponent>(gun), Is.True);
            Assert.That(hands.TryPickupAnyHand(user, gun, checkActionBlocker: false), Is.True);
            Assert.That(hands.TryDrop(user, gun, checkActionBlocker: false), Is.True);
        });

        await Pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var inventory = server.System<InventorySystem>();
            Assert.That(inventory.TryGetSlotEntity(user, "suitstorage", out var slung), Is.True);
            Assert.That(slung, Is.EqualTo(gun));
        });

        await server.WaitPost(() => server.System<SharedMapSystem>().DeleteMap(map.MapId));
    }

    [TestCase("RMCWeaponLauncherM5ATL", null, false)]
    [TestCase("WeaponShotgunM42A1", "RMCAttachmentMagneticHarness", false)]
    [TestCase("WeaponShotgunM42A1", "RMCAttachmentTwoPointSling", false)]
    [TestCase("RMCWeaponLauncherM5ATL", null, true)]
    public async Task FailedSlingReturnLeavesWeaponWithGroundFriction(string prototype, string attachment, bool fillSlotAfterThrow)
    {
        var map = await Pair.CreateTestMap();
        var gun = EntityUid.Invalid;
        var user = EntityUid.Invalid;
        var occupied = EntityUid.Invalid;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var maps = Server.System<SharedMapSystem>();
                for (var x = -2; x <= 20; x++)
                for (var y = -2; y <= 2; y++)
                    maps.SetTile(map.Grid, new Vector2i(x, y), map.Tile.Tile);
                var gravity = SEntMan.EnsureComponent<GravityComponent>(map.Grid);
                gravity.Enabled = true;
                gravity.Inherent = true;

                var hands = Server.System<HandsSystem>();
                var inventory = Server.System<InventorySystem>();
                user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                occupied = SEntMan.SpawnEntity("WeaponShotgunM42A1", map.GridCoords);
                if (!fillSlotAfterThrow)
                    Assert.That(inventory.TryEquip(user, occupied, "suitstorage", force: true), Is.True);

                gun = SEntMan.SpawnEntity(prototype, map.GridCoords);
                if (attachment != null)
                {
                    var sling = SEntMan.SpawnEntity(attachment, map.GridCoords);
                    var holder = SEntMan.GetComponent<AttachableHolderComponent>(gun);
                    Assert.That(Server.System<AttachableHolderSystem>().Attach((gun, holder), sling, user), Is.True);
                }

                Assert.That(hands.TryPickupAnyHand(user, gun, checkActionBlocker: false), Is.True);
                Assert.That(hands.ThrowHeldItem(user, map.GridCoords.Offset(new Vector2(4f, 0f))), Is.True);
                if (fillSlotAfterThrow)
                    Assert.That(inventory.TryEquip(user, occupied, "suitstorage", force: true), Is.True);

                Assert.That(SEntMan.GetComponent<PhysicsComponent>(gun).LinearVelocity.LengthSquared(), Is.GreaterThan(0),
                    "The regression must exercise a moving weapon, not a stationary drop.");
                Assert.That(Server.System<SharedGravitySystem>().IsWeightless(gun), Is.False);
            });

            await Pair.RunTicksSync(90);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(user, "suitstorage", out var slung), Is.True);
                Assert.That(slung, Is.EqualTo(occupied), "The original item must still occupy the sling destination.");
                Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(gun), Is.False,
                    "The failed return must leave the weapon in the world.");
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(gun), Is.False);
                var physics = SEntMan.GetComponent<PhysicsComponent>(gun);
                Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround),
                    "Cancelling a throw for a sling must not leave the weapon permanently airborne.");
                Assert.That(physics.LinearVelocity.Length(), Is.LessThan(0.05f),
                    "Ground friction must stop the weapon after the sling return fails.");
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.System<SharedMapSystem>().DeleteMap(map.MapId));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    [PairConfig(nameof(ConnectedSettings))]
    public async Task FailedAutomaticSlingReturnDoesNotShowEquipErrors(bool fillSlotAfterDrop)
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        var user = EntityUid.Invalid;
        var gun = EntityUid.Invalid;
        var occupied = EntityUid.Invalid;
        var errorMessage = string.Empty;

        try
        {
            await Server.WaitAssertion(() =>
            {
                user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                gun = SEntMan.SpawnEntity("RMCWeaponLauncherM5ATL", map.GridCoords);
                occupied = SEntMan.SpawnEntity("WeaponShotgunM42A1", map.GridCoords);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, gun), Is.True);
                Server.PlayerMan.SetAttachedEntity(session, user);
                errorMessage = Loc.GetString("inventory-component-can-unequip-cannot");
            });
            await Pair.RunTicksSync(5);

            await Server.WaitAssertion(() =>
            {
                var inventory = Server.System<InventorySystem>();
                if (!fillSlotAfterDrop)
                    Assert.That(inventory.TryEquip(user, occupied, "suitstorage", force: true), Is.True);

                Assert.That(Server.System<SharedHandsSystem>().TryDrop(user, gun), Is.True);

                // Filling the slot before the queued return runs must also fail quietly.
                if (fillSlotAfterDrop)
                    Assert.That(inventory.TryEquip(user, occupied, "suitstorage", force: true), Is.True);
            });
            await Pair.RunTicksSync(10);

            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(user, "suitstorage", out var slung), Is.True);
                Assert.That(slung, Is.EqualTo(occupied));
                Assert.That(Server.System<SharedContainerSystem>().IsEntityInContainer(gun), Is.False);
            });
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<PopupSystem>().CursorLabels.Any(label => label.Text.Contains(errorMessage)), Is.False,
                    "An automatic sling return must not show equip errors when the destination is occupied.");
            });

            // Verify the same failure is visible for an explicit equip attempt.
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<InventorySystem>().TryEquip(user, gun, "suitstorage", force: true), Is.False);
            });
            await Pair.RunTicksSync(3);
            await Client.WaitAssertion(() =>
            {
                Assert.That(Client.System<PopupSystem>().CursorLabels.Any(label => label.Text.Contains(errorMessage)), Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(session, originalAttached);
                Server.System<SharedMapSystem>().DeleteMap(map.MapId);
            });
        }
    }
}
