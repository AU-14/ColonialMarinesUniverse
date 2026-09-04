using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Armor.Magnetic;
using Content.Shared._RMC14.Attachable.Components;
using Content.Shared._RMC14.Attachable.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._CMU14.Weapons;

[TestFixture]
[TestOf(typeof(RMCMagneticSystem))]
public sealed class TwoPointSlingRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

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
}
