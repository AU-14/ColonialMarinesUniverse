using Content.Server.Chat.Systems;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;

namespace Content.Server.Radio.EntitySystems;

/// <inheritdoc />
public sealed partial class RadioDeviceSystem : SharedRadioDeviceSystem
{
    [Dependency] private ChatSystem _chat = default!;

    protected override bool CanRelaySpeech(Entity<RadioMicrophoneComponent> ent, EntityUid source)
    {
        return !ent.Comp.IgnoreXenos || !HasComp<XenoComponent>(source);
    }

    protected override void OnReceiveRadio(Entity<RadioSpeakerComponent> ent, ref RadioReceiveEvent args)
    {
        if (ent.Owner == args.RadioSource)
            return;

        var nameEv = new TransformSpeakerNameEvent(args.MessageSource, Name(args.MessageSource));
        RaiseLocalEvent(args.MessageSource, nameEv);

        // Log to chat so people can identify the speaker/source, but avoid clogging ghost chat if there are many radios.
        _chat.SendRadioSpeakerWhisperWithLanguage(
            ent.Owner,
            args.Message,
            args.Language,
            nameEv.VoiceName,
            ignoreXenos: true,
            originalSpeaker: args.MessageSource);
    }
}
