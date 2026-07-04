using System;
using System.Collections.Generic;
using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Anatomy.Organs;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Conditions;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Effects;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Synth;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Prototypes;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUSurgeryRulebookSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private CMSurgerySystem _rmcSurgery = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private SharedCMUSurgeryFlowSystem _flowSurgery = default!;
    [Dependency] private SharedCMUSurgicalTraitSystem _surgicalTraits = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

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

            var eligible = BuildEligibleSurgeries(
                patient,
                childComp.PartType,
                childComp.Symmetry,
                surgeon,
                childId,
                ignoreSkillRequirements: ignoreSkillRequirements);

            var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(childComp.PartType, childComp.Symmetry);
            var conditionSummary = BuildConditionSummary(childId, childComp.PartType);
            var isReattachLock = lockComp is not null && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(lockComp.LeafSurgeryId);
            var isInFlightHere = lockComp is not null
                && lockComp.Part == childId
                && (!isReattachLock
                    || (lockComp.TargetPartType == childComp.PartType && lockComp.TargetSymmetry == childComp.Symmetry));
            var lockedByOtherPart = lockComp is not null && !isInFlightHere;

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
                if (!CMUBodyPartSlots.TryGetSymmetry(slotId, BodyPartSymmetry.None, out var symmetry))
                    continue;
                if (attachedSlots.Contains((slot.Type, symmetry)))
                    continue;

                var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(slot.Type, symmetry);
                var conditionSummary = Loc.GetString("cmu-medical-surgery-condition-missing");
                var eligible = BuildEligibleSurgeries(
                    patient,
                    slot.Type,
                    symmetry,
                    surgeon,
                    null,
                    ignoreSkillRequirements: ignoreSkillRequirements);
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

            var resolveTarget = targetPart;
            if (resolveTarget is null
                && SharedCMUSurgeryFlowSystem.IsReattachSurgeryId(metadata.Surgery))
            {
                if (!_flowSurgery.TryGetReattachAnchorPart(patient, out var anchor))
                    continue;

                resolveTarget = anchor;
            }

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

            entries.Add(BuildEntry(metadata, surgeryProto, resolved));
        }

        TryAddCloseUpEntries(patient, targetPart, partType, lockComp, entries, surgeon);
        return entries;
    }

    public bool HasRequiredSurgerySkill(EntityUid surgeon, int minSkill)
    {
        return minSkill <= 0 || _skills.HasSkill(surgeon, SurgerySkill, minSkill);
    }

    private static CMUSurgeryEntry BuildEntry(
        CMUSurgeryStepMetadataPrototype metadata,
        EntityPrototype surgeryProto,
        CMUResolvedStep resolved)
    {
        return new CMUSurgeryEntry(
            metadata.Surgery,
            metadata.DisplayName ?? surgeryProto.Name,
            resolved.StepLabel,
            resolved.ToolCategory,
            resolved.AbsoluteStepIndex,
            resolved.TotalSteps,
            resolved.GatingSurgeryId,
            metadata.Category);
    }

    private static bool IsSurgicallySupportedPart(BodyPartType type)
    {
        return type is BodyPartType.Head or BodyPartType.Torso or BodyPartType.Arm or BodyPartType.Leg;
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

    private bool IsSurgeryEligible(
        EntityUid patient,
        EntityUid? targetPart,
        EntityPrototype surgeryProto,
        BodyPartType partType,
        EntityUid surgeon)
    {
        var patientIsSynth = HasComp<SynthComponent>(patient);
        var surgeryIsSynth = surgeryProto.HasComponent<RMCSynthSurgeryComponent>();

        if (patientIsSynth != surgeryIsSynth)
            return false;

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
