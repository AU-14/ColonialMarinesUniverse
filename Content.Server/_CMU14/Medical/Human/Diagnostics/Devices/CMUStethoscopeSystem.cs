using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared._RMC14.Synth;
using Content.Shared.DoAfter;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Human.Diagnostics;

public sealed partial class CMUStethoscopeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";

    public enum StethoscopeAudioCue : byte { Strong, Weak, Fast, Flatline }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RMCStethoscopeComponent, CMUStethoscopeLedgerAttemptEvent>(OnLedgerAttempt);
        SubscribeLocalEvent<RMCStethoscopeComponent, CMUStethoscopeDoAfterEvent>(OnDoAfter);
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled);
    }

    private void OnLedgerAttempt(Entity<RMCStethoscopeComponent> ent, ref CMUStethoscopeLedgerAttemptEvent args)
    {
        if (args.Handled)
            return;

        if (!IsLayerEnabled())
            return;

        if (!HasComp<HumanMedicalComponent>(args.Target))
            return;

        if (_skills.GetSkill(args.User, MedicalSkill) < 1)
            return;

        var skillMult = _skills.GetSkillDelayMultiplier(args.User, MedicalSkill);
        var ev = new CMUStethoscopeDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2) * skillMult,
            ev, ent.Owner, target: args.Target, used: ent.Owner)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnDoAfter(Entity<RMCStethoscopeComponent> ent, ref CMUStethoscopeDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } patient)
            return;

        var (_, popup) = ReadStethoscope(args.User, patient);
        _popup.PopupClient(popup, patient, args.User);
    }

    public (StethoscopeAudioCue Cue, string Popup) ReadStethoscope(EntityUid user, EntityUid patient)
    {
        if (HasComp<SynthComponent>(patient))
            return (StethoscopeAudioCue.Flatline, Loc.GetString("rmc-stethoscope-synth"));

        var skill = _skills.GetSkill(user, MedicalSkill);
        if (!TryComp<HumanMedicalComponent>(patient, out var medical))
        {
            return (
                StethoscopeAudioCue.Flatline,
                $"{Loc.GetString("cmu-medical-stethoscope-no-heart")}\n{Loc.GetString("cmu-medical-stethoscope-no-lungs")}");
        }

        var heart = medical.Organs[(int) OrganSlot.Heart];
        var heartReadout = ReadHeart(heart);
        var cue = heartReadout.Cue;
        var pulseStr = PulseText(heartReadout, skill);

        var lungEfficiency = GetBestLungEfficiency(medical);
        var lungStr = lungEfficiency is null
            ? Loc.GetString("cmu-medical-stethoscope-no-lungs")
            : skill >= 2
                ? Loc.GetString("cmu-medical-stethoscope-lungs-precise", ("stage", $"{lungEfficiency.Value:F2}"))
                : Loc.GetString(
                    "cmu-medical-stethoscope-lungs-qualitative",
                    ("description", QualitativeLungs(lungEfficiency.Value)));

        var painStr = string.Empty;
        if (skill >= 2 && TryComp<PainShockComponent>(patient, out var pain))
        {
            painStr = _pain.GetEffectiveTier(patient, pain) switch
            {
                PainTier.Mild => Loc.GetString("cmu-medical-stethoscope-pain-mild"),
                PainTier.Discomforting => Loc.GetString("cmu-medical-stethoscope-pain-moderate"),
                PainTier.Moderate => Loc.GetString("cmu-medical-stethoscope-pain-moderate"),
                PainTier.Distressing => Loc.GetString("cmu-medical-stethoscope-pain-severe"),
                PainTier.Severe => Loc.GetString("cmu-medical-stethoscope-pain-severe"),
                PainTier.Shock => Loc.GetString("cmu-medical-stethoscope-pain-shock"),
                _ => string.Empty,
            };
        }

        var combined = string.IsNullOrEmpty(painStr)
            ? $"{pulseStr}\n{lungStr}"
            : $"{pulseStr}\n{lungStr}\n{painStr}";
        return (cue, combined);
    }

    public StethoscopeAudioCue ReadCueOnly(EntityUid user, EntityUid patient)
        => ReadStethoscope(user, patient).Cue;

    private HeartReadout ReadHeart(OrganState heart)
    {
        if (heart.Missing)
            return new HeartReadout(
                StethoscopeAudioCue.Flatline,
                null,
                "cmu-medical-stethoscope-no-heart",
                null);

        return heart.Status switch
        {
            OrganDamageStatus.Broken => new HeartReadout(
                StethoscopeAudioCue.Flatline,
                0,
                "cmu-medical-stethoscope-no-pulse",
                null),
            OrganDamageStatus.Bruised => new HeartReadout(
                StethoscopeAudioCue.Weak,
                45,
                "cmu-medical-stethoscope-pulse",
                "slow"),
            OrganDamageStatus.LittleBruised => new HeartReadout(
                StethoscopeAudioCue.Fast,
                115,
                "cmu-medical-stethoscope-pulse",
                "racing"),
            _ => new HeartReadout(
                StethoscopeAudioCue.Strong,
                80,
                "cmu-medical-stethoscope-pulse",
                "steady"),
        };
    }

    private static float? GetBestLungEfficiency(HumanMedicalComponent medical)
    {
        var left = LungEfficiency(medical.Organs[(int) OrganSlot.LeftLung]);
        var right = LungEfficiency(medical.Organs[(int) OrganSlot.RightLung]);

        if (left is null)
            return right;

        if (right is null)
            return left;

        return MathF.Max(left.Value, right.Value);
    }

    private static float? LungEfficiency(OrganState lung)
    {
        if (lung.Missing)
            return null;

        return lung.Status switch
        {
            OrganDamageStatus.LittleBruised => 0.75f,
            OrganDamageStatus.Bruised => 0.50f,
            OrganDamageStatus.Broken => 0.25f,
            _ => 1.00f,
        };
    }

    private static string QualitativeLungs(float efficiency) => efficiency switch
    {
        >= 0.85f => "clear",
        >= 0.5f => "wet",
        _ => "faint",
    };

    private string PulseText(HeartReadout readout, int skill)
    {
        if (readout.BeatsPerMinute is not { } bpm)
            return Loc.GetString(readout.PreciseLocale);

        if (skill >= 2)
            return Loc.GetString(readout.PreciseLocale, ("bpm", bpm));

        return Loc.GetString(
            "cmu-medical-stethoscope-pulse-qualitative",
            ("description", readout.QualitativeDescription ?? "steady"));
    }

    private readonly record struct HeartReadout(
        StethoscopeAudioCue Cue,
        int? BeatsPerMinute,
        string PreciseLocale,
        string? QualitativeDescription);
}
