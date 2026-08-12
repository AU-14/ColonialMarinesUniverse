using Content.Shared._RMC14.Hands;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14.Inventory;

[TestFixture]
[TestOf(typeof(RMCHandsSystem))]
public sealed class RMCBootsItemSlotTest
{
    [Test]
    public async Task ClickingFilledBootsEjectsBayonetToWearersHand()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var entMan = server.EntMan;
            var hands = server.System<SharedHandsSystem>();
            var inventory = server.System<InventorySystem>();
            var itemSlots = server.System<ItemSlotsSystem>();
            var rmcHands = server.System<RMCHandsSystem>();
            var wearer = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var boots = entMan.SpawnEntity("CMBootsBlackFilled", map.GridCoords);

            Assert.That(inventory.TryEquip(wearer, boots, "shoes", force: true), Is.True);

            var slot = entMan.GetComponent<ItemSlotsComponent>(boots).Slots["item"];
            var bayonet = slot.Item;
            Assert.That(bayonet, Is.Not.Null);
            Assert.That(entMan.GetComponent<MetaDataComponent>(bayonet!.Value).EntityPrototype?.ID,
                Is.EqualTo("RMCM5Bayonet"));
            Assert.That(rmcHands.TryStorageEjectHand(wearer, boots), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(itemSlots.GetItemOrNull(boots, "item"), Is.Null);
                Assert.That(hands.GetActiveItem(wearer), Is.EqualTo(bayonet));
            });
        });

        await pair.CleanReturnAsync();
    }
}
