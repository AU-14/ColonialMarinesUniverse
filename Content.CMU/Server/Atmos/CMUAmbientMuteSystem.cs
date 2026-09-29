using Content.Shared.Audio;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tools.Systems;

namespace Content.Server.CMU14.Atmos;

public sealed class CMUAmbientMuteSystem : EntitySystem
{
    private const string ScrewingQuality = "Screwing";

    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUAmbientMuteComponent, InteractUsingEvent>(OnInteractUsing);
    }

    // The scrubber system re-enables ambience every atmos tick, so muting has
    // to remove the component outright; SetAmbience on a missing comp is a
    // no-op. AmbientSoundComponent fields are read-only outside
    // SharedAmbientSoundSystem, hence the saved copy lives here.
    private void OnInteractUsing(Entity<CMUAmbientMuteComponent> ent, ref InteractUsingEvent args)
    {
        if (!_tool.HasQuality(args.Used, ScrewingQuality))
            return;

        args.Handled = true;

        if (TryComp(ent, out AmbientSoundComponent? ambient))
        {
            ent.Comp.Sound = ambient.Sound;
            ent.Comp.Volume = ambient.Volume;
            ent.Comp.Range = ambient.Range;
            ent.Comp.Enabled = ambient.Enabled;

            RemComp<AmbientSoundComponent>(ent);
            _popup.PopupClient(Loc.GetString("cmu-ambient-mute-muted"), ent, args.User);
        }
        else if (ent.Comp.Sound is { } sound)
        {
            var restored = AddComp<AmbientSoundComponent>(ent);
            _ambient.SetSound(ent, sound, restored);
            _ambient.SetVolume(ent, ent.Comp.Volume, restored);
            _ambient.SetRange(ent, ent.Comp.Range, restored);
            _ambient.SetAmbience(ent, ent.Comp.Enabled, restored);
            _popup.PopupClient(Loc.GetString("cmu-ambient-mute-unmuted"), ent, args.User);
        }
    }
}
