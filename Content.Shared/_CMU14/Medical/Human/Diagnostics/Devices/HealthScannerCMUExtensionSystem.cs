using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Organs.Heart;
using Content.Shared._CMU14.Medical.Human.Effects;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared.Body.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.Medical.Human.Diagnostics;

public sealed partial class HealthScannerCMUExtensionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedBodySystem _body = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";

    public override void Initialize()
    {
        base.Initialize();

        // RMC's UpdateUI raises the event directed on the scanner entity, but
        // tests synthesise the raise on the patient body. Anchor on both; the
        // handler is idempotent against the state object.
        SubscribeLocalEvent<HealthScannerComponent, HealthScannerBuildStateEvent>(OnBuildScanner);
        SubscribeLocalEvent<HumanMedicalComponent, HealthScannerBuildStateEvent>(OnBuildPatient);
    }

    private void OnBuildScanner(Entity<HealthScannerComponent> ent, ref HealthScannerBuildStateEvent args)
    {
        HandleBuildState(ref args);
    }

    private void OnBuildPatient(Entity<HumanMedicalComponent> ent, ref HealthScannerBuildStateEvent args)
    {
        HandleBuildState(ref args);
    }

    private void HandleBuildState(ref HealthScannerBuildStateEvent args)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return;
        if (!_cfg.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled))
            return;
        if (!TryComp<HumanMedicalComponent>(args.Patient, out var humanMedical))
            return;

        HumanMedicalScannerBuiSystem.FillHealthScannerState(
            humanMedical,
            args.State,
            includeFullLedger: GetMedicalSkill(args.Examiner) >= 1);

        FillCmuVitals(args.Patient, args.State);
    }

    private void FillCmuVitals(EntityUid patient, HealthScannerBuiState state)
    {
        state.CMUPulseBpm = null;
        state.CMUNoPulse = false;
        state.CMUShockRisk = HealthScannerShockRisk.Unknown;
        state.CMUShockRiskPercent = 0;

        if (TryComp<MissingHeartComponent>(patient, out _))
        {
            state.CMUPulseBpm = 0;
            state.CMUNoPulse = true;
        }
        else
        {
            foreach (var heart in _body.GetBodyOrganEntityComps<HeartComponent>(patient))
            {
                state.CMUPulseBpm = Math.Max(0, heart.Comp1.BeatsPerMinute);
                state.CMUNoPulse = heart.Comp1.Stopped || heart.Comp1.BeatsPerMinute <= 0;
                break;
            }
        }

        if (!TryComp<PainShockComponent>(patient, out var pain))
            return;

        var threshold = _pain.ShockThreshold;
        if (threshold <= 0)
            return;

        var rawRisk = Math.Clamp(pain.Pain.Float() / threshold.Float(), 0f, 1.5f);
        state.CMUShockRiskPercent = Math.Clamp((int) MathF.Round(rawRisk * 100f), 0, 150);
        var effectiveTier = _pain.GetEffectiveTier(patient, pain);
        state.CMUShockRisk = GetShockRisk(rawRisk, effectiveTier);

        if (rawRisk >= 0.5f &&
            effectiveTier < PainTier.Shock &&
            _pain.IsPainRiskSuppressed(patient, pain))
        {
            state.CMUShockRisk = HealthScannerShockRisk.Suppressed;
        }
    }

    private static HealthScannerShockRisk GetShockRisk(float rawRisk, PainTier effectiveTier)
    {
        if (effectiveTier == PainTier.Shock || rawRisk >= 1f)
            return HealthScannerShockRisk.Shock;
        if (rawRisk >= 0.8f)
            return HealthScannerShockRisk.High;
        if (rawRisk >= 0.5f)
            return HealthScannerShockRisk.Elevated;
        if (rawRisk >= 0.15f)
            return HealthScannerShockRisk.Low;

        return HealthScannerShockRisk.None;
    }

    private int GetMedicalSkill(EntityUid? examiner)
    {
        if (examiner is not { } uid)
            return 0;

        return HasComp<BypassSkillChecksComponent>(uid)
            ? int.MaxValue
            : _skills.GetSkill(uid, MedicalSkill);
    }
}
