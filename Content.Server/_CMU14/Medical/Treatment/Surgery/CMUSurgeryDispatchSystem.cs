using System;
using System.Collections.Generic;
using Content.Server.Popups;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgeryDispatchSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _flowSurgery = default!;
    [Dependency] private CMUSurgeryRulebookSystem _rulebook = default!;
    [Dependency] private CMUSurgerySessionSystem _sessions = default!;

    private ulong _lastViewRevision;

    public override void Initialize()
    {
        base.Initialize();

        // RMC's CMSurgerySystem owns the directed tool interact slot. RMC's
        // handler calls TryDispatch directly so CMU surgery can win the click.
        Subs.BuiEvents<CMUSurgeryWindowOpenComponent>(CMUSurgeryUIKey.Key, subs =>
        {
            subs.Event<CMUSurgeryArmStepMessage>(OnArmStepMessage);
            subs.Event<CMUSurgeryClearArmedMessage>(OnClearArmedMessage);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
        });
    }

    public void RefreshUiForPatient(EntityUid patient)
    {
        var query = EntityQueryEnumerator<CMUSurgeryWindowOpenComponent>();
        while (query.MoveNext(out var medic, out var marker))
        {
            if (marker.Patient != patient)
                continue;

            var parts = BuildPartEntries(patient, medic);
            var armed = CompOrNull<CMUSurgeryArmedStepComponent>(patient);
            var state = BuildBuiStateForViewer(patient, medic, marker, parts, armed);
            _ui.SetUiState(medic, CMUSurgeryUIKey.Key, state);
        }
    }

    public bool TryDispatch(EntityUid surgeon, EntityUid patient, EntityUid? tool = null)
    {
        if (!IsLayerEnabled())
            return false;

        if (!IsCmuOrganicSurgeryPatient(patient))
            return false;

        if (!_flowSurgery.CanOperateOnPatient(patient, surgeon, popup: true))
            return true;

        var parts = BuildPartEntries(patient, surgeon);
        if (parts.Count == 0)
            return false;

        var armed = CompOrNull<CMUSurgeryArmedStepComponent>(patient);
        if (tool is { } armedTool
            && armed is not null
            && _flowSurgery.TryHandleArmedToolUse(patient, armed, surgeon, armedTool, patient, out var handled, out _)
            && handled)
        {
            RefreshUiForPatient(patient);
            return true;
        }

        if (tool is { } usedTool
            && armed is null
            && CanAutoHandleToolIntent(patient)
            && TryArmByToolIntent(surgeon, patient, usedTool, parts))
        {
            return true;
        }

        var marker = EnsureComp<CMUSurgeryWindowOpenComponent>(surgeon);
        marker.Patient = patient;
        marker.TargetPartType = parts[0].Type;
        marker.TargetSymmetry = parts[0].Symmetry;

        var state = BuildBuiStateForViewer(patient, surgeon, marker, parts, armed);

        _ui.SetUiState(surgeon, CMUSurgeryUIKey.Key, state);
        _ui.OpenUi(surgeon, CMUSurgeryUIKey.Key, surgeon);
        return true;
    }

    public List<CMUSurgeryPartEntry> BuildPartEntries(
        EntityUid patient,
        EntityUid surgeon,
        bool ignoreSkillRequirements = false)
    {
        return _rulebook.BuildPartEntries(patient, surgeon, ignoreSkillRequirements);
    }

    public List<CMUSurgeryEntry> BuildEligibleSurgeries(
        EntityUid patient,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        EntityUid surgeon,
        EntityUid? targetPart = null,
        bool ignoreInProgressLock = false,
        bool ignoreSkillRequirements = false)
    {
        return _rulebook.BuildEligibleSurgeries(
            patient,
            partType,
            symmetry,
            surgeon,
            targetPart,
            ignoreInProgressLock,
            ignoreSkillRequirements);
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.SurgeryEnabled);
    }

    public bool IsUiStateCurrent(
        EntityUid patient,
        CMUSurgerySessionId? expectedSession,
        CMUSurgeryAttemptToken? expectedAttempt,
        CMUSurgeryArmedStateId? expectedArmedState)
    {
        if (!_sessions.MatchesExpectedState(patient, expectedSession, expectedAttempt))
            return false;

        var currentArmedState = CompOrNull<CMUSurgeryArmedStepComponent>(patient)?.StateId;
        return currentArmedState == expectedArmedState;
    }

    public bool CanAbandonSurgery(EntityUid patient, EntityUid surgeon)
    {
        if (_sessions.TryGetSession(patient, out var session))
        {
            // This is authority over one active action, not ownership of the
            // surgery. As soon as it stops, any qualified medic may take over.
            if (session.Phase == CMUSurgerySessionPhase.Performing)
                return session.ActiveSurgeon == surgeon;

            return HasRequiredSkillForProcedure(surgeon, session.Procedure.Id);
        }

        if (!TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed))
            return false;

        var procedure = string.IsNullOrEmpty(armed.LeafSurgeryId)
            ? armed.SurgeryId
            : armed.LeafSurgeryId;
        return HasRequiredSkillForProcedure(surgeon, procedure);
    }

    private bool HasRequiredSkillForProcedure(EntityUid surgeon, string surgeryId)
    {
        return !_flowSurgery.TryGetDefinition(surgeryId, out var definition)
            || _rulebook.HasRequiredSurgerySkill(surgeon, definition.MinSkill);
    }

    private CMUSurgeryBuiState BuildBuiStateForViewer(
        EntityUid patient,
        EntityUid viewer,
        CMUSurgeryWindowOpenComponent marker,
        List<CMUSurgeryPartEntry> parts,
        CMUSurgeryArmedStepComponent? armed)
    {
        marker.ViewRevision = NextViewRevision();
        var state = _flowSurgery.BuildBuiState(patient, Name(patient), parts, armed);
        state.ViewRevision = marker.ViewRevision;
        state.CanAbandon = CanAbandonSurgery(patient, viewer);
        return state;
    }

    private bool IsUiCommandCurrent(
        CMUSurgeryWindowOpenComponent marker,
        NetEntity expectedPatient,
        ulong expectedViewRevision,
        CMUSurgerySessionId? expectedSession,
        CMUSurgeryAttemptToken? expectedAttempt,
        CMUSurgeryArmedStateId? expectedArmedState)
    {
        if (marker.ViewRevision != expectedViewRevision
            || !TryGetEntity(expectedPatient, out var patient)
            || patient is null
            || patient.Value != marker.Patient)
        {
            return false;
        }

        return IsUiStateCurrent(marker.Patient, expectedSession, expectedAttempt, expectedArmedState);
    }

    private ulong NextViewRevision()
    {
        _lastViewRevision = unchecked(_lastViewRevision + 1);
        if (_lastViewRevision == 0)
            _lastViewRevision = 1;

        return _lastViewRevision;
    }

    private bool CanAutoHandleToolIntent(EntityUid patient)
    {
        if (!TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
            return false;

        return HasComp<CMUSurgeryInFlightComponent>(lockComp.Part);
    }

    private bool IsCmuOrganicSurgeryPatient(EntityUid patient)
    {
        return HasComp<CMUHumanMedicalComponent>(patient)
            || HasComp<YautjaComponent>(patient);
    }

    private bool TryArmByToolIntent(EntityUid surgeon, EntityUid patient, EntityUid tool, List<CMUSurgeryPartEntry> parts)
    {
        var candidates = new List<ToolIntentCandidate>();
        var hasSelectedPart = TryGetSelectedPart(surgeon, out var selectedType, out var selectedSymmetry);

        foreach (var part in parts)
        {
            if (part.LockedByOtherPart)
                continue;
            if (hasSelectedPart && (part.Type != selectedType || part.Symmetry != selectedSymmetry))
                continue;

            foreach (var entry in part.EligibleSurgeries)
            {
                if (!_flowSurgery.ToolMatchesCategory(tool, entry.NextStepToolCategory))
                    continue;

                var score = ScoreToolIntentCandidate(part, entry, hasSelectedPart);
                candidates.Add(new ToolIntentCandidate(part, entry, score));
            }
        }

        if (candidates.Count == 0)
            return false;

        if (!hasSelectedPart)
        {
            List<ToolIntentCandidate>? openCandidates = null;
            NetEntity? openPart = null;
            BodyPartType openType = default;
            BodyPartSymmetry openSymmetry = default;

            foreach (var candidate in candidates)
            {
                if (!candidate.Part.IsInFlightHere && !IsOpenPart(candidate.Part.Part))
                    continue;

                openCandidates ??= new List<ToolIntentCandidate>();
                openCandidates.Add(candidate);

                if (openPart is null)
                {
                    openPart = candidate.Part.Part;
                    openType = candidate.Part.Type;
                    openSymmetry = candidate.Part.Symmetry;
                    continue;
                }

                if (!openPart.Value.Equals(candidate.Part.Part)
                    || openType != candidate.Part.Type
                    || openSymmetry != candidate.Part.Symmetry)
                {
                    return false;
                }
            }

            if (openCandidates is not null)
                candidates = openCandidates;
            else if (candidates.Count != 1)
                return false;
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = candidates[0];
        if (candidates.Count > 1 && candidates[1].Score == best.Score)
            return false;

        var targetPart = GetEntity(best.Part.Part);
        if (!HasComp<BodyPartComponent>(targetPart))
        {
            if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(best.Entry.SurgeryId)
                && _flowSurgery.TryGetReattachAnchorPart(patient, out var anchor))
            {
                targetPart = anchor;
            }
            else
            {
                targetPart = patient;
            }
        }

        var armed = _flowSurgery.TryArmStep(
            surgeon,
            patient,
            targetPart,
            best.Entry.SurgeryId,
            best.Entry.NextStepIndex,
            best.Part.Type,
            best.Part.Symmetry);

        if (armed is null)
            return false;

        if (!_flowSurgery.TryHandleArmedToolUse(patient, armed, surgeon, tool, targetPart, out var handled, out var started) || !handled)
            return false;

        if (started)
        {
            _popup.PopupEntity(
                Loc.GetString("cmu-medical-surgery-auto-armed", ("surgery", best.Entry.DisplayName)),
                patient,
                surgeon);
        }

        RefreshUiForPatient(patient);
        return true;
    }

    private int ScoreToolIntentCandidate(CMUSurgeryPartEntry part, CMUSurgeryEntry entry, bool hasSelectedPart)
    {
        var score = 0;
        if (hasSelectedPart)
            score += 1000;
        if (part.IsInFlightHere)
            score += 200;
        if (IsOpenPart(part.Part))
            score += 100;
        if (entry.Category != "close_up")
            score += 25;

        score += CategoryPriority(entry.Category);
        return score;
    }

    private bool TryGetSelectedPart(EntityUid surgeon, out BodyPartType type, out BodyPartSymmetry symmetry)
    {
        type = default;
        symmetry = default;

        if (!TryComp<BodyZoneTargetingComponent>(surgeon, out var aim)
            || aim.LastSelectedAt == TimeSpan.Zero)
        {
            return false;
        }

        (type, symmetry) = SharedBodyZoneTargetingSystem.ToBodyPart(aim.Selected);
        return true;
    }

    private bool IsOpenPart(NetEntity part)
    {
        var uid = GetEntity(part);
        return HasComp<CMIncisionOpenComponent>(uid)
            || HasComp<CMSkinRetractedComponent>(uid)
            || HasComp<CMRibcageOpenComponent>(uid);
    }

    private static int CategoryPriority(string category)
    {
        return category switch
        {
            "bleed" => 90,
            "fracture" => 80,
            "burn" => 70,
            "suture" => 60,
            "head_organ" => 60,
            "parasite" => 50,
            "remove_organ" => 30,
            "amputation" => 20,
            "close_up" => -50,
            _ => 0,
        };
    }

    private void OnArmStepMessage(Entity<CMUSurgeryWindowOpenComponent> ent, ref CMUSurgeryArmStepMessage args)
    {
        var marker = ent.Comp;
        var medic = ent.Owner;
        if (!marker.Patient.IsValid())
            return;

        if (!IsUiCommandCurrent(
                marker,
                args.Patient,
                args.ExpectedViewRevision,
                args.ExpectedSession,
                args.ExpectedAttempt,
                args.ExpectedArmedState))
        {
            RefreshUiForPatient(marker.Patient);
            return;
        }

        CMUSurgeryPartEntry? selectedPart = null;
        CMUSurgeryEntry? selectedSurgery = null;
        foreach (var part in BuildPartEntries(marker.Patient, medic))
        {
            if (part.Part != args.Part
                || part.Type != args.TargetPartType
                || part.Symmetry != args.TargetSymmetry)
            {
                continue;
            }

            foreach (var surgery in part.EligibleSurgeries)
            {
                if (surgery.SurgeryId != args.SurgeryId || surgery.NextStepIndex != args.StepIndex)
                    continue;

                selectedPart = part;
                selectedSurgery = surgery;
                break;
            }

            if (selectedSurgery is not null)
                break;
        }

        if (selectedPart is null
            || selectedSurgery is null
            || !TryGetEntity(selectedPart.Part, out var selectedTarget)
            || selectedTarget is null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-surgery-cannot-start"), marker.Patient, medic);
            RefreshUiForPatient(marker.Patient);
            return;
        }

        var targetPart = selectedTarget.Value;
        if (!HasComp<BodyPartComponent>(targetPart))
        {
            if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(selectedSurgery.SurgeryId)
                && _flowSurgery.TryGetReattachAnchorPart(marker.Patient, out var anchor))
            {
                targetPart = anchor;
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("cmu-medical-surgery-cannot-start"), marker.Patient, medic);
                RefreshUiForPatient(marker.Patient);
                return;
            }
        }

        var armedType = selectedPart.Type;
        var armedSymmetry = selectedPart.Symmetry;
        marker.TargetPartType = armedType;
        marker.TargetSymmetry = armedSymmetry;

        if (_flowSurgery.TryGetMetadata(selectedSurgery.SurgeryId, out var metadata)
            && !_rulebook.HasRequiredSurgerySkill(medic, metadata.MinSkill))
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-surgery-missing-skills"), marker.Patient, medic);
            return;
        }

        var allowChoiceSwitch = TryComp<CMUSurgeryInProgressComponent>(marker.Patient, out var lockComp)
            && lockComp.AwaitingClosureChoice
            && lockComp.Part == targetPart;
        var armed = _flowSurgery.TryArmStep(
            medic,
            marker.Patient,
            targetPart,
            selectedSurgery.SurgeryId,
            selectedSurgery.NextStepIndex,
            armedType,
            armedSymmetry,
            allowSamePartInFlightSwitch: allowChoiceSwitch);
        if (armed is null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-surgery-cannot-start"), marker.Patient, medic);
            RefreshUiForPatient(marker.Patient);
            return;
        }

        if (allowChoiceSwitch)
        {
            _flowSurgery.EnsureSurgeryInFlight(
                marker.Patient,
                targetPart,
                medic,
                selectedSurgery.SurgeryId,
                _flowSurgery.ResolveSurgeryDisplayName(selectedSurgery.SurgeryId),
                armedType,
                armedSymmetry);
        }

        RefreshUiForPatient(marker.Patient);
    }

    private void OnClearArmedMessage(Entity<CMUSurgeryWindowOpenComponent> ent, ref CMUSurgeryClearArmedMessage args)
    {
        var marker = ent.Comp;
        if (!marker.Patient.IsValid())
            return;

        if (!IsUiCommandCurrent(
                marker,
                args.Patient,
                args.ExpectedViewRevision,
                args.ExpectedSession,
                args.ExpectedAttempt,
                args.ExpectedArmedState))
        {
            RefreshUiForPatient(marker.Patient);
            return;
        }

        if (!CanAbandonSurgery(marker.Patient, ent.Owner))
        {
            var message = _sessions.IsPerforming(marker.Patient)
                ? "cmu-medical-surgery-step-busy"
                : "cmu-medical-surgery-missing-skills";
            _popup.PopupEntity(Loc.GetString(message), marker.Patient, ent.Owner);
            RefreshUiForPatient(marker.Patient);
            return;
        }

        _flowSurgery.ClearArmed(marker.Patient);
        _flowSurgery.ClearSurgeryInFlight(marker.Patient);

        RefreshUiForPatient(marker.Patient);
    }

    private void OnUiClosed(Entity<CMUSurgeryWindowOpenComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not CMUSurgeryUIKey)
            return;

        RemComp<CMUSurgeryWindowOpenComponent>(ent.Owner);
    }

    private readonly record struct ToolIntentCandidate(CMUSurgeryPartEntry Part, CMUSurgeryEntry Entry, int Score);
}
