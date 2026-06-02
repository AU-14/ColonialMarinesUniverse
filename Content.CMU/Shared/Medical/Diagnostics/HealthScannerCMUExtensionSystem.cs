using System.Collections.Generic;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Shrapnel;
using Content.Shared._CMU14.Medical.Stabilizers;
using Content.Shared._CMU14.Medical.StatusEffects;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Scanner;
using Content.Shared._RMC14.Medical.Wounds;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Diagnostics;

public sealed partial class HealthScannerCMUExtensionSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPainShockSystem _pain = default!;
    [Dependency] private SharedCMUTraumaGovernorSystem _traumaGovernor = default!;
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

        state.CMUTraumaGovernor = _traumaGovernor.GetReadout(args.Patient);
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
        foreach (var (partUid, partComp) in _body.GetBodyChildren(patient))
        {
            if (!TryComp<BodyPart.BodyPartHealthComponent>(partUid, out var ph))
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
                ? WorstUntreatedWoundReadout(pw)
                : null;
            parts[dictKey] = new CMUBodyPartReadout(
                partComp.PartType,
                partComp.Symmetry,
                ph.Current,
                ph.Max,
                woundReadout?.Size,
                woundReadout?.Mechanism,
                shrapnel?.Fragments ?? 0,
                shrapnel?.Severity ?? 0f,
                HasComp<CMUEscharComponent>(partUid),
                HasComp<CMUSplintedComponent>(partUid),
                HasComp<CMUCastComponent>(partUid),
                HasComp<CMUTourniquetComponent>(partUid));
        }
        state.CMUParts = parts;
    }

    private static (WoundSize Size, WoundMechanism Mechanism)? WorstUntreatedWoundReadout(BodyPartWoundComponent wounds)
    {
        (WoundSize Size, WoundMechanism Mechanism)? worst = null;
        for (var i = 0; i < wounds.Wounds.Count; i++)
        {
            var wound = wounds.Wounds[i];
            if (wound.Treated)
                continue;

            var size = i < wounds.Sizes.Count ? wounds.Sizes[i] : WoundSize.Deep;
            if (worst is not null && (byte)size <= (byte)worst.Value.Size)
                continue;

            var mechanism = i < wounds.Mechanisms.Count
                ? wounds.Mechanisms[i]
                : LegacyMechanismFor(wound.Type);
            worst = (size, mechanism);
        }

        return worst;
    }

    private static WoundMechanism LegacyMechanismFor(WoundType type) => type switch
    {
        WoundType.Burn => WoundMechanism.Burn,
        WoundType.Surgery => WoundMechanism.Surgical,
        _ => WoundMechanism.Generic,
    };

    private static BodyPartType ToDictKey(BodyPartType type, BodyPartSymmetry sym)
    {
        return (BodyPartType)((int)type | ((int)sym << 8));
    }

    private void FillOrgans(EntityUid patient, HealthScannerBuiState state)
    {
        var organs = new List<CMUOrganReadout>();

        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (!TryComp<OrganHealthComponent>(organ.Id, out var oh))
                continue;
            organs.Add(new CMUOrganReadout(
                OrganName(organ.Id),
                oh.Stage,
                oh.Current,
                oh.Max,
                Removed: false));
        }

        // A slot whose container has no attached organ entity means the organ
        // was extracted (or never present) — emit a Removed row keyed by the
        // slot id so the medic sees the missing organ instead of inferring it
        // from a shorter list.
        foreach (var (partUid, partComp) in _body.GetBodyChildren(patient))
        {
            foreach (var (slotId, _) in partComp.Organs)
            {
                var containerId = SharedBodySystem.OrganSlotContainerIdPrefix + slotId;
                if (!_containers.TryGetContainer(partUid, containerId, out var container))
                    continue;
                if (container.ContainedEntities.Count > 0)
                    continue;
                organs.Add(new CMUOrganReadout(
                    slotId,
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
        foreach (var (partUid, partComp) in _body.GetBodyChildren(patient))
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
        foreach (var (partUid, partComp) in _body.GetBodyChildren(patient))
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

        foreach (var (partUid, _) in _body.GetBodyChildren(patient))
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
        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (!TryComp<HeartComponent>(organ.Id, out var heart))
                continue;
            state.CMUHeartBpm = heart.BeatsPerMinute;
            state.CMUHeartStopped = heart.Stopped;
            return;
        }
    }

    private string OrganName(EntityUid organ)
    {
        var meta = MetaData(organ);
        return meta.EntityPrototype is { } proto ? proto.ID : "organ";
    }
}
