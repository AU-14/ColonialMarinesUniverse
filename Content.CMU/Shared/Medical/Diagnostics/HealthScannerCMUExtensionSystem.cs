using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared._CMU14.Medical.Injuries.Shrapnel;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Diagnostics;

public sealed partial class HealthScannerCMUExtensionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;
    [Dependency] private SkillsSystem _skills = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";

    public override void Initialize()
    {
        base.Initialize();
        // RMC's UpdateUI raises the event directed on the scanner entity, but
        // tests synthesise the raise on the patient body. Anchor on both — the
        // handler is idempotent against the state object.
        SubscribeLocalEvent<HealthScannerComponent, HealthScannerBuildStateEvent>(OnBuildScanner);
        SubscribeLocalEvent<CMUHumanMedicalComponent, HealthScannerBuildStateEvent>(OnBuildPatient);
    }

    private void OnBuildScanner(Entity<HealthScannerComponent> ent, ref HealthScannerBuildStateEvent args)
        => HandleBuildState(ref args);

    private void OnBuildPatient(Entity<CMUHumanMedicalComponent> ent, ref HealthScannerBuildStateEvent args)
        => HandleBuildState(ref args);

    private void HandleBuildState(ref HealthScannerBuildStateEvent args)
    {
        if (!_cfg.GetCVar(CMUMedicalCCVars.Enabled))
            return;
        if (!_cfg.GetCVar(CMUMedicalCCVars.DiagnosticsEnabled))
            return;
        if (!HasComp<CMUHumanMedicalComponent>(args.Patient))
            return;

        var skill = 0;
        if (args.Examiner is { } examiner)
        {
            skill = HasComp<BypassSkillChecksComponent>(examiner)
                ? int.MaxValue
                : _skills.GetSkill(examiner, MedicalSkill);
        }
        var state = args.State;

        FillBodyParts(args.Patient, state);
        if (skill >= 2)
        {
            state.CMUSyntheticPhysiology = HasComp<SynthComponent>(args.Patient);
            FillOrgans(args.Patient, state);
        }
        if (skill >= 1)
            FillFractures(args.Patient, state, exactSeverity: skill >= 3);
        if (skill >= 1)
            FillInternalBleeds(args.Patient, state, exactLocation: skill >= 3);
        FillHeart(args.Patient, state);
        if (skill >= 1)
            FillPainShockRisk(args.Patient, state);
    }

    private void FillPainShockRisk(EntityUid patient, HealthScannerBuiState state)
    {
        if (!TryComp<PainShockComponent>(patient, out var pain))
            return;

        state.CMUPainShockRisk = PainShockRiskFromTier(_pain.GetEffectiveTier(patient, pain));
        state.CMUPainShockSuppressed = _pain.IsPainRiskSuppressed(patient, pain);
    }

    private static CMUPainShockRisk PainShockRiskFromTier(PainTier tier) => tier switch
    {
        PainTier.Mild => CMUPainShockRisk.Elevated,
        PainTier.Moderate => CMUPainShockRisk.High,
        PainTier.Severe => CMUPainShockRisk.Imminent,
        PainTier.Shock => CMUPainShockRisk.Active,
        _ => CMUPainShockRisk.Low,
    };

    private void FillBodyParts(EntityUid patient, HealthScannerBuiState state)
    {
        var parts = new Dictionary<BodyPartType, CMUBodyPartReadout>();
        var seen = new HashSet<(BodyPartType, BodyPartSymmetry)>();
        foreach (var (partUid, partComp) in _medicalIndex.GetBodyParts(patient))
        {
            if (!TryComp<BodyPartHealthComponent>(partUid, out var ph))
                continue;

            var key = (partComp.PartType, partComp.Symmetry);
            if (!seen.Add(key))
                continue;

            // BodyPartType is the dictionary key on the wire; with mixed
            // symmetry that means collisions on Arm/Hand/Leg/Foot. Encode the
            // symmetry into the dictionary key by offsetting the enum value
            // when symmetric.
            var dictKey = ToDictKey(partComp.PartType, partComp.Symmetry);
            TryComp<CMUShrapnelComponent>(partUid, out var shrapnel);
            var woundReadout = TryComp<BodyPartWoundComponent>(partUid, out var pw)
                ? _woundLedger.GetWorstUntreatedWound(pw)
                : null;
            parts[dictKey] = new CMUBodyPartReadout(
                partComp.PartType,
                partComp.Symmetry,
                ph.Current,
                ph.Max,
                woundReadout?.Size,
                woundReadout?.Mechanism,
                woundReadout?.Damage ?? FixedPoint2.Zero,
                shrapnel?.Fragments ?? 0,
                shrapnel?.Severity ?? 0f,
                HasComp<CMUEscharComponent>(partUid),
                HasComp<CMUSplintedComponent>(partUid),
                HasComp<CMUCastComponent>(partUid),
                HasComp<CMUTourniquetComponent>(partUid));
        }
        state.CMUParts = parts;
    }

    private static BodyPartType ToDictKey(BodyPartType type, BodyPartSymmetry sym)
    {
        return (BodyPartType)((int)type | ((int)sym << 8));
    }

    private void FillOrgans(EntityUid patient, HealthScannerBuiState state)
    {
        var organs = new List<CMUOrganReadout>();

        foreach (var organ in _medicalIndex.GetOrgans(patient))
        {
            if (!TryComp<OrganHealthComponent>(organ.Owner, out var oh))
                continue;
            organs.Add(new CMUOrganReadout(
                OrganName(organ.Owner),
                oh.Stage,
                oh.Current,
                oh.Max,
                Removed: false));
        }

        // A slot whose container has no attached organ entity means the organ
        // was extracted (or never present) — emit a Removed row keyed by the
        // slot id so the medic sees the missing organ instead of inferring it
        // from a shorter list.
        foreach (var (partUid, partComp) in _medicalIndex.GetBodyParts(patient))
        {
            foreach (var slot in _medicalIndex.GetOrganSlots(partUid))
            {
                if (slot.Organ is not null)
                    continue;
                organs.Add(new CMUOrganReadout(
                    slot.SlotId,
                    OrganDamageStage.Dead,
                    FixedPoint2.Zero,
                    FixedPoint2.Zero,
                    Removed: true));
            }
        }

        state.CMUOrgans = organs;
    }

    private void FillFractures(EntityUid patient, HealthScannerBuiState state, bool exactSeverity)
    {
        var fractures = new List<CMUFractureReadout>();
        foreach (var (partUid, partComp) in _medicalIndex.GetBodyParts(patient))
        {
            if (!TryComp<FractureComponent>(partUid, out var frac))
                continue;
            fractures.Add(new CMUFractureReadout(
                partComp.PartType,
                partComp.Symmetry,
                Severity: frac.Severity,
                ExactSeverity: exactSeverity,
                Suppressed: HasComp<CMUSplintedComponent>(partUid) || HasComp<CMUCastComponent>(partUid)));
        }
        state.CMUFractures = fractures;
    }

    private void FillInternalBleeds(EntityUid patient, HealthScannerBuiState state, bool exactLocation)
    {
        var bleeds = new List<CMUInternalBleedReadout>();
        foreach (var (partUid, partComp) in _medicalIndex.GetBodyParts(patient))
        {
            if (!TryComp<InternalBleedingComponent>(partUid, out var ib))
                continue;
            bleeds.Add(new CMUInternalBleedReadout(
                partComp.PartType,
                partComp.Symmetry,
                exactLocation,
                ib.BloodlossPerSecond));
        }
        state.CMUInternalBleeds = bleeds;

        foreach (var (partUid, _) in _medicalIndex.GetBodyParts(patient))
        {
            if (!TryComp<BodyPartWoundComponent>(partUid, out var pw))
                continue;
            if (pw.ExternalBleeding == ExternalBleedTier.None)
                continue;

            state.CMUExternalBleeding = true;
            return;
        }
    }

    private void FillHeart(EntityUid patient, HealthScannerBuiState state)
    {
        if (!_medicalIndex.TryGetOrgan<HeartComponent>(patient, out var organ) ||
            !TryComp<HeartComponent>(organ, out var heart))
            return;

        state.CMUHeartBpm = heart.BeatsPerMinute;
        state.CMUHeartStopped = heart.Stopped;
    }

    private string OrganName(EntityUid organ)
    {
        var meta = MetaData(organ);
        return meta.EntityPrototype is { } proto ? proto.ID : "organ";
    }
}
