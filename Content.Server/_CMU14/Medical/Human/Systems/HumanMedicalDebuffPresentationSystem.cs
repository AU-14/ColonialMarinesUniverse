using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._CMU14.Medical.Human.Systems;

public sealed partial class HumanMedicalDebuffPresentationSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier SplintBreakSound = new SoundPathSpecifier(
        "/Audio/_RMC14/Medical/splint.ogg",
        AudioParams.Default.WithVariation(0.1f).WithVolume(-1f));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HumanSplintBrokenEvent>(OnSplintBroken);
        SubscribeLocalEvent<HumanInternalRipEvent>(OnInternalRip);
        SubscribeLocalEvent<HumanWoundWidenedEvent>(OnWoundWidened);
    }

    private void OnSplintBroken(ref HumanSplintBrokenEvent args)
    {
        _popup.PopupEntity(
            Loc.GetString(
                "cmu-medical-splint-break",
                ("part", GetRegionName(args.Region))),
            args.Body,
            args.Body,
            PopupType.LargeCaution);
        _audio.PlayPvs(SplintBreakSound, args.Body);
    }

    private void OnInternalRip(ref HumanInternalRipEvent args)
    {
        _popup.PopupEntity(
            Loc.GetString(
                "cmu-medical-internal-rip",
                ("part", GetRegionName(args.Region))),
            args.Body,
            args.Body,
            PopupType.LargeCaution);
    }

    private void OnWoundWidened(ref HumanWoundWidenedEvent args)
    {
        _popup.PopupEntity(
            Loc.GetString(
                "cmu-medical-wound-widened",
                ("part", GetRegionName(args.Region))),
            args.Body,
            args.Body,
            PopupType.MediumCaution);

        if (!_random.Prob(0.25f))
            return;

        _popup.PopupEntity(
            Loc.GetString(
                "cmu-medical-wound-widened-audible",
                ("part", GetRegionName(args.Region))),
            args.Body,
            args.Body,
            PopupType.MediumCaution);
    }

    private string GetRegionName(BodyRegion region)
    {
        return Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(region));
    }
}
