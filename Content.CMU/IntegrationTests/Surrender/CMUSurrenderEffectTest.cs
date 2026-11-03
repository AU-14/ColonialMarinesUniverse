using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.Chat;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Cuffs;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderEffectTest : GameTest
{
    [Test]
    public async Task SuccessfulSurrenderShowsOnlyAttachedAnimationAndExpiresWithoutLoweringHands()
    {
        var map = await Pair.CreateTestMap();
        var attached = ServerSession!.AttachedEntity;
        EntityUid human = default;
        EntityUid effect = default;
        var messages = new List<ChatMessage>();
        var chatUi = Client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>();
        NetEntity sender = default;
        void OnMessage(ChatMessage message)
        {
            if (message.SenderEntity == sender && message.Channel is ChatChannel.Local or ChatChannel.Emotes)
                messages.Add(message);
        }

        try
        {
            await Server.WaitPost(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                sender = SEntMan.GetNetEntity(human);
                Server.PlayerMan.SetAttachedEntity(ServerSession, human);
            });
            await Pair.RunUntilSynced();
            await Client.WaitPost(() => chatUi.MessageAdded += OnMessage);
            await Server.WaitAssertion(() =>
            {
                var item = SEntMan.SpawnEntity("Pen", map.GridCoords);
                var hands = Server.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(human, item), Is.True);
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(hands.EnumerateHeld(human).Any(), Is.False);
                effect = Effects(human).Single();
                Assert.That(SEntMan.GetComponent<TransformComponent>(effect).LocalPosition, Is.EqualTo(System.Numerics.Vector2.Zero));
                Assert.That(SEntMan.GetComponent<TimedDespawnComponent>(effect).Lifetime, Is.EqualTo(3f),
                    "Three seconds is the requested popup duration, independent of the inherited lifetime.");
                SEntMan.DeleteEntity(item);
            });
            await Pair.RunUntilSynced();
            var clientEffect = Pair.ToClientUid(effect);
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.EntityExists(clientEffect), Is.True);
                Assert.That(CEntMan.GetComponent<TransformComponent>(clientEffect).ParentUid, Is.EqualTo(Pair.ToClientUid(human)));
                Assert.That(messages, Has.Count.EqualTo(1), "Raising hands must send one action to the chat box.");
                Assert.That(messages[0].Channel, Is.EqualTo(ChatChannel.Emotes));
                Assert.That(messages[0].HideChat, Is.False);
                Assert.That(messages[0].HidePopup, Is.True, "The animation must remain the only overhead popup.");
            });
            await RunSeconds(2.5f);
            await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(effect), Is.False));
            await RunSeconds(0.6f);
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.Deleted(effect), Is.True);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.True);
                Assert.That(Effects(human), Is.Empty);
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
            });
            await Pair.RunUntilSynced();
            await Client.WaitAssertion(() =>
            {
                Assert.That(CEntMan.Deleted(clientEffect), Is.True);
                Assert.That(messages, Has.Count.EqualTo(1), "Lowering hands must not repeat the surrender action.");
            });
        }
        finally
        {
            await Client.WaitPost(() => chatUi.MessageAdded -= OnMessage);
            await Server.WaitPost(() =>
            {
                Server.PlayerMan.SetAttachedEntity(ServerSession, attached);
                if (SEntMan.EntityExists(human)) SEntMan.DeleteEntity(human);
            });
        }
    }

    [TestCase("toggle")]
    [TestCase("cuffs")]
    [TestCase("component removal")]
    [TestCase("deletion")]
    public async Task EarlyEndRemovesAnimationAndRepeatedActivationNeverDuplicatesIt(string ending)
    {
        var map = await Pair.CreateTestMap();
        EntityUid human = default;
        EntityUid effect = default;
        try
        {
            await Server.WaitAssertion(() =>
            {
                human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
                var chat = Server.System<ChatSystem>();
                for (var cycle = 0; cycle < 3; cycle++)
                {
                    Assert.That(chat.TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                    effect = Effects(human).Single();
                    Assert.That(chat.TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                }
            });
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.Deleted(effect), Is.True);
                Assert.That(Effects(human), Is.Empty);
                Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                effect = Effects(human).Single();
                switch (ending)
                {
                    case "toggle":
                        Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote), Is.True);
                        break;
                    case "cuffs":
                        Assert.That(Server.System<SharedCuffableSystem>().TryAddCuffsInstant(human, "RMCHandcuffs"), Is.Not.Null);
                        break;
                    case "component removal":
                        SEntMan.RemoveComponent<CMUSurrenderComponent>(human);
                        break;
                    case "deletion":
                        SEntMan.DeleteEntity(human);
                        break;
                }
            });
            await Pair.RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.Deleted(effect), Is.True, "Cancellation must not wait for the three-second timeout.");
                Assert.That(Effects(human), Is.Empty);
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(human)) SEntMan.DeleteEntity(human);
            });
        }
    }

    [Test]
    public async Task UndroppableItemPreventsSurrenderAndAnimation()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var human = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var item = SEntMan.SpawnEntity("Pen", map.GridCoords);
            try
            {
                var hands = Server.System<SharedHandsSystem>();
                Assert.That(hands.TryPickupAnyHand(human, item), Is.True);
                SEntMan.AddComponent<UnremoveableComponent>(item);
                Server.System<ChatSystem>().TryEmoteWithChat(human, CMUSurrenderSystem.SurrenderEmote);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(human), Is.False);
                Assert.That(Effects(human), Is.Empty);
                Assert.That(hands.EnumerateHeld(human), Does.Contain(item));
            }
            finally
            {
                SEntMan.DeleteEntity(human);
                if (SEntMan.EntityExists(item)) SEntMan.DeleteEntity(item);
            }
        });
    }

    private List<EntityUid> Effects(EntityUid parent)
    {
        var effects = new List<EntityUid>();
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var meta, out var transform))
        {
            if (meta.EntityPrototype?.ID == "CMUEffectSurrenderHands" && transform.ParentUid == parent && !SEntMan.IsQueuedForDeletion(uid))
                effects.Add(uid);
        }
        return effects;
    }
}
