using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.ActionBlocker;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderInteractionTest : GameTest
{
    [TestCase("RMCWeaponRifleM54C", "back")]
    [TestCase("Crowbar", "pocket1")]
    public async Task InventoryRetrievalIsBlockedOnServerAndPredictingClient(string prototype, string slot)
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid human = default;
        EntityUid item = default;
        EntityUid other = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                other = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                item = SEntMan.SpawnEntity(prototype, map.GridCoords);
                Assert.That(Server.System<InventorySystem>().TryEquip(human, item, slot, force: true), Is.True);
                Server.PlayerMan.SetAttachedEntity(ServerSession, human);
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);

                var inventory = Server.System<InventorySystem>();
                Assert.That(inventory.CanUnequip(human, slot, out var reason), Is.False);
                Assert.That(reason, Is.EqualTo("cmu-surrender-hands-raised"));
                Assert.That(inventory.TryUnequip(human, slot), Is.False);
                Assert.That(inventory.CanUnequip(other, human, slot, out _), Is.True,
                    "Other players must still be able to disarm a surrendered player.");
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(human, item), Is.False);
                Assert.That(inventory.TryGetSlotEntity(human, slot, out var equipped), Is.True);
                Assert.That(equipped, Is.EqualTo(item));
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.True);
            });
            await Pair.RunUntilSynced();
            var clientHuman = Pair.ToClientUid(human);
            var clientItem = Pair.ToClientUid(item);
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.HasComponent<CMUSurrenderComponent>(clientHuman), Is.True);
                Assert.That(Client.System<ActionBlockerSystem>().CanPickup(clientHuman, clientItem), Is.False);
                Assert.That(Client.System<InventorySystem>().CanUnequip(clientHuman, slot, out _), Is.False);
                Assert.That(Client.System<SharedHandsSystem>().TryPickupAnyHand(clientHuman, clientItem,
                    checkActionBlocker: false), Is.False);

                var containers = Client.System<SharedContainerSystem>();
                foreach (var name in CEntMan.GetComponent<HandsComponent>(clientHuman).Hands.Keys)
                    Assert.That(containers.CanInsert(clientItem, containers.GetContainer(clientHuman, name)), Is.False);
            });
            await Server.WaitAssertion(() =>
            {
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(Server.System<InventorySystem>().TryUnequip(human, slot), Is.True);
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(human, item), Is.True);
            });
        }
        finally
        {
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession, attached));
            await Server.WaitPost(() =>
            {
                foreach (var entity in new[] { human, item, other })
                {
                    if (SEntMan.EntityExists(entity))
                        SEntMan.DeleteEntity(entity);
                }
            });
        }
    }

    [Test]
    public async Task BlockedStoragePickupDoesNotRemoveTheItemOrEndSurrender()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var bag = SEntMan.SpawnEntity("CMSatchel", map.GridCoords);
            var item = SEntMan.SpawnEntity("Pen", map.GridCoords);
            try
            {
                var storage = Server.System<SharedStorageSystem>();
                var hands = Server.System<SharedHandsSystem>();
                var containers = Server.System<SharedContainerSystem>();
                var chat = Server.System<ChatSystem>();
                Assert.That(Server.System<InventorySystem>().TryEquip(human, bag, "back", force: true), Is.True);
                Assert.That(storage.Insert(bag, item, out _, playSound: false), Is.True);
                var source = SEntMan.GetComponent<StorageComponent>(bag).Container;
                Assert.That(chat.TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);

                Assert.That(hands.TryPickupAnyHand(human, item), Is.False);
                Assert.That(hands.TryPickupAnyHand(human, item, checkActionBlocker: false), Is.False);
                foreach (var name in SEntMan.GetComponent<HandsComponent>(human).Hands.Keys)
                {
                    var destination = containers.GetContainer(human, name);
                    Assert.That(containers.CanInsert(item, destination), Is.False);
                    Assert.That(containers.Insert(item, destination), Is.False);
                }
                Assert.That(source.ContainedEntities, Does.Contain(item));
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.True);
                Assert.That(hands.EnumerateHeld(human).Any(), Is.False);

                Assert.That(chat.TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(hands.TryPickupAnyHand(human, item), Is.True);
                Assert.That(source.ContainedEntities, Does.Not.Contain(item));
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                if (!SEntMan.Deleted(bag)) SEntMan.DeleteEntity(bag);
                if (!SEntMan.Deleted(item)) SEntMan.DeleteEntity(item);
            }
        });
    }
}
