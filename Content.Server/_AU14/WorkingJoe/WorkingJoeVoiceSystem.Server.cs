using Content.Server.Chat.Systems;
using Content.Shared._AU14.WorkingJoe;
using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Mobs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._AU14.WorkingJoe;

public sealed partial class WorkingJoeVoiceSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly (string SoundCollectionId, string? LocKey)[] DeathSounds =
    {
        ("AU14WorkingJoeDeathNormalVar1", null),
        ("AU14WorkingJoeDeathNormalVar2", null),
        ("AU14WorkingJoeDeathTomorrowVar1", null),
        ("AU14WorkingJoeDeathTomorrowVar2", null),
        ("AU14WorkingJoeSilenceVar1", null),
        ("AU14WorkingJoeSilenceVar2", null),
        ("AU14WorkingJoeSilenceVar3", null),
        ("AU14WorkingJoeSilenceVar4", null),
        ("AU14WorkingJoeToSleepPerchanceToDreamVar1", null),
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WorkingJoeVoiceComponent, PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<WorkingJoeVoiceComponent, PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<WorkingJoeVoiceComponent, WorkingJoeVoiceActionEvent>(OnAction);
        SubscribeLocalEvent<WorkingJoeVoiceComponent, WorkingJoePlayLineMessage>(OnPlayLine);
        SubscribeLocalEvent<WorkingJoeVoiceComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnPlayerAttached(Entity<WorkingJoeVoiceComponent> ent, ref PlayerAttachedEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
    }

    private void OnPlayerDetached(Entity<WorkingJoeVoiceComponent> ent, ref PlayerDetachedEvent args)
    {
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);
    }

    private void OnAction(Entity<WorkingJoeVoiceComponent> ent, ref WorkingJoeVoiceActionEvent args)
    {
        _ui.TryToggleUi(ent.Owner, WorkingJoeVoiceUiKey.Key, args.Performer);
        args.Handled = true;
    }

    private void OnPlayLine(Entity<WorkingJoeVoiceComponent> ent, ref WorkingJoePlayLineMessage args)
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

        var soundId = "AU14" + args.EmoteId;
        var sound = new SoundCollectionSpecifier(soundId);
        _audio.PlayPvs(sound, ent.Owner);
    }

    private void OnMobStateChanged(Entity<WorkingJoeVoiceComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var pick = _random.Pick(DeathSounds);
        var sound = new SoundCollectionSpecifier(pick.SoundCollectionId, AudioParams.Default.WithVolume(6f));
        _audio.PlayPvs(sound, ent.Owner);
    }
}
