using System.Linq;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Diagnostics;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Organs.Lungs;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Diagnostics;

public sealed class CMUStethoscopeSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";

    public enum StethoscopeAudioCue : byte { Strong, Weak, Fast, Flatline }

    public override void Initialize()
    {
        base.Initialize();
        // <CMUHumanMedicalComponent, AfterInteractEvent> slot is owned by
        // CMUMedicInteractHubSystem — HandleAfterInteract is the dispatch
        // target. We still own the DoAfter completion event.
        SubscribeLocalEvent<CMUHumanMedicalComponent, CMUStethoscopeDoAfterEvent>(OnDoAfter);
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled);
    }

    public void HandleAfterInteract(Entity<CMUHumanMedicalComponent> medic, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;
        var used = args.Used;
        if (!HasComp<RMCStethoscopeComponent>(used))
            return;
        if (!IsLayerEnabled())
            return;
        if (!HasComp<CMUHumanMedicalComponent>(target))
            return;
        if (_skills.GetSkill(args.User, MedicalSkill) < 1)
            return;

        var skillMult = _skills.GetSkillDelayMultiplier(args.User, MedicalSkill);
        var ev = new CMUStethoscopeDoAfterEvent();
        var doAfter = new DoAfterArgs(EntityManager, args.User, TimeSpan.FromSeconds(2) * skillMult,
            ev, args.User, target: target, used: used)
        {
            BreakOnMove = true,
            BlockDuplicate = true,
        };
        _doAfter.TryStartDoAfter(doAfter);
        args.Handled = true;
    }

    private void OnDoAfter(Entity<CMUHumanMedicalComponent> medic, ref CMUStethoscopeDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } patient)
            return;

        var (cue, popup) = ReadStethoscope(args.User, patient);
        _popup.PopupClient(popup, patient, args.User);
    }

    public (StethoscopeAudioCue Cue, string Popup) ReadStethoscope(EntityUid user, EntityUid patient)
    {
        var skill = _skills.GetSkill(user, MedicalSkill);

        var heart = TryGetHeart(patient);
        var lungs = TryGetLungs(patient);

        var cue = StethoscopeAudioCue.Strong;
        string pulseStr;
        if (heart is null)
        {
            cue = StethoscopeAudioCue.Flatline;
            pulseStr = Loc.GetString("cmu-medical-stethoscope-no-heart");
        }
        else if (heart.Stopped)
        {
            cue = StethoscopeAudioCue.Flatline;
            pulseStr = Loc.GetString("cmu-medical-stethoscope-no-pulse");
        }
        else if (heart.BeatsPerMinute < 50)
        {
            cue = StethoscopeAudioCue.Weak;
            pulseStr = skill >= 2
                ? Loc.GetString("cmu-medical-stethoscope-pulse", ("bpm", heart.BeatsPerMinute))
                : Loc.GetString("cmu-medical-stethoscope-pulse-qualitative", ("description", "slow"));
        }
        else if (heart.BeatsPerMinute > 130)
        {
            cue = StethoscopeAudioCue.Fast;
            pulseStr = skill >= 2
                ? Loc.GetString("cmu-medical-stethoscope-pulse", ("bpm", heart.BeatsPerMinute))
                : Loc.GetString("cmu-medical-stethoscope-pulse-qualitative", ("description", "racing"));
        }
        else
        {
            cue = StethoscopeAudioCue.Strong;
            pulseStr = skill >= 2
                ? Loc.GetString("cmu-medical-stethoscope-pulse", ("bpm", heart.BeatsPerMinute))
                : Loc.GetString("cmu-medical-stethoscope-pulse-qualitative", ("description", "steady"));
        }

        string lungStr;
        if (lungs is null)
            lungStr = Loc.GetString("cmu-medical-stethoscope-no-lungs");
        else if (skill >= 2)
            lungStr = Loc.GetString("cmu-medical-stethoscope-lungs-precise",
                ("stage", $"{lungs.Efficiency:F2}"));
        else
            lungStr = Loc.GetString("cmu-medical-stethoscope-lungs-qualitative",
                ("description", QualitativeLungs(lungs)));

        var painStr = string.Empty;
        if (skill >= 2 && TryComp<PainShockComponent>(patient, out var pain))
        {
            painStr = pain.Tier switch
            {
                PainTier.Mild => Loc.GetString("cmu-medical-stethoscope-pain-mild"),
                PainTier.Moderate => Loc.GetString("cmu-medical-stethoscope-pain-moderate"),
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

    private HeartComponent? TryGetHeart(EntityUid body)
    {
        foreach (var organ in _body.GetBodyOrgans(body))
        {
            if (TryComp<HeartComponent>(organ.Id, out var heart))
                return heart;
        }
        return null;
    }

    private LungsComponent? TryGetLungs(EntityUid body)
    {
        // Pair-survival: take the best (highest efficiency) lung.
        LungsComponent? best = null;
        foreach (var organ in _body.GetBodyOrgans(body))
        {
            if (!TryComp<LungsComponent>(organ.Id, out var lungs))
                continue;
            if (best is null || lungs.Efficiency > best.Efficiency)
                best = lungs;
        }
        return best;
    }

    private static string QualitativeLungs(LungsComponent l) => l.Efficiency switch
    {
        >= 0.85f => "clear",
        >= 0.5f => "wet",
        _ => "faint",
    };
}
