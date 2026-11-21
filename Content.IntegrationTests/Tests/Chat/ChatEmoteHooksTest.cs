#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Chat.Systems;
using Content.Shared.CMU14.Chat;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Emote;
using Content.Shared._RMC14.Voicelines;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Chat;

[TestFixture]
[TestOf(typeof(ChatSystem))]
public sealed class ChatEmoteHooksTest : GameTest
{

    [SidedDependency(Side.Client)]
    private readonly IUserInterfaceManager _uiManager = null!;

    [Test]
    public async Task BeforeCancellationAndTypedCooldownRaiseOneEventAndKeepChatText()
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        var source = await PrepareSource(map.GridCoords);
        var messages = new List<ChatMessage>();
        var controller = _uiManager.GetUIController<ChatUIController>();
        Action<ChatMessage> handler = message =>
        {
            if (message.Channel == ChatChannel.Emotes)
                messages.Add(message);
        };

        await Client.WaitPost(() => controller.MessageAdded += handler);
        try
        {
            await Server.WaitPost(() =>
            {
                var chat = Server.System<ChatSystem>();
                var probe = SEntMan.GetComponent<ChatEmoteTestProbeComponent>(source);
                probe.CancelNext = true;

                chat.TrySendInGameICMessage(
                    source,
                    "screams",
                    InGameICChatType.Emote,
                    hideChat: false,
                    player: null,
                    ignoreActionBlocker: true);

                Assert.Multiple(() =>
                {
                    Assert.That(probe.BeforeEvents, Is.EqualTo(1));
                    Assert.That(probe.EmoteEvents, Is.Zero,
                        "a cancelled BeforeEmoteEvent must suppress the typed EmoteEvent");
                });

                var cooldown = SEntMan.GetComponent<EmoteCooldownComponent>(source);
                cooldown.NextEmote = TimeSpan.Zero;
                SEntMan.Dirty(source, cooldown);

                chat.TrySendInGameICMessage(
                    source,
                    "screams",
                    InGameICChatType.Emote,
                    hideChat: false,
                    player: null,
                    ignoreActionBlocker: true);
                chat.TrySendInGameICMessage(
                    source,
                    "screams",
                    InGameICChatType.Emote,
                    hideChat: false,
                    player: null,
                    ignoreActionBlocker: true);

                Assert.Multiple(() =>
                {
                    Assert.That(probe.BeforeEvents, Is.EqualTo(2),
                        "the cooldown-denied typed emote must not enter the BeforeEmote hook");
                    Assert.That(probe.EmoteEvents, Is.EqualTo(1),
                        "two immediate typed messages must raise one EmoteEvent");
                });

                Assert.That(chat.TryEmoteWithoutChat(source, "Scream", ignoreActionBlocker: true), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(probe.BeforeEvents, Is.EqualTo(3));
                    Assert.That(probe.EmoteEvents, Is.EqualTo(2),
                        "one direct shared call must raise one additional EmoteEvent");
                });
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                Assert.That(messages, Has.Count.EqualTo(2),
                    "a cooldown denial keeps the typed emote text visible without duplicating the event");
                Assert.That(messages.All(message => message.Message == "screams"), Is.True);
            });
        }
        finally
        {
            try
            {
                await Client.WaitPost(() => controller.MessageAdded -= handler);
            }
            finally
            {
                await CleanupSource(source, originalAttached);
            }
        }
    }

    private async Task<EntityUid> PrepareSource(EntityCoordinates coordinates)
    {
        EntityUid source = default;
        await Server.WaitPost(() =>
        {
            _ = Server.System<ChatEmoteTestProbeSystem>();
            source = SEntMan.SpawnEntity("MobHuman", coordinates);
            var vocal = SEntMan.EnsureComponent<VocalComponent>(source);
            vocal.EmoteSounds = "MaleHuman";
            vocal.WilhelmProbability = 0;
            SEntMan.Dirty(source, vocal);

            var cooldown = SEntMan.EnsureComponent<EmoteCooldownComponent>(source);
            cooldown.NextEmote = TimeSpan.Zero;
            SEntMan.Dirty(source, cooldown);
            SEntMan.EnsureComponent<ChatEmoteTestProbeComponent>(source);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, source);
        });
        await Pair.RunUntilSynced();
        return source;
    }

    private async Task CleanupSource(EntityUid source, EntityUid? originalAttached)
    {
        await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(ServerSession!, originalAttached));
        await Pair.DeleteEntityTreeLeafFirst(source);
        await Pair.RunUntilSynced();
    }

}

[RegisterComponent]
public sealed partial class ChatEmoteTestProbeComponent : Component
{
    public int BeforeEvents;
    public int EmoteEvents;
    public bool CancelNext;
}

public sealed class ChatEmoteTestProbeSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ChatEmoteTestProbeComponent, BeforeEmoteEvent>(OnBeforeEmote);
        SubscribeLocalEvent<ChatEmoteTestProbeComponent, EmoteEvent>(OnEmote);
    }

    private static void OnBeforeEmote(Entity<ChatEmoteTestProbeComponent> ent, ref BeforeEmoteEvent args)
    {
        ent.Comp.BeforeEvents++;
        if (!ent.Comp.CancelNext)
            return;

        ent.Comp.CancelNext = false;
        args.Cancel();
    }

    private static void OnEmote(Entity<ChatEmoteTestProbeComponent> ent, ref EmoteEvent args)
    {
        ent.Comp.EmoteEvents++;
    }
}

#pragma warning restore RA0002
