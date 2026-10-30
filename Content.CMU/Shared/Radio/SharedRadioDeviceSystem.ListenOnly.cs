using Content.Shared.CMU14.Radio;
using Content.Shared.Examine;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Shared.Radio.EntitySystems;

public abstract partial class SharedRadioDeviceSystem
{
    private bool CMUTryCycleHandheld(Entity<RadioMicrophoneComponent> ent, EntityUid user, bool quiet)
    {
        if (!HasComp<CMURadioListenOnlyComponent>(ent) || !TryComp<RadioSpeakerComponent>(ent, out var speaker))
            return false;

        if (ent.Comp.PowerRequired && !_power.IsPowered(ent.Owner))
            return true;

        // Both on -> receive only -> both off -> both on. Use the shared setters so prediction,
        // visuals and listener subscriptions agree with the authoritative component state.
        var microphoneEnabled = !ent.Comp.Enabled && !speaker.Enabled;
        var speakerEnabled = ent.Comp.Enabled || !speaker.Enabled;
        SetMicrophoneEnabled(ent.AsNullable(), user, microphoneEnabled, quiet: true);
        SetSpeakerEnabled((ent.Owner, speaker), user, speakerEnabled, quiet: true);
        if (!quiet)
        {
            var state = Loc.GetString(CMUHandheldState(ent.Comp, speaker));
            _popup.PopupClient(Loc.GetString("handheld-radio-component-on-use", ("radioState", state)), user, user);
        }
        return true;
    }

    [SubscribeLocalEvent]
    private void CMUOnExamineHandheld(Entity<CMURadioListenOnlyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange ||
            !TryComp<RadioMicrophoneComponent>(ent, out var microphone) ||
            !TryComp<RadioSpeakerComponent>(ent, out var speaker))
            return;

        args.PushMarkup(Loc.GetString("cmu-handheld-radio-state", ("state", Loc.GetString(CMUHandheldState(microphone, speaker)))));
    }

    private static string CMUHandheldState(RadioMicrophoneComponent microphone, RadioSpeakerComponent speaker)
    {
        return microphone.Enabled
            ? "handheld-radio-component-on-state"
            : speaker.Enabled ? "cmu-handheld-radio-listen-only" : "handheld-radio-component-off-state";
    }
}
