using Content.Server.Chat.Systems;
using Content.Shared._CMU14.Wendigo;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using WendigoPlayLineMessage = Content.Shared._CMU14.Wendigo.WendigoPlayLineMessage;
using WendigoVoiceActionEvent = Content.Shared._CMU14.Wendigo.WendigoVoiceActionEvent;
using WendigoVoiceComponent = Content.Shared._CMU14.Wendigo.WendigoVoiceComponent;

namespace Content.Server._CMU14.Wendigo;

public sealed partial class WendigoVoiceSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoVoiceComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<WendigoVoiceComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<WendigoVoiceComponent, WendigoVoiceActionEvent>(OnAction);
        SubscribeLocalEvent<WendigoVoiceComponent, WendigoPlayLineMessage>(OnPlayLine);
    }

    private void OnPlayerAttached(Entity<WendigoVoiceComponent> ent, ref PlayerAttachedEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnPlayerDetached(Entity<WendigoVoiceComponent> ent, ref PlayerDetachedEvent args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnAction(Entity<WendigoVoiceComponent> ent, ref WendigoVoiceActionEvent args)
    {
        _ui.TryToggleUi(ent.Owner, WendigoVoiceUiKey.Key, args.Performer);
        args.Handled = true;
    }

    private static readonly Dictionary<string, string> EmoteToSound = new()
    {
        // Voice lines
        { "WendigoAllYourFault",     "/Audio/_AU14/Wendigo/mimicry/all_your_fault.ogg" },
        { "WendigoCloser",           "/Audio/_AU14/Wendigo/mimicry/closer.ogg" },
        { "WendigoComeCloser",       "/Audio/_AU14/Wendigo/mimicry/come_closer.ogg" },
        { "WendigoGuilty1",          "/Audio/_AU14/Wendigo/mimicry/guilty1.ogg" },
        { "WendigoGuilty2",          "/Audio/_AU14/Wendigo/mimicry/guilty2.ogg" },
        { "WendigoGuilty3",          "/Audio/_AU14/Wendigo/mimicry/guilty3.ogg" },
        { "WendigoHelpMe",           "/Audio/_AU14/Wendigo/mimicry/help_me.ogg" },
        { "WendigoHungry",           "/Audio/_AU14/Wendigo/mimicry/hungry.ogg" },
        { "WendigoImComingToHelp",   "/Audio/_AU14/Wendigo/mimicry/im_coming_to_help.ogg" },
        { "WendigoItsAlive",         "/Audio/_AU14/Wendigo/mimicry/its_alive.ogg" },
        { "WendigoItsInTheHouse",    "/Audio/_AU14/Wendigo/mimicry/its_in_the_house.ogg" },
        { "WendigoItsStillOutThere", "/Audio/_AU14/Wendigo/mimicry/its_still_out_there.ogg" },
        { "WendigoJustAStory",       "/Audio/_AU14/Wendigo/mimicry/just_a_story.ogg" },
        { "WendigoLetMeIn",          "/Audio/_AU14/Wendigo/mimicry/let_me_in.ogg" },
        { "WendigoLittleLight1",     "/Audio/_AU14/Wendigo/mimicry/little_light1.ogg" },
        { "WendigoLittleLight2",     "/Audio/_AU14/Wendigo/mimicry/little_light2.ogg" },
        { "WendigoNoNo",             "/Audio/_AU14/Wendigo/mimicry/no_no.ogg" },
        { "WendigoOpenTheDoor",      "/Audio/_AU14/Wendigo/mimicry/open_the_door.ogg" },
        { "WendigoPleaseNo",         "/Audio/_AU14/Wendigo/mimicry/please_no.ogg" },
        { "WendigoSaveMe",           "/Audio/_AU14/Wendigo/mimicry/save_me.ogg" },
        { "WendigoSecrets",          "/Audio/_AU14/Wendigo/mimicry/secrets.ogg" },
        { "WendigoSoAfraid",         "/Audio/_AU14/Wendigo/mimicry/so_afraid.ogg" },
        { "WendigoWeCantGetOut",     "/Audio/_AU14/Wendigo/mimicry/we_cant_get_out.ogg" },
        { "WendigoWhereAreYou",      "/Audio/_AU14/Wendigo/mimicry/where_are_you.ogg" },
    };

    private void OnPlayLine(Entity<WendigoVoiceComponent> ent, ref WendigoPlayLineMessage args)
    {
        if (!_proto.TryIndex<EmotePrototype>(args.EmoteId, out var emote))
            return;

        if (emote.ChatMessages.Count > 0)
        {
            var msg = Loc.GetString(_random.Pick(emote.ChatMessages));
            _chat.TrySendInGameICMessage(
                ent.Owner,
                msg,
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                nameOverride: null
            );
        }

        if (!EmoteToSound.TryGetValue(args.EmoteId, out var soundValue))
            return;

        SoundSpecifier sound = soundValue.StartsWith('/')
            ? new SoundPathSpecifier(soundValue)
            : new SoundCollectionSpecifier(soundValue);

        _audio.PlayPvs(sound, ent.Owner);
    }
}
