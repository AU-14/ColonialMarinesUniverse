using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server._RMC14.Medical.Surgery;
using Content.Shared._CMU14.Medical;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Conditions;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
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
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedCMUSurgeryFlowSystem _flowSurgery = default!;

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
            var state = _flowSurgery.BuildBuiState(patient, Name(patient), parts, armed);
            _ui.SetUiState(medic, CMUSurgeryUIKey.Key, state);
        }
    }

    public bool TryDispatch(EntityUid surgeon, EntityUid patient)
    {
        if (!IsLayerEnabled())
            return false;

        if (!HasComp<CMUHumanMedicalComponent>(patient))
            return false;

        var parts = BuildPartEntries(patient, surgeon);
        if (parts.Count == 0)
            return false;

        var marker = EnsureComp<CMUSurgeryWindowOpenComponent>(surgeon);
        marker.Patient = patient;
        // Default fallback only — armed-step messages carry an explicit
        // Part NetEntity that overrides this.
        marker.TargetPartType = parts[0].Type;
        marker.TargetSymmetry = parts[0].Symmetry;
        Dirty(surgeon, marker);

        var armed = CompOrNull<CMUSurgeryArmedStepComponent>(patient);
        var state = _flowSurgery.BuildBuiState(patient, Name(patient), parts, armed);

        _ui.SetUiState(surgeon, CMUSurgeryUIKey.Key, state);
        _ui.OpenUi(surgeon, CMUSurgeryUIKey.Key, surgeon);
        return true;
    }

    public List<CMUSurgeryPartEntry> BuildPartEntries(EntityUid patient, EntityUid surgeon)
    {
        var parts = new List<CMUSurgeryPartEntry>();
        TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp);
        var attachedSlots = new HashSet<(BodyPartType, BodyPartSymmetry)>();

        foreach (var (childId, childComp) in _body.GetBodyChildren(patient))
        {
            if (!IsSurgicallySupportedPart(childComp.PartType))
                continue;

            attachedSlots.Add((childComp.PartType, childComp.Symmetry));

            var eligible = BuildEligibleSurgeries(patient, childComp.PartType, childComp.Symmetry, surgeon, childId);

            var displayName = SharedCMUSurgeryFlowSystem.FormatPartName(childComp.PartType, childComp.Symmetry);
            var conditionSummary = BuildConditionSummary(childId);
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
                var eligible = BuildEligibleSurgeries(patient, slot.Type, symmetry, surgeon, null);
                var lockedByOtherPart = lockComp is not null;

                parts.Add(new CMUSurgeryPartEntry(
                    patientNetEntity,
                    slot.Type,
                    symmetry,
                    displayName,
                    conditionSummary,
                    false,
                    lockedByOtherPart,
                    eligible));
            }
        }

        return parts;
    }

    private static bool IsSurgicallySupportedPart(BodyPartType type) =>
        type is BodyPartType.Head or BodyPartType.Torso or BodyPartType.Arm or BodyPartType.Leg;

    private string BuildConditionSummary(EntityUid part)
    {
        var bits = new List<string>();
        if (HasComp<CMIncisionOpenComponent>(part))
            bits.Add(Loc.GetString("cmu-medical-surgery-condition-incision-open"));
        if (HasComp<CMRibcageOpenComponent>(part))
            bits.Add(Loc.GetString("cmu-medical-surgery-condition-ribcage-open"));
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
        return string.Join(" · ", bits);
    }

    public bool IsLayerEnabled()
    {
        return _cfg.GetCVar(CMUMedicalCCVars.Enabled)
            && _cfg.GetCVar(CMUMedicalCCVars.SurgeryEnabled);
    }

    public List<CMUSurgeryEntry> BuildEligibleSurgeries(EntityUid patient, BodyPartType partType, BodyPartSymmetry symmetry, EntityUid surgeon, EntityUid? targetPart = null)
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

            if (lockComp is not null)
            {
                if (lockComp.Part != targetPart || lockComp.LeafSurgeryId != metadata.Surgery)
                    continue;
            }

            if (!IsSurgeryEligible(patient, targetPart, surgeryProto, partType, surgeon))
                continue;

            // Reattach has no part — markers ride on the patient body.
            var resolveTarget = targetPart;
            if (resolveTarget is null && metadata.Surgery == "CMUSurgeryReattachLimb")
                resolveTarget = patient;

            CMUResolvedStep resolved;
            if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var armedComp)
                && armedComp.LeafSurgeryId == metadata.Surgery)
            {
                if (!_flowSurgery.TryResolveStepAt(armedComp.SurgeryId, armedComp.StepIndex, out resolved))
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

        // Post-abandon cleanup: surface CMSurgeryCloseIncision /
        // CMSurgeryCloseRibcage when V1 markers linger after abandon so a
        // fresh surgeon can finish the cleanup. These don't have V2
        // metadata; labels + tool categories are synthesised from the
        // step prototypes.
        if (lockComp is null && targetPart is { } closePart)
        {
            TryAddCloseUpEntry(patient, closePart, partType, "CMSurgeryCloseRibcage", entries, surgeon);
            TryAddCloseUpEntry(patient, closePart, partType, "CMSurgeryCloseIncision", entries, surgeon);
        }

        return entries;
    }

    private void TryAddCloseUpEntry(EntityUid patient, EntityUid part, BodyPartType partType, string surgeryId, List<CMUSurgeryEntry> entries, EntityUid surgeon)
    {
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
        // Reattach surfaces ONLY on the synthesized missing-slot entries
        // (targetPart is null). Held-limb match enforcement lives in
        // click-target validation, so dispatch can surface reattach
        // unconditionally on the missing slot.
        if (surgeryProto.ID == "CMUSurgeryReattachLimb")
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

        var armed = _flowSurgery.TryArmStep(medic, marker.Patient, targetPart, args.SurgeryId, args.StepIndex, armedType, armedSymmetry);
        if (armed is null)
            return;

        // Re-walk the part list because re-arming may have eliminated some
        // eligible surgeries (e.g. an open-incision step now removes the
        // prerequisite gate from a fracture-set).
        var parts = BuildPartEntries(marker.Patient, medic);
        var state = _flowSurgery.BuildBuiState(marker.Patient, Name(marker.Patient), parts, armed);
        _ui.SetUiState(medic, CMUSurgeryUIKey.Key, state);
    }

    private void OnClearArmedMessage(Entity<CMUSurgeryWindowOpenComponent> ent, ref CMUSurgeryClearArmedMessage args)
    {
        var marker = ent.Comp;
        if (!marker.Patient.IsValid())
            return;
        // BUI cancel = explicit abandon: lift the in-flight lock so a
        // different surgery can be started. V1 physical-state markers stay
        // — a fresh surgeon sees CMSurgeryCloseIncision /
        // CMSurgeryCloseRibcage as cleanup options.
        _flowSurgery.ClearArmed(marker.Patient);
        _flowSurgery.ClearSurgeryInFlight(marker.Patient);

        var parts = BuildPartEntries(marker.Patient, ent.Owner);
        var state = _flowSurgery.BuildBuiState(marker.Patient, Name(marker.Patient), parts, null);
        _ui.SetUiState(ent.Owner, CMUSurgeryUIKey.Key, state);
    }

    private void OnUiClosed(Entity<CMUSurgeryWindowOpenComponent> ent, ref BoundUIClosedEvent args)
    {
        if (args.UiKey is not CMUSurgeryUIKey)
            return;
        // The patient's armed component intentionally stays — the medic
        // may re-open the window or click the patient with the right tool.
        RemComp<CMUSurgeryWindowOpenComponent>(ent.Owner);
    }
}
