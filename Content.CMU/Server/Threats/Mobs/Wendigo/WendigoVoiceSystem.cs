using Content.Server.Chat.Systems;
using Content.Shared._CMU14.Threats.Mobs.Wendigo;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using WendigoPlayLineMessage = Content.Shared._CMU14.Threats.Mobs.Wendigo.WendigoPlayLineMessage;
using WendigoVoiceActionEvent = Content.Shared._CMU14.Threats.Mobs.Wendigo.WendigoVoiceActionEvent;
using WendigoVoiceComponent = Content.Shared._CMU14.Threats.Mobs.Wendigo.WendigoVoiceComponent;

namespace Content.Server._CMU14.Threats.Mobs.Wendigo;

public sealed partial class WendigoVoiceSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly Dictionary<string, string> EmoteToSound = new()
    {
        // Voice lines
        { "WendigoAllYourFault", "/Audio/Wendigo/mimicry/all_your_fault.ogg" },
        { "WendigoCloser", "/Audio/Wendigo/mimicry/closer.ogg" },
        { "WendigoComeCloser", "/Audio/Wendigo/mimicry/come_closer.ogg" },
        { "WendigoGuilty1", "/Audio/Wendigo/mimicry/guilty1.ogg" },
        { "WendigoGuilty2", "/Audio/Wendigo/mimicry/guilty2.ogg" },
        { "WendigoGuilty3", "/Audio/Wendigo/mimicry/guilty3.ogg" },
        { "WendigoHelpMe", "/Audio/Wendigo/mimicry/help_me.ogg" },
        { "WendigoHungry", "/Audio/Wendigo/mimicry/hungry.ogg" },
        { "WendigoImComingToHelp", "/Audio/Wendigo/mimicry/im_coming_to_help.ogg" },
        { "WendigoItsAlive", "/Audio/Wendigo/mimicry/its_alive.ogg" },
        { "WendigoItsInTheHouse", "/Audio/Wendigo/mimicry/its_in_the_house.ogg" },
        { "WendigoItsStillOutThere", "/Audio/Wendigo/mimicry/its_still_out_there.ogg" },
        { "WendigoJustAStory", "/Audio/Wendigo/mimicry/just_a_story.ogg" },
        { "WendigoLetMeIn", "/Audio/Wendigo/mimicry/let_me_in.ogg" },
        { "WendigoLittleLight1", "/Audio/Wendigo/mimicry/little_light1.ogg" },
        { "WendigoLittleLight2", "/Audio/Wendigo/mimicry/little_light2.ogg" },
        { "WendigoNoNo", "/Audio/Wendigo/mimicry/no_no.ogg" },
        { "WendigoOpenTheDoor", "/Audio/Wendigo/mimicry/open_the_door.ogg" },
        { "WendigoPleaseNo", "/Audio/Wendigo/mimicry/please_no.ogg" },
        { "WendigoSaveMe", "/Audio/Wendigo/mimicry/save_me.ogg" },
        { "WendigoSecrets", "/Audio/Wendigo/mimicry/secrets.ogg" },
        { "WendigoSoAfraid", "/Audio/Wendigo/mimicry/so_afraid.ogg" },
        { "WendigoWeCantGetOut", "/Audio/Wendigo/mimicry/we_cant_get_out.ogg" },
        { "WendigoWhereAreYou", "/Audio/Wendigo/mimicry/where_are_you.ogg" }
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WendigoVoiceComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<WendigoVoiceComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<WendigoVoiceComponent, WendigoVoiceActionEvent>(OnAction);
        SubscribeLocalEvent<WendigoVoiceComponent, WendigoPlayLineMessage>(OnPlayLine);
    }

    private void OnPlayerAttached(Entity<WendigoVoiceComponent> ent, ref PlayerAttachedEvent args)
        => _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);

    private void OnPlayerDetached(Entity<WendigoVoiceComponent> ent, ref PlayerDetachedEvent args)
        => _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

    private void OnAction(Entity<WendigoVoiceComponent> ent, ref WendigoVoiceActionEvent args)
    {
        _ui.TryToggleUi(ent.Owner, WendigoVoiceUiKey.Key, args.Performer);
        args.Handled = true;
    }

    private void OnPlayLine(Entity<WendigoVoiceComponent> ent, ref WendigoPlayLineMessage args)
    {
        if (!_proto.TryIndex(args.EmoteId, out EmotePrototype? emote))
            return;

        if (emote.ChatMessages.Count > 0)
        {
            string msg = Loc.GetString(_random.Pick(emote.ChatMessages));
            _chat.TrySendInGameICMessage(ent.Owner,
                msg,
                InGameICChatType.Speak,
                ChatTransmitRange.Normal,
                nameOverride: null);
        }

        if (!EmoteToSound.TryGetValue(args.EmoteId, out string? soundValue))
            return;

        SoundSpecifier sound = soundValue.StartsWith('/')
            ? new SoundPathSpecifier(soundValue)
            : new SoundCollectionSpecifier(soundValue);

        _audio.PlayPvs(sound, ent.Owner);
    }
}
