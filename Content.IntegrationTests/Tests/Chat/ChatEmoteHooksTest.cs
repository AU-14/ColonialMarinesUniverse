#pragma warning disable RA0002 // Integration regression intentionally inspects restricted component state.

using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Chat.Systems;
using Content.Shared._CMU14.Chat;
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
    private static readonly string[] PainMessages =
    [
        "OW!!",
        "AGH!!",
        "ARGH!!",
        "OUCH!!",
        "ACK!!",
        "OUF!",
    ];

    private static readonly string[] ScreamMessages =
    [
        "FUCK!!!",
        "AGH!!!",
        "ARGH!!!",
        "AAAA!!!",
        "HGH!!!",
        "NGHHH!!!",
        "NNHH!!!",
        "SHIT!!!",
    ];

    [SidedDependency(Side.Client)]
    private readonly IUserInterfaceManager _uiManager = null!;

    [Test]
    public async Task ScreamHotbarPreferenceIsHiddenByDefaultAndDoesNotDuplicateOnToggle()
    {
        var map = await Pair.CreateTestMap();
        var session = ServerSession!;
        var originalAttached = session.AttachedEntity;
        var source = await PrepareSource(map.GridCoords);
        var clientSource = Pair.ToClientUid(source);
        var originalPreference = Client.CfgMan.GetCVar(CCVars.CMUScreamOnHotbarEnabled);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<VocalComponent>(source), Is.True);
            Assert.That(GetScreamActions(SEntMan, Server.System<SharedActionsSystem>(), source), Is.Empty,
                "the scream action must remain off the hotbar until the player opts in");
        });

        try
        {
            await Client.WaitPost(() => Client.CfgMan.SetCVar(CCVars.CMUScreamOnHotbarEnabled, true));
            await Pair.RunUntilSynced();

            EntityUid firstAction = default;
            await Server.WaitAssertion(() =>
            {
                var actions = GetScreamActions(SEntMan, Server.System<SharedActionsSystem>(), source);
                Assert.That(actions, Has.Count.EqualTo(1));
                firstAction = actions[0];
            });
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(GetScreamActions(CEntMan, Client.System<SharedActionsSystem>(), clientSource),
                    Has.Count.EqualTo(1));
            });

            await Client.WaitPost(() => Client.CfgMan.SetCVar(CCVars.CMUScreamOnHotbarEnabled, false));
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                AssertRetainedScreamAction(SEntMan, Server.System<SharedActionsSystem>(), source, firstAction);
            });
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(GetScreamActions(CEntMan, Client.System<SharedActionsSystem>(), clientSource), Is.Empty);
            });

            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, null));
            await Pair.RunUntilSynced();
            await Server.WaitPost(() => Server.PlayerMan.SetAttachedEntity(session, source));
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                AssertRetainedScreamAction(SEntMan, Server.System<SharedActionsSystem>(), source, firstAction);
            });

            await Client.WaitPost(() => Client.CfgMan.SetCVar(CCVars.CMUScreamOnHotbarEnabled, true));
            await Pair.RunUntilSynced();
            await Server.WaitAssertion(() =>
            {
                var actions = GetScreamActions(SEntMan, Server.System<SharedActionsSystem>(), source);
                Assert.Multiple(() =>
                {
                    Assert.That(actions, Has.Count.EqualTo(1),
                        "re-enabling the preference must not create duplicate scream actions");
                    Assert.That(actions[0], Is.EqualTo(firstAction),
                        "the retained action entity should be reattached instead of respawned");
                });
            });
            await Pair.RunTicksSync(2);
            await Client.WaitAssertion(() =>
            {
                Assert.That(GetScreamActions(CEntMan, Client.System<SharedActionsSystem>(), clientSource),
                    Has.Count.EqualTo(1));
            });
        }
        finally
        {
            try
            {
                await Client.WaitPost(() => Client.CfgMan.SetCVar(CCVars.CMUScreamOnHotbarEnabled, originalPreference));
                await Pair.RunUntilSynced();
            }
            finally
            {
                await CleanupSource(source, originalAttached);
            }
        }
    }

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

    [Test]
    public async Task PainAndScreamPresentationRespectRecipientFilterWithoutDuplicateSound()
    {
        var map = await Pair.CreateTestMap();
        var originalAttached = ServerSession!.AttachedEntity;
        var source = await PrepareSource(map.GridCoords);
        var clientSource = Pair.ToClientUid(source);
        var messages = new List<ChatMessage>();
        var controller = _uiManager.GetUIController<ChatUIController>();
        var originalPlaySelf = Client.CfgMan.GetCVar(RMCCVars.RMCPlayEmotesYourself);
        Action<ChatMessage> handler = message =>
        {
            if (message.Channel == ChatChannel.Emotes)
                messages.Add(message);
        };

        await Client.WaitPost(() =>
        {
            controller.MessageAdded += handler;
            Client.CfgMan.SetCVar(RMCCVars.RMCPlayEmotesYourself, false);
        });

        try
        {
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                var voices = Server.System<HumanoidVoicelinesSystem>();
                Assert.That(voices.ShouldPlayEmote(source, ServerSession!), Is.False,
                    "the self-recipient emote CVar must remove this session from the sound filter");

                var chat = Server.System<ChatSystem>();
                Assert.That(chat.TryEmoteWithChat(
                    source,
                    "Scream",
                    ignoreActionBlocker: true,
                    forceEmote: true), Is.True);
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(messages, Has.Count.EqualTo(1));
                    Assert.That(messages[0].Message, Is.AnyOf(ScreamMessages));
                    Assert.That(messages[0].SpeechStyleClass, Is.EqualTo(CMURunechatStyles.Scream));
                    Assert.That(AttachedAudioCount(clientSource), Is.Zero,
                        "a filtered recipient must still see runechat but receive no emote sound");
                });
            });

            await Client.WaitPost(() => Client.CfgMan.SetCVar(RMCCVars.RMCPlayEmotesYourself, true));
            await Pair.RunTicksSync(3);
            await Server.WaitAssertion(() =>
            {
                var voices = Server.System<HumanoidVoicelinesSystem>();
                Assert.That(voices.ShouldPlayEmote(source, ServerSession!), Is.True);

                var chat = Server.System<ChatSystem>();
                Assert.That(chat.TryEmoteWithChat(
                    source,
                    "PainGrimace",
                    ignoreActionBlocker: true,
                    forceEmote: true), Is.True);
                Assert.That(chat.TryEmoteWithChat(
                    source,
                    "Scream",
                    ignoreActionBlocker: true,
                    forceEmote: true), Is.True);

                var probe = SEntMan.GetComponent<ChatEmoteTestProbeComponent>(source);
                Assert.That(probe.EmoteEvents, Is.EqualTo(3),
                    "three direct emote calls must produce exactly three events");
            });
            await Pair.RunTicksSync(3);

            await Client.WaitAssertion(() =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(messages, Has.Count.EqualTo(3));
                    Assert.That(messages[1].Message, Is.AnyOf(PainMessages));
                    Assert.That(messages[1].SpeechStyleClass, Is.EqualTo(CMURunechatStyles.Pain));
                    Assert.That(messages[2].Message, Is.AnyOf(ScreamMessages));
                    Assert.That(messages[2].SpeechStyleClass, Is.EqualTo(CMURunechatStyles.Scream));
                    Assert.That(AttachedAudioCount(clientSource), Is.EqualTo(1),
                        "one accepted vocal emote must create one sound despite Shared and server hooks");
                });
            });
        }
        finally
        {
            try
            {
                await Client.WaitPost(() =>
                {
                    controller.MessageAdded -= handler;
                    Client.CfgMan.SetCVar(RMCCVars.RMCPlayEmotesYourself, originalPlaySelf);
                });
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

    private int AttachedAudioCount(EntityUid source)
    {
        return CEntMan.EntityQuery<AudioComponent>()
            .Count(audio => CEntMan.GetComponent<TransformComponent>(audio.Owner).ParentUid == source);
    }

    private static List<EntityUid> GetScreamActions(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid source)
    {
        var instantActions = entMan.GetEntityQuery<InstantActionComponent>();
        return actions.GetActions(source)
            .Where(action => instantActions.CompOrNull(action)?.Event is EmoteActionEvent)
            .Select(action => action.Owner)
            .ToList();
    }

    private static void AssertRetainedScreamAction(
        IEntityManager entMan,
        SharedActionsSystem actions,
        EntityUid source,
        EntityUid expectedAction)
    {
        var vocal = entMan.GetComponent<VocalComponent>(source);
        var action = entMan.GetComponent<ActionComponent>(expectedAction);
        var performerActions = actions.GetActions(source).Select(entity => entity.Owner);

        Assert.Multiple(() =>
        {
            Assert.That(vocal.EmoteActionEntity, Is.EqualTo(expectedAction),
                "disabling the preference must retain the same action entity for reuse");
            Assert.That(action.Container, Is.EqualTo(source),
                "the retained action must remain owned by its vocal source");
            Assert.That(action.AttachedEntity, Is.Null,
                "the retained action must be detached from the performer's hotbar");
            Assert.That(performerActions, Does.Not.Contain(expectedAction),
                "the detached action must not remain in the performer's action list");
        });
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
