using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Diagnostics;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Lungs;
using Content.Shared.CMU14.Medical.Injuries.Pain;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body;
using Content.Shared.Body.Part;
using Content.Shared.DoAfter;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.Medical.Diagnostics;

public sealed partial class CMUStethoscopeSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedLungsSystem _lungs = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private RMCStethoscopeSystem _stethoscope = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";
    private ulong _nextAttempt;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RMCStethoscopeExamineRequest>(OnExamineRequest);
        SubscribeLocalEvent<CMUStethoscopeExaminationComponent, CMUStethoscopeDoAfterEvent>(OnDoAfter);
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled);
    }

    private void OnExamineRequest(ref RMCStethoscopeExamineRequest args)
    {
        if (args.Handled || !IsLayerEnabled() ||
            !TryComp<CMUHumanMedicalComponent>(args.Patient, out var medical))
            return;

        // Rejection is also a handled CMU examination, never a fallback scan.
        args.Handled = true;
        if (_skills.GetSkill(args.User, MedicalSkill) < 1)
        {
            if (_stethoscope.CanExamine(args.User, args.Patient, args.Tool, args.FromVerb))
            {
                var denied = new FormattedMessage();
                denied.AddText(Loc.GetString("rmc-stethoscope-unskilled"));
                _stethoscope.ShowResult(args.User, args.Patient, denied, args.FromVerb);
            }
            return;
        }

        if (TryComp<CMUStethoscopeExaminationComponent>(args.User, out var previous))
        {
            // Direct replacement of the generic DoAfter component discards its
            // callbacks. Reclaim only that orphan before allowing a new scan.
            if (previous.DoAfter == null || IsSame(args.User, previous.DoAfter))
                return;
            ClearExamination(args.User, previous);
        }

        if (!TryComp<BodyComponent>(args.Patient, out var body) ||
            !TryComp<TransformComponent>(args.Patient, out var patientTransform) ||
            !TryComp<TransformComponent>(args.User, out var medicTransform) ||
            !TryComp<SkillsComponent>(args.User, out var skills) ||
            !TryComp<DoAfterComponent>(args.User, out var doAfterComponent) ||
            _skills.GetSkill((args.User, skills), MedicalSkill) < 1)
            return;

        var context = AddComp<CMUStethoscopeExaminationComponent>(args.User);
        context.Attempt = ++_nextAttempt;
        context.Patient = args.Patient;
        context.Tool = args.Tool;
        context.Medical = medical;
        context.Body = body;
        context.PatientTransform = patientTransform;
        context.MedicTransform = medicTransform;
        context.Skills = skills;
        context.DoAfter = doAfterComponent;
        context.Skill = _skills.GetSkill((args.User, skills), MedicalSkill);
        context.FromVerb = args.FromVerb;
        var medic = args.User;
        var delay = TimeSpan.FromSeconds(2) * _skills.GetSkillDelayMultiplier((medic, skills), MedicalSkill);
        var doAfter = new DoAfterArgs(EntityManager, medic, delay,
            new CMUStethoscopeDoAfterEvent(context.Attempt), medic, target: context.Patient, used: context.Tool)
        {
            BreakOnMove = true,
            NeedHand = !context.FromVerb,
            BlockDuplicate = true,
            AttemptFrequency = AttemptFrequency.EveryTick,
            ExtraCheck = () => IsCurrent(medic, context),
        };
        if (!_doAfter.TryStartDoAfter(doAfter))
            ClearExamination(medic, context);
    }

    private void OnDoAfter(Entity<CMUStethoscopeExaminationComponent> medic, ref CMUStethoscopeDoAfterEvent args)
    {
        if (args.Handled || args.Attempt != medic.Comp.Attempt)
            return;
        args.Handled = true;
        var context = medic.Comp;
        if (args.Cancelled || args.User != medic.Owner || args.Target != context.Patient ||
            args.Used != context.Tool.Owner || !IsCurrent(medic, context))
        {
            ClearExamination(medic, context);
            return;
        }

        // Pain is integrated at the observation time. Its public physiology
        // callbacks can invalidate this operation, so check the snapshot again.
        _pain.SettlePainBeforeModifierChange(context.Patient);
        if (IsCurrent(medic, context))
        {
            var result = new FormattedMessage();
            result.AddText(ReadStethoscope(medic, context.Patient));
            _stethoscope.ShowResult(medic, context.Patient, result, context.FromVerb);
        }
        ClearExamination(medic, context);
    }

    private void ClearExamination(EntityUid medic, CMUStethoscopeExaminationComponent context)
    {
        if (TryComp<CMUStethoscopeExaminationComponent>(medic, out var current) && ReferenceEquals(current, context))
            RemComp<CMUStethoscopeExaminationComponent>(medic);
    }

    private bool IsCurrent(EntityUid medic, CMUStethoscopeExaminationComponent context)
    {
        return IsIdentityCurrent(medic, context) &&
               _stethoscope.CanExamine(medic, context.Patient, context.Tool, context.FromVerb) &&
               IsIdentityCurrent(medic, context);
    }

    private bool IsIdentityCurrent(EntityUid medic, CMUStethoscopeExaminationComponent context)
    {
        return IsLayerEnabled() && IsLive(medic) && IsLive(context.Patient) &&
               IsSame(medic, context) && IsSame(medic, context.MedicTransform) &&
               context.DoAfter != null && IsSame(medic, context.DoAfter) &&
               IsSame(medic, context.Skills) && _skills.GetSkill((medic, context.Skills), MedicalSkill) == context.Skill &&
               context.Skill >= 1 && IsSame(context.Patient, context.Medical) &&
               IsSame(context.Patient, context.Body) && IsSame(context.Patient, context.PatientTransform);
    }

    private bool IsLive(EntityUid uid) => !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid);

    private bool IsSame<T>(EntityUid uid, T expected) where T : Component
        => expected.LifeStage < ComponentLifeStage.Stopping && TryComp<T>(uid, out var current) && ReferenceEquals(current, expected);

    /// <summary>Formats the authoritative organ and pain projections without aggregate-damage inference.</summary>
    public string ReadStethoscope(EntityUid user, EntityUid patient)
    {
        var skill = _skills.GetSkill(user, MedicalSkill);
        var heart = TryGetHeart(patient);
        var hasLungs = _lungs.TryGetRespiratoryCapacity(patient, out var lungs) && IsAttachedOrgan(patient, lungs.Organ);
        string pulse;
        if (heart is null)
            pulse = Loc.GetString("cmu-medical-stethoscope-no-heart");
        else if (heart.Stopped)
            pulse = Loc.GetString("cmu-medical-stethoscope-no-pulse");
        else if (skill >= 2)
            pulse = Loc.GetString("cmu-medical-stethoscope-pulse", ("bpm", heart.BeatsPerMinute));
        else
            pulse = Loc.GetString("cmu-medical-stethoscope-pulse-qualitative",
                ("description", heart.BeatsPerMinute < 50 ? "slow" : heart.BeatsPerMinute > 130 ? "racing" : "steady"));

        string breathing;
        if (!hasLungs)
            breathing = Loc.GetString("cmu-medical-stethoscope-no-lungs");
        else if (skill >= 2)
            breathing = Loc.GetString("cmu-medical-stethoscope-lungs-precise", ("stage", $"{lungs.Efficiency:F2}"));
        else
            breathing = Loc.GetString("cmu-medical-stethoscope-lungs-qualitative",
                ("description", QualitativeLungs(lungs.Efficiency)));

        var painText = string.Empty;
        if (skill >= 2 && TryComp<PainShockComponent>(patient, out var pain))
        {
            painText = _pain.GetEffectiveTier(patient, pain) switch
            {
                PainTier.Mild => Loc.GetString("cmu-medical-stethoscope-pain-mild"),
                PainTier.Moderate => Loc.GetString("cmu-medical-stethoscope-pain-moderate"),
                PainTier.Severe => Loc.GetString("cmu-medical-stethoscope-pain-severe"),
                PainTier.Shock => Loc.GetString("cmu-medical-stethoscope-pain-shock"),
                _ => string.Empty,
            };
        }
        return string.IsNullOrEmpty(painText) ? $"{pulse}\n{breathing}" : $"{pulse}\n{breathing}\n{painText}";
    }

    private HeartComponent? TryGetHeart(EntityUid body)
    {
        foreach (var organ in _medicalIndex.GetOrgans(body))
        {
            if (IsAttachedOrgan(body, organ) && TryComp<HeartComponent>(organ, out var heart) &&
                heart.LifeStage < ComponentLifeStage.Stopping)
                return heart;
        }
        return null;
    }

    private bool IsAttachedOrgan(EntityUid body, EntityUid organ)
    {
        return IsLive(organ) && TryComp<OrganComponent>(organ, out var anatomy) && anatomy.Body == body &&
               _medicalIndex.TryGetOrganPart(organ, out var part) && IsLive(part) &&
               TryComp<BodyPartComponent>(part, out var partAnatomy) && partAnatomy.Body == body;
    }

    private static string QualitativeLungs(float efficiency) => efficiency switch
    {
        >= 0.85f => "clear",
        >= 0.5f => "wet",
        _ => "faint",
    };
}
