using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Medical.Surgery;
using Content.Server.Popups;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Surgery.Conditions;
using Content.Shared._CMU14.Medical.Surgery.Effects;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMUSurgeryDispatchSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly CMSurgerySystem _rmcSurgery = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedCMUSurgeryFlowSystem _flowSurgery = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    public override void Initialize()
    {
        base.Initialize();

        // RMC's CMSurgerySystem owns the <CMSurgeryToolComponent,
        // AfterInteractEvent> slot — the EventBus rejects a duplicate
        // directed subscription. RMC's OnToolAfterInteract calls into
        // TryDispatch directly so V2 wins the click cleanly.
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
            var state = _flowSurgery.BuildBuiState(patient, Name(patient), parts, armed, medic);
            _ui.SetUiState(medic, CMUSurgeryUIKey.Key, state);
        }
    }

    public bool TryDispatch(EntityUid surgeon, EntityUid patient, EntityUid? tool = null)
    {
        if (!IsLayerEnabled())
            return false;

        if (!HasComp<CMUHumanMedicalComponent>(patient))
            return false;

        if (!_flowSurgery.CanOperateOnPatient(patient, surgeon, popup: true))
            return true;

        var parts = BuildPartEntries(patient, surgeon);
        if (parts.Count == 0)
            return false;

        var armed = CompOrNull<CMUSurgeryArmedStepComponent>(patient);
        if (tool is { } usedTool
            && armed is null
            && CanAutoHandleToolIntent(surgeon, patient)
            && TryArmByToolIntent(surgeon, patient, usedTool, parts))
        {
            return true;
        }

        var marker = EnsureComp<CMUSurgeryWindowOpenComponent>(surgeon);
        marker.Patient = patient;
        // Default fallback only — armed-step messages carry an explicit
        // Part NetEntity that overrides this.
        marker.TargetPartType = parts[0].Type;
        marker.TargetSymmetry = parts[0].Symmetry;
        Dirty(surgeon, marker);

        var state = _flowSurgery.BuildBuiState(patient, Name(patient), parts, armed, surgeon);

        _ui.SetUiState(surgeon, CMUSurgeryUIKey.Key, state);
        _ui.OpenUi(surgeon, CMUSurgeryUIKey.Key, surgeon);
        return true;
    }

    private bool CanAutoHandleToolIntent(EntityUid surgeon, EntityUid patient)
    {
        if (!TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
            return false;

        return TryComp<CMUSurgeryInFlightComponent>(lockComp.Part, out var inFlight)
            && inFlight.Surgeon == surgeon;
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
            var openCandidates = candidates
                .Where(candidate => candidate.Part.IsInFlightHere || IsOpenPart(candidate.Part.Part))
                .ToList();

            if (openCandidates.Count > 0)
            {
                var openParts = openCandidates
                    .Select(candidate => (candidate.Part.Part, candidate.Part.Type, candidate.Part.Symmetry))
                    .Distinct()
                    .Count();
                if (openParts != 1)
                    return false;

                candidates = openCandidates;
            }
            else if (candidates.Count != 1)
            {
                return false;
            }
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = candidates[0];
        if (candidates.Count > 1 && candidates[1].Score == best.Score)
            return false;

        var targetPart = GetEntity(best.Part.Part);
        if (!HasComp<BodyPartComponent>(targetPart))
            targetPart = patient;

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

    private static int CategoryPriority(string category) => category switch
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

    private readonly record struct ToolIntentCandidate(CMUSurgeryPartEntry Part, CMUSurgeryEntry Entry, int Score);

    public List<CMUSurgeryPartEntry> BuildPartEntries(EntityUid patient, EntityUid surgeon, bool ignoreSkillRequirements = false)
    {
        var parts = new List<CMUSurgeryPartEntry>();
        if (!_flowSurgery.CanOperateOnPatient(patient, surgeon))
            return parts;

        TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp);
        var attachedSlots = new HashSet<(BodyPartType, BodyPartSymmetry)>();

        foreach (var (childId, childComp) in _body.GetBodyChildren(patient))
        {
            if (!IsSurgicallySupportedPart(childComp.PartType))
                continue;

            attachedSlots.Add((childComp.PartType, childComp.Symmetry));

            var eligible = BuildEligibleSurgeries(patient, childComp.PartType, childComp.Symmetry, surgeon, childId, ignoreSkillRequirements: ignoreSkillRequirements);

            var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(childComp.PartType, childComp.Symmetry);
            var conditionSummary = BuildConditionSummary(childId, childComp.PartType);
            var isInFlightHere = lockComp is not null && lockComp.Part == childId;
            var lockedByOtherPart = lockComp is not null && lockComp.Part != childId;

            parts.Add(new CMUSurgeryPartEntry(
                GetNetEntity(childId),
                childComp.PartType,
                childComp.Symmetry,
                displayName,
                conditionSummary,
                isInFlightHere,
                lockedByOtherPart,
                eligible));
        }

        if (TryComp<BodyComponent>(patient, out var bodyComp)
            && _body.GetRootPartOrNull(patient, bodyComp) is { } root)
        {
            var patientNetEntity = GetNetEntity(patient);
            foreach (var (slotId, slot) in root.BodyPart.Children)
            {
                if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                    continue;
                var symmetry = slotId.Contains("left", StringComparison.Ordinal)
                    ? BodyPartSymmetry.Left
                    : (slotId.Contains("right", StringComparison.Ordinal)
                        ? BodyPartSymmetry.Right
                        : BodyPartSymmetry.None);
                if (symmetry == BodyPartSymmetry.None)
                    continue;
                if (attachedSlots.Contains((slot.Type, symmetry)))
                    continue;

                var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(slot.Type, symmetry);
                var conditionSummary = Loc.GetString("cmu-medical-surgery-condition-missing");
                var eligible = BuildEligibleSurgeries(patient, slot.Type, symmetry, surgeon, null, ignoreSkillRequirements: ignoreSkillRequirements);
                // Reattach pins lockComp.Part to the patient body but stores
                // the targeted slot in (TargetPartType, TargetSymmetry) — so
                // only the matching synthesized slot is "in flight here";
                // the other missing slot (if any) is locked.
                var isInFlightHere = lockComp is not null
                    && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(lockComp.LeafSurgeryId)
                    && lockComp.TargetPartType == slot.Type
                    && lockComp.TargetSymmetry == symmetry;
                var lockedByOtherPart = lockComp is not null && !isInFlightHere;

                parts.Add(new CMUSurgeryPartEntry(
                    patientNetEntity,
                    slot.Type,
                    symmetry,
                    displayName,
                    conditionSummary,
                    isInFlightHere,
                    lockedByOtherPart,
                    eligible));
            }
        }

        return parts;
    }

    private static bool IsSurgicallySupportedPart(BodyPartType type) =>
        type is BodyPartType.Head or BodyPartType.Torso or BodyPartType.Arm or BodyPartType.Leg;

    private string BuildConditionSummary(EntityUid part, BodyPartType partType)
    {
        var bits = new List<string>();
        if (HasComp<CMIncisionOpenComponent>(part))
            bits.Add(Loc.GetString("cmu-medical-surgery-condition-incision-open"));
        if (HasComp<CMRibcageOpenComponent>(part))
            bits.Add(Loc.GetString(GetOpenBoneConditionKey(partType)));
        if (TryComp<FractureComponent>(part, out var frac))
        {
            var severity = frac.Severity;
            if (severity != FractureSeverity.None)
            {
                var severityKey = severity switch
                {
                    FractureSeverity.Hairline => "hairline",
                    FractureSeverity.Simple => "simple",
                    FractureSeverity.Compound => "compound",
                    FractureSeverity.Comminuted => "comminuted",
                    _ => "fracture",
                };
                bits.Add(Loc.GetString("cmu-medical-surgery-condition-fracture",
                    ("severity", severityKey)));
            }
        }
        if (HasComp<InternalBleedingComponent>(part))
            bits.Add(Loc.GetString("cmu-medical-surgery-condition-internal-bleed"));
        if (HasComp<CMUEscharComponent>(part))
            bits.Add(Loc.GetString("cmu-medical-surgery-condition-eschar"));
        return string.Join(" · ", bits);
    }

    private static string GetOpenBoneConditionKey(BodyPartType partType)
    {
        return partType switch
        {
            BodyPartType.Head => "cmu-medical-surgery-condition-skull-open",
            BodyPartType.Torso => "cmu-medical-surgery-condition-ribcage-open",
            _ => "cmu-medical-surgery-condition-bones-open",
        };
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.SurgeryEnabled);
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
        var entries = new List<CMUSurgeryEntry>();

        if (targetPart is null)
        {
            foreach (var (childId, childComp) in _body.GetBodyChildren(patient))
            {
                if (childComp.PartType != partType || childComp.Symmetry != symmetry)
                    continue;
                targetPart = childId;
                break;
            }
        }

        // Patient-level lock: when a surgery is in-flight, only the
        // in-flight surgery shows on its own part — every other (part,
        // surgery) combo is blocked.
        TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp);

        foreach (var metadata in _flowSurgery.EnumerateMetadata())
        {
            if (!_prototypes.TryIndex<EntityPrototype>(metadata.Surgery, out var surgeryProto))
                continue;

            if (!metadata.ValidParts.Contains(partType))
                continue;

            if (patient == surgeon && !_flowSurgery.CanSelfOperateSurgery(metadata.Surgery, partType))
                continue;

            if (!ignoreSkillRequirements && !HasRequiredSurgerySkill(surgeon, metadata.MinSkill))
                continue;

            if (lockComp is not null && !ignoreInProgressLock)
            {
                // Reattach uses patient body as anchor — match by stored
                // target slot type/symmetry instead of part-equality so the
                // synthesized missing-slot entry surfaces the in-flight
                // reattach's next step.
                if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(metadata.Surgery))
                {
                    if (lockComp.TargetPartType != partType || lockComp.TargetSymmetry != symmetry)
                        continue;
                }
                else if (lockComp.Part != targetPart)
                {
                    continue;
                }

                if (lockComp.AwaitingClosureChoice)
                {
                    if (!IsContinuationChoiceCategory(metadata.Category))
                        continue;
                    if (lockComp.LeafSurgeryId == metadata.Surgery)
                        continue;
                }
                else if (lockComp.LeafSurgeryId != metadata.Surgery)
                {
                    continue;
                }
            }

            if (!IsNeededSurgeryForPart(patient, targetPart, surgeryProto.ID, metadata.Category, partType))
                continue;

            if (!IsSurgeryEligible(patient, targetPart, surgeryProto, partType, surgeon))
                continue;

            // Reattach has no part — markers ride on the patient body.
            var resolveTarget = targetPart;
            if (resolveTarget is null
                && (metadata.Surgery == "CMUSurgeryReattachLimb" || metadata.Surgery == "RMCSynthSurgeryReattachLimb"))
                resolveTarget = patient;

            CMUResolvedStep resolved;
            if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var armedComp)
                && armedComp.LeafSurgeryId == metadata.Surgery
                && armedComp.TargetPartType == partType
                && armedComp.TargetSymmetry == symmetry)
            {
                if (!_flowSurgery.TryResolveStepAt(armedComp.SurgeryId, armedComp.StepIndex, out resolved, targetPart))
                    continue;
            }
            else if (!_flowSurgery.TryResolveNextStep(patient, resolveTarget, metadata.Surgery, out resolved))
            {
                continue;
            }

            entries.Add(new CMUSurgeryEntry(
                metadata.Surgery,
                metadata.DisplayName ?? surgeryProto.Name,
                resolved.StepLabel,
                resolved.ToolCategory,
                resolved.AbsoluteStepIndex,
                resolved.TotalSteps,
                resolved.GatingSurgeryId,
                metadata.Category));
        }

        // Post-abandon cleanup: surface the CMU close-up that matches the
        // physical access state. Soft-tissue openings only need cautery;
        // opened skulls/ribcages must be closed and mended first.
        var closeUpLockedHere = lockComp is not null
            && targetPart is { } lockedPart
            && lockComp.Part == lockedPart
            && IsCloseUpSurgeryId(lockComp.LeafSurgeryId);
        var canShowCloseUp = lockComp is null
            || closeUpLockedHere
            || (lockComp.AwaitingClosureChoice && targetPart is { } choicePart && lockComp.Part == choicePart);
        if (canShowCloseUp && targetPart is { } closePart)
        {
            if (lockComp is { AwaitingClosureChoice: true }
                && lockComp.Part == closePart
                && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(lockComp.LeafSurgeryId))
            {
                TryAddReattachCloseUpEntry(patient, closePart, partType, lockComp.LeafSurgeryId, entries, surgeon);
            }
            else if (closeUpLockedHere && lockComp is not null)
            {
                TryAddCloseUpEntry(patient, closePart, partType, lockComp.LeafSurgeryId, entries, surgeon);
            }
            else if (NeedsBoneCavityClosure(closePart))
            {
                TryAddCloseUpEntry(patient, closePart, partType, "CMUSurgeryCloseBoneCavity", entries, surgeon);
            }
            else if (NeedsSoftTissueClosure(closePart))
            {
                TryAddCloseUpEntry(patient, closePart, partType, "CMUSurgeryCloseIncision", entries, surgeon);
            }
        }

        return entries;
    }

    private void TryAddReattachCloseUpEntry(EntityUid patient, EntityUid part, BodyPartType partType, string surgeryId, List<CMUSurgeryEntry> entries, EntityUid surgeon)
    {
        if (patient == surgeon && !_flowSurgery.CanSelfOperateSurgery(surgeryId, partType))
            return;
        if (_flowSurgery.TryGetMetadata(surgeryId, out var metadata) && !HasRequiredSurgerySkill(surgeon, metadata.MinSkill))
            return;
        if (!_prototypes.TryIndex<EntityPrototype>(surgeryId, out var proto))
            return;
        if (!_flowSurgery.TryResolveNextStep(patient, part, surgeryId, out var resolved))
            return;
        if (resolved.ResolvedSurgeryId != surgeryId)
            return;

        entries.Add(new CMUSurgeryEntry(
            surgeryId,
            proto.Name,
            resolved.StepLabel,
            resolved.ToolCategory,
            resolved.AbsoluteStepIndex,
            resolved.TotalSteps,
            resolved.GatingSurgeryId,
            "close_up"));
    }

    private bool NeedsBoneCavityClosure(EntityUid part)
    {
        return HasComp<CMRibcageOpenComponent>(part)
            || HasComp<CMRibcageSawedComponent>(part);
    }

    private bool NeedsSoftTissueClosure(EntityUid part)
    {
        return HasComp<CMIncisionOpenComponent>(part)
            || HasComp<CMBleedersClampedComponent>(part)
            || HasComp<CMSkinRetractedComponent>(part);
    }

    private static bool IsCloseUpSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgeryCloseIncision"
            or "CMUSurgeryCloseBoneCavity"
            or "CMSurgeryCloseIncision"
            or "CMSurgeryCloseRibcage";
    }

    private bool IsNeededSurgeryForPart(
        EntityUid patient,
        EntityUid? targetPart,
        string surgeryId,
        string category,
        BodyPartType partType)
    {
        if (targetPart is not { } part)
            return category == "reattach";

        return category switch
        {
            "fracture" => TryComp<FractureComponent>(part, out var fracture)
                && fracture.Severity != FractureSeverity.None,
            "bleed" => HasComp<InternalBleedingComponent>(part),
            "burn" => HasComp<CMUEscharComponent>(part),
            "parasite" => partType == BodyPartType.Torso,
            "suture" or "head_organ" => HasDamagedOrganForSurgery(part, surgeryId),
            "remove_organ" => HasOrganForSurgery(part, surgeryId),
            "transplant" => IsOrganReplacementNeededForSurgery(part, surgeryId),
            "amputation" => partType is BodyPartType.Arm or BodyPartType.Leg,
            _ => true,
        };
    }

    private bool HasDamagedOrganForSurgery(EntityUid part, string surgeryId)
    {
        if (!TryGetOrganConditionForSurgery(surgeryId, out var slot, out var minStage))
            return false;

        return HasOrganInSlotAtLeast(part, slot, minStage);
    }

    private bool HasDeadOrganForSurgery(EntityUid part, string surgeryId)
    {
        if (!TryGetOrganConditionForSurgery(surgeryId, out var slot, out _))
            return false;

        return HasOrganInSlotAtLeast(part, slot, OrganDamageStage.Dead);
    }

    private bool HasOrganForSurgery(EntityUid part, string surgeryId)
    {
        if (!TryGetOrganConditionForSurgery(surgeryId, out var slot, out _))
            return false;

        return TryGetOrganInSlot(part, slot, out _);
    }

    private bool IsOrganReplacementNeededForSurgery(EntityUid part, string surgeryId)
    {
        if (!TryGetReinsertOrganSlotForSurgery(surgeryId, out var slot))
            return false;

        return !TryGetOrganInSlot(part, slot, out _);
    }

    private bool HasOrganInSlotAtLeast(EntityUid part, string slot, OrganDamageStage stage)
    {
        return TryGetOrganInSlot(part, slot, out var organ)
            && TryComp<OrganHealthComponent>(organ, out var health)
            && health.Stage.IsAtLeast(stage);
    }

    private bool TryGetOrganInSlot(EntityUid part, string slotId, out EntityUid organ)
    {
        organ = default;
        var containerId = SharedBodySystem.GetOrganContainerId(slotId);
        if (!_containers.TryGetContainer(part, containerId, out var container))
            return false;

        foreach (var contained in container.ContainedEntities)
        {
            if (!HasComp<OrganComponent>(contained))
                continue;

            organ = contained;
            return true;
        }

        return false;
    }

    private bool TryGetOrganConditionForSurgery(string surgeryId, out string slot, out OrganDamageStage minStage)
    {
        slot = string.Empty;
        minStage = OrganDamageStage.Bruised;

        if (_rmcSurgery.GetSingleton(new EntProtoId(surgeryId)) is not { } surgeryEnt
            || !TryComp<CMSurgeryComponent>(surgeryEnt, out var surgery))
        {
            return false;
        }

        foreach (var stepId in surgery.Steps)
        {
            if (_rmcSurgery.GetSingleton(stepId) is not { } stepEnt
                || !TryComp<CMUOrganDamagedSurgeryConditionComponent>(stepEnt, out var condition))
            {
                continue;
            }

            slot = condition.OrganSlot;
            minStage = condition.MinStage;
            return true;
        }

        return false;
    }

    private bool TryGetReinsertOrganSlotForSurgery(string surgeryId, out string slot)
    {
        slot = string.Empty;

        if (_rmcSurgery.GetSingleton(new EntProtoId(surgeryId)) is not { } surgeryEnt
            || !TryComp<CMSurgeryComponent>(surgeryEnt, out var surgery))
        {
            return false;
        }

        foreach (var stepId in surgery.Steps)
        {
            if (_rmcSurgery.GetSingleton(stepId) is not { } stepEnt
                || !TryComp<CMUSurgeryStepReinsertOrganEffectComponent>(stepEnt, out var reinsert))
            {
                continue;
            }

            slot = reinsert.OrganSlot;
            return true;
        }

        return false;
    }

    private bool HasRequiredSurgerySkill(EntityUid surgeon, int minSkill)
    {
        return minSkill <= 0 || _skills.HasSkill(surgeon, SurgerySkill, minSkill);
    }

    private void TryAddCloseUpEntry(EntityUid patient, EntityUid part, BodyPartType partType, string surgeryId, List<CMUSurgeryEntry> entries, EntityUid surgeon)
    {
        if (patient == surgeon && !_flowSurgery.CanSelfOperateSurgery(surgeryId, partType))
            return;

        if (!_prototypes.TryIndex<EntityPrototype>(surgeryId, out var proto))
            return;
        if (!IsSurgeryEligible(patient, part, proto, partType, surgeon))
            return;
        if (!_flowSurgery.TryResolveNextStep(patient, part, surgeryId, out var resolved))
            return;

        entries.Add(new CMUSurgeryEntry(
            surgeryId,
            proto.Name,
            resolved.StepLabel,
            resolved.ToolCategory,
            resolved.AbsoluteStepIndex,
            resolved.TotalSteps,
            resolved.GatingSurgeryId,
            "close_up"));
    }

    private bool IsSurgeryEligible(EntityUid patient, EntityUid? targetPart, EntityPrototype surgeryProto, BodyPartType partType, EntityUid surgeon)
    {
        var patientIsSynth = HasComp<SynthComponent>(patient);
        var surgeryIsSynth = surgeryProto.HasComponent<RMCSynthSurgeryComponent>();

        // Synth bodies use synth-marked surgery only. Their brute/burn repair
        // stays on the synth tool-repair path, so human grafts and other
        // organic procedures should never be surfaced for them.
        if (patientIsSynth != surgeryIsSynth)
            return false;

        // Reattach surfaces ONLY on the synthesized missing-slot entries
        // (targetPart is null). Held-limb match enforcement lives in
        // click-target validation, so dispatch can surface reattach
        // unconditionally on the missing slot.
        if (surgeryProto.ID == "CMUSurgeryReattachLimb" || surgeryProto.ID == "RMCSynthSurgeryReattachLimb")
        {
            if (targetPart is not null)
                return false;
            return ReattachHasAnyMissingSlot(patient);
        }

        if (targetPart is not { } part)
            return false;

        if (_rmcSurgery.GetSingleton(new EntProtoId(surgeryProto.ID)) is not { } surgeryEnt)
            return false;

        var validEv = new CMSurgeryValidEvent(patient, part);
        RaiseLocalEvent(surgeryEnt, ref validEv);
        return !validEv.Cancelled;
    }

    private bool ReattachHasAnyMissingSlot(EntityUid patient)
    {
        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return false;
        if (_body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return false;

        foreach (var (slotId, slot) in root.BodyPart.Children)
        {
            if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;
            var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (!_containers.TryGetContainer(root.Entity, containerId, out var container))
                return true;
            if (container.ContainedEntities.Count == 0)
                return true;
        }
        return false;
    }

    private bool ReattachWouldSucceed(EntityUid patient, EntityUid surgeon)
    {
        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return false;

        if (_body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return false;

        var missingSlots = new List<(BodyPartType Type, BodyPartSymmetry Symmetry)>();
        foreach (var (slotId, slot) in root.BodyPart.Children)
        {
            if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;
            var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (!_containers.TryGetContainer(root.Entity, containerId, out var container))
                continue;
            if (container.ContainedEntities.Count > 0)
                continue;
            var symmetry = slotId.Contains("left", StringComparison.Ordinal)
                ? BodyPartSymmetry.Left
                : (slotId.Contains("right", StringComparison.Ordinal)
                    ? BodyPartSymmetry.Right
                    : BodyPartSymmetry.None);
            if (symmetry == BodyPartSymmetry.None)
                continue;
            missingSlots.Add((slot.Type, symmetry));
        }

        if (missingSlots.Count == 0)
            return false;
        if (!surgeon.IsValid())
            return false;

        foreach (var held in _hands.EnumerateHeld(surgeon))
        {
            if (!TryComp<BodyPartComponent>(held, out var heldBp))
                continue;
            if (heldBp.PartType is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;
            foreach (var (slotType, slotSymmetry) in missingSlots)
            {
                if (slotType == heldBp.PartType && slotSymmetry == heldBp.Symmetry)
                    return true;
            }
        }

        return false;
    }

    private void OnArmStepMessage(Entity<CMUSurgeryWindowOpenComponent> ent, ref CMUSurgeryArmStepMessage args)
    {
        var marker = ent.Comp;
        var medic = ent.Owner;
        if (!marker.Patient.IsValid())
            return;

        // Reattach uses the patient body as a symbolic anchor (V1's
        // ApplyLimbReattach drives slot lookup off the held-limb's
        // BodyPartComponent). Synthesized missing-slot entries send the
        // patient NetEntity as Part and supply (TargetPartType,
        // TargetSymmetry) explicitly; real parts derive them from the
        // BodyPartComponent on targetPart.
        EntityUid targetPart = GetEntity(args.Part);
        BodyPartType armedType = args.TargetPartType;
        BodyPartSymmetry armedSymmetry = args.TargetSymmetry;
        if (TryComp<BodyPartComponent>(targetPart, out var partComp))
        {
            armedType = partComp.PartType;
            armedSymmetry = partComp.Symmetry;
        }
        else
        {
            targetPart = marker.Patient;
        }

        marker.TargetPartType = armedType;
        marker.TargetSymmetry = armedSymmetry;
        Dirty(medic, marker);

        if (_flowSurgery.TryGetMetadata(args.SurgeryId, out var metadata)
            && !HasRequiredSurgerySkill(medic, metadata.MinSkill))
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
            args.SurgeryId,
            args.StepIndex,
            armedType,
            armedSymmetry,
            allowSamePartInFlightSwitch: allowChoiceSwitch);
        if (armed is null)
        {
            _popup.PopupEntity(Loc.GetString("cmu-medical-surgery-cannot-start"), marker.Patient, medic);
            return;
        }

        if (allowChoiceSwitch)
        {
            _flowSurgery.EnsureSurgeryInFlight(
                marker.Patient,
                targetPart,
                medic,
                args.SurgeryId,
                _flowSurgery.ResolveSurgeryDisplayName(args.SurgeryId),
                armedType,
                armedSymmetry);
        }

        // Re-walk the part list because re-arming may have eliminated some
        // eligible surgeries (e.g. an open-incision step now removes the
        // prerequisite gate from a fracture-set).
        var parts = BuildPartEntries(marker.Patient, medic);
        var state = _flowSurgery.BuildBuiState(marker.Patient, Name(marker.Patient), parts, armed, medic);
        _ui.SetUiState(medic, CMUSurgeryUIKey.Key, state);
    }

    private void OnClearArmedMessage(Entity<CMUSurgeryWindowOpenComponent> ent, ref CMUSurgeryClearArmedMessage args)
    {
        var marker = ent.Comp;
        if (!marker.Patient.IsValid())
            return;
        // BUI cancel is deliberate takeover/abandon. Opening another
        // surgeon's menu is harmless, but once a medic presses abandon they
        // should be able to stop a stale or unsafe surgery order.
        var medic = ent.Owner;
        _flowSurgery.ClearArmed(marker.Patient);
        _flowSurgery.ClearSurgeryInFlight(marker.Patient);

        var parts = BuildPartEntries(marker.Patient, medic);
        var refreshedArmed = CompOrNull<CMUSurgeryArmedStepComponent>(marker.Patient);
        var state = _flowSurgery.BuildBuiState(marker.Patient, Name(marker.Patient), parts, refreshedArmed, medic);
        _ui.SetUiState(medic, CMUSurgeryUIKey.Key, state);
    }

    private void OnUiClosed(Entity<CMUSurgeryWindowOpenComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not CMUSurgeryUIKey)
            return;
        // The patient's armed component intentionally stays — the medic
        // may re-open the window or click the patient with the right tool.
        RemComp<CMUSurgeryWindowOpenComponent>(ent.Owner);
    }

    private static bool IsOrganRepairChoiceCategory(string category)
    {
        return category is "suture" or "head_organ";
    }

    private static bool IsContinuationChoiceCategory(string category)
    {
        return category is "bleed"
            or "fracture"
            or "burn"
            or "parasite"
            or "suture"
            or "head_organ"
            or "remove_organ"
            or "amputation"
            or "transplant";
    }
}
