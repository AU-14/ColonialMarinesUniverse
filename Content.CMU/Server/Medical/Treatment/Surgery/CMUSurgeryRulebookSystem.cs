using System;
using System.Collections.Generic;
using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgeryRulebookSystem : EntitySystem
{
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMSurgerySystem _rmcSurgery = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _flowSurgery = default!;
    [Dependency] private CMUSurgerySessionSystem _sessions = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    public List<CMUSurgeryPartEntry> BuildPartEntries(
        EntityUid patient,
        EntityUid surgeon,
        bool ignoreSkillRequirements = false,
        bool allowOptionalHemostasis = false)
    {
        var parts = new List<CMUSurgeryPartEntry>();
        if (!_flowSurgery.CanOperateOnPatient(patient, surgeon))
            return parts;

        TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp);
        var hasSession = _sessions.TryGetSession(patient, out var session);
        var sessionBlocksOtherSites = hasSession
            && (session.Phase == CMUSurgerySessionPhase.Performing || lockComp is not null);
        foreach (var (childId, childComp) in _medicalIndex.GetBodyParts(patient))
        {
            if (!IsSurgicallySupportedPart(childComp.PartType))
                continue;

            var eligible = BuildEligibleSurgeries(
                patient,
                childComp.PartType,
                childComp.Symmetry,
                surgeon,
                childId,
                ignoreSkillRequirements: ignoreSkillRequirements,
                allowOptionalHemostasis: allowOptionalHemostasis);

            var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(childComp.PartType, childComp.Symmetry);
            var conditionSummary = BuildConditionSummary(childId, childComp.PartType);
            var isReattachLock = lockComp is not null && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(lockComp.LeafSurgeryId);
            var isInFlightHere = lockComp is not null
                && lockComp.Part == childId
                && (!isReattachLock
                    || (lockComp.TargetPartType == childComp.PartType && lockComp.TargetSymmetry == childComp.Symmetry));
            isInFlightHere |= hasSession
                && session.Site == new CMUMedicalBodyPartKey(childComp.PartType, childComp.Symmetry);
            var lockedByOtherPart = (lockComp is not null || sessionBlocksOtherSites) && !isInFlightHere;

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

        if (_medicalIndex.TryGetRootPart(patient, out var root))
        {
            var patientNetEntity = GetNetEntity(patient);
            foreach (var slot in _medicalIndex.GetBodyPartSlots(root.Owner))
            {
                if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                    continue;
                if (!CMUBodyPartSlots.TryGetSymmetry(slot.SlotId, BodyPartSymmetry.None, out var symmetry))
                    continue;
                if (slot.Part is not null)
                    continue;

                var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(slot.Type, symmetry);
                var conditionSummary = Loc.GetString("cmu-medical-surgery-condition-missing");
                var eligible = BuildEligibleSurgeries(
                    patient,
                    slot.Type,
                    symmetry,
                    surgeon,
                    null,
                    ignoreSkillRequirements: ignoreSkillRequirements,
                    allowOptionalHemostasis: allowOptionalHemostasis);
                var isInFlightHere = lockComp is not null
                    && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(lockComp.LeafSurgeryId)
                    && lockComp.TargetPartType == slot.Type
                    && lockComp.TargetSymmetry == symmetry;
                isInFlightHere |= hasSession
                    && session.Site == new CMUMedicalBodyPartKey(slot.Type, symmetry);
                var lockedByOtherPart = (lockComp is not null || sessionBlocksOtherSites) && !isInFlightHere;

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

    public List<CMUSurgeryEntry> BuildEligibleSurgeries(
        EntityUid patient,
        BodyPartType partType,
        BodyPartSymmetry symmetry,
        EntityUid surgeon,
        EntityUid? targetPart = null,
        bool ignoreInProgressLock = false,
        bool ignoreSkillRequirements = false,
        bool allowOptionalHemostasis = false)
    {
        var entries = new List<CMUSurgeryEntry>();

        if (!ignoreInProgressLock
            && _sessions.TryGetSession(patient, out var session)
            && session.Phase == CMUSurgerySessionPhase.Performing)
        {
            return entries;
        }

        if (targetPart is null)
        {
            foreach (var (childId, childComp) in _medicalIndex.GetBodyParts(patient))
            {
                if (childComp.PartType != partType || childComp.Symmetry != symmetry)
                    continue;

                targetPart = childId;
                break;
            }
        }

        TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp);

        foreach (var surgery in _flowSurgery.GetEligibleDefinitions(partType))
        {
            if (patient == surgeon && !_flowSurgery.CanSelfOperateSurgery(surgery.Id.Id, partType))
                continue;

            if (!ignoreSkillRequirements && !HasRequiredSurgerySkill(surgeon, surgery.MinSkill))
                continue;

            if (lockComp is not null && !ignoreInProgressLock)
            {
                if (SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(surgery.Id.Id))
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
                    if (!IsContinuationChoiceCategory(surgery.Category))
                        continue;
                    if (lockComp.LeafSurgeryId == surgery.Id.Id)
                        continue;
                }
                else if (lockComp.LeafSurgeryId != surgery.Id.Id)
                {
                    continue;
                }
            }

            if (!IsNeededSurgeryForPart(patient, targetPart, surgery.Id.Id, surgery.Category, partType))
                continue;

            if (!IsSurgeryEligible(patient, targetPart, surgery, partType, surgeon))
                continue;

            var resolveTarget = targetPart;
            if (resolveTarget is null
                && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(surgery.Id.Id))
            {
                if (!_flowSurgery.TryGetReattachAnchorPart(patient, out var anchor))
                    continue;

                resolveTarget = anchor;
            }

            CMUResolvedStep resolved;
            if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var armedComp)
                && armedComp.LeafSurgeryId == surgery.Id.Id
                && armedComp.TargetPartType == partType
                && armedComp.TargetSymmetry == symmetry)
            {
                if (!_flowSurgery.TryResolveStepAt(armedComp.SurgeryId, armedComp.StepIndex, out resolved, targetPart))
                    continue;
            }
            else if (!_flowSurgery.TryResolveNextStep(
                         patient,
                         resolveTarget,
                         surgery.Id.Id,
                         out resolved,
                         allowOptionalHemostasis))
            {
                continue;
            }

            entries.Add(BuildEntry(surgery, resolved));
        }

        TryAddCloseUpEntries(patient, targetPart, partType, lockComp, entries, surgeon);
        return entries;
    }

    public bool HasRequiredSurgerySkill(EntityUid surgeon, int minSkill)
    {
        return minSkill <= 0 || _skills.HasSkill(surgeon, SurgerySkill, minSkill);
    }

    private static CMUSurgeryEntry BuildEntry(
        CMUSurgeryDefinition surgery,
        CMUResolvedStep resolved)
    {
        return new CMUSurgeryEntry(
            surgery.Id.Id,
            surgery.DisplayName,
            resolved.StepLabel,
            resolved.ToolCategory,
            resolved.AbsoluteStepIndex,
            resolved.TotalSteps,
            resolved.GatingSurgeryId,
            surgery.Category);
    }

    private static bool IsSurgicallySupportedPart(BodyPartType type)
    {
        return type is BodyPartType.Head
            or BodyPartType.Torso
            or BodyPartType.Arm
            or BodyPartType.Hand
            or BodyPartType.Leg
            or BodyPartType.Foot;
    }

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
                    FractureSeverity.Shattered => "shattered",
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
        foreach (var trait in _surgicalTraits.EnumerateOrderedTraits(part))
            bits.Add(Loc.GetString(CMUSurgicalTraitMetadata.ConditionLocId(trait)));

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

    private void TryAddCloseUpEntries(
        EntityUid patient,
        EntityUid? targetPart,
        BodyPartType partType,
        CMUSurgeryInProgressComponent? lockComp,
        List<CMUSurgeryEntry> entries,
        EntityUid surgeon)
    {
        var closeUpLockedHere = lockComp is not null
            && targetPart is { } lockedPart
            && lockComp.Part == lockedPart
            && SharedCMUSurgeryFlowSystem.IsCloseUpSurgeryId(lockComp.LeafSurgeryId);
        var canShowCloseUp = lockComp is null
            || closeUpLockedHere
            || (lockComp.AwaitingClosureChoice && targetPart is { } choicePart && lockComp.Part == choicePart);

        if (!canShowCloseUp || targetPart is not { } closePart)
            return;

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

    private void TryAddReattachCloseUpEntry(
        EntityUid patient,
        EntityUid part,
        BodyPartType partType,
        string surgeryId,
        List<CMUSurgeryEntry> entries,
        EntityUid surgeon)
    {
        if (patient == surgeon && !_flowSurgery.CanSelfOperateSurgery(surgeryId, partType))
            return;
        if (!_flowSurgery.TryGetDefinition(surgeryId, out var surgery))
            return;
        if (!HasRequiredSurgerySkill(surgeon, surgery.MinSkill))
            return;
        if (!_flowSurgery.TryResolveNextStep(patient, part, surgeryId, out var resolved))
            return;
        if (resolved.ResolvedSurgeryId != surgeryId)
            return;

        entries.Add(new CMUSurgeryEntry(
            surgeryId,
            surgery.DisplayName,
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
        return _medicalIndex.TryGetOrganInSlot(part, slotId, out organ);
    }

    private bool TryGetOrganConditionForSurgery(string surgeryId, out string slot, out OrganDamageStage minStage)
    {
        slot = string.Empty;
        minStage = OrganDamageStage.Bruised;

        if (!_flowSurgery.TryGetDefinition(surgeryId, out var surgery))
            return false;

        foreach (var step in surgery.Steps)
        {
            if (step.OrganCondition is not { } condition)
                continue;

            slot = condition.OrganSlot;
            minStage = condition.MinStage;
            return true;
        }

        return false;
    }

    private bool TryGetReinsertOrganSlotForSurgery(string surgeryId, out string slot)
    {
        slot = string.Empty;

        if (!_flowSurgery.TryGetDefinition(surgeryId, out var surgery))
            return false;

        foreach (var step in surgery.Steps)
        {
            if (step.ReinsertOrganSlot is not { } reinsertOrganSlot)
                continue;

            slot = reinsertOrganSlot;
            return true;
        }

        return false;
    }

    private void TryAddCloseUpEntry(
        EntityUid patient,
        EntityUid part,
        BodyPartType partType,
        string surgeryId,
        List<CMUSurgeryEntry> entries,
        EntityUid surgeon)
    {
        if (patient == surgeon && !_flowSurgery.CanSelfOperateSurgery(surgeryId, partType))
            return;

        if (!_flowSurgery.TryGetDefinition(surgeryId, out var surgery))
            return;
        if (!IsSurgeryEligible(patient, part, surgery, partType, surgeon))
            return;
        if (!_flowSurgery.TryResolveNextStep(patient, part, surgeryId, out var resolved))
            return;

        entries.Add(new CMUSurgeryEntry(
            surgeryId,
            surgery.DisplayName,
            resolved.StepLabel,
            resolved.ToolCategory,
            resolved.AbsoluteStepIndex,
            resolved.TotalSteps,
            resolved.GatingSurgeryId,
            "close_up"));
    }

    private bool IsSurgeryEligible(
        EntityUid patient,
        EntityUid? targetPart,
        CMUSurgeryDefinition surgery,
        BodyPartType partType,
        EntityUid surgeon)
    {
        var patientIsSynth = HasComp<SynthComponent>(patient);
        var surgeryIsSynth = surgery.Prototype.HasComponent<RMCSynthSurgeryComponent>();

        if (patientIsSynth != surgeryIsSynth)
            return false;

        if (surgery.Id.Id is "CMUSurgeryReattachLimb" or "RMCSynthSurgeryReattachLimb")
        {
            if (targetPart is not null)
                return false;

            return ReattachHasAnyMissingSlot(patient);
        }

        if (targetPart is not { } part)
            return false;

        if (_rmcSurgery.GetSingleton(surgery.Id) is not { } surgeryEnt)
            return false;

        var validEv = new CMSurgeryValidEvent(patient, part);
        RaiseLocalEvent(surgeryEnt, ref validEv);
        return !validEv.Cancelled;
    }

    private bool ReattachHasAnyMissingSlot(EntityUid patient)
    {
        if (!_medicalIndex.TryGetRootPart(patient, out var root))
            return false;

        foreach (var slot in _medicalIndex.GetBodyPartSlots(root.Owner))
        {
            if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;

            if (slot.Part is null)
                return true;
        }

        return false;
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
