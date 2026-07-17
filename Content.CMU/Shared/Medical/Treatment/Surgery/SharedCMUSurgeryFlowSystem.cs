using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Content.Shared._CMU14.DroneOperator;
using Content.Shared._CMU14.Medical.Anatomy.BodyParts;
using Content.Shared._CMU14.Medical.Anatomy.Bones;
using Content.Shared._CMU14.Medical.Core;
using Content.Shared._CMU14.Medical.Injuries.Wounds;
using Content.Shared._CMU14.Medical.Treatment.FirstAid;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Conditions;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Effects;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Markers;
using Content.Shared._CMU14.Medical.Treatment.Surgery.Traits;
using Content.Shared._CMU14.Medical.Injuries.Pain;
using Content.Shared._RMC14.Medical.Surgery;
using Content.Shared._RMC14.Medical.Surgery.Steps;
using Content.Shared._RMC14.Medical.Surgery.Steps.Parts;
using Content.Shared._RMC14.Medical.Surgery.Tools;
using Content.Shared._RMC14.Repairable;
using Content.Shared.Bed.Sleep;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.Smoking;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Medical.Treatment.Surgery;

public abstract partial class SharedCMUSurgeryFlowSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected IComponentFactory ComponentFactory = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected CMUMedicalBodyIndexSystem MedicalIndex = default!;
    [Dependency] protected CMUMedicalSchedulerSystem MedicalScheduler = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected SharedHandsSystem Hands = default!;
    [Dependency] protected ItemToggleSystem ItemToggle = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedPainShockSystem Pain = default!;
    [Dependency] protected CMUSurgerySessionSystem SurgerySessions = default!;
    [Dependency] protected SharedCMUSurgicalTraitSystem SurgicalTraits = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected SharedUserInterfaceSystem UserInterface = default!;
    [Dependency] protected SharedCMSurgerySystem RmcSurgery = default!;

    private CMUSurgeryRegistry _registry = CMUSurgeryRegistry.Empty;
    private ulong _lastArmedStateId;

    private readonly Dictionary<string, Type[]> _toolCategories = new();

    private const float SurgeryPainSuppressionMinimum = 0.5f;
    private const int SurgeryPainSuppressionTierMinimum = 2;
    private const string SurgeryUnconsciousStatus = "StatusEffectCMUUnconscious";
    private const string SurgeryForcedSleepingStatus = "StatusEffectForcedSleeping";
    private const string TieVascularTearSurgery = "CMUSurgeryTieVascularTear";
    private const string ExtractForeignBodySurgery = "CMUSurgeryExtractForeignBody";
    private const string RelieveCompartmentPressureSurgery = "CMUSurgeryRelieveCompartmentPressure";
    private const string DebrideContaminatedWoundSurgery = "CMUSurgeryDebrideContaminatedWound";
    private const string RemoveBoneFragmentsSurgery = "CMUSurgeryRemoveBoneFragments";
    private const string FreeOrganAdhesionsSurgery = "CMUSurgeryFreeOrganAdhesions";
    private const string PackOrganBleedSurgery = "CMUSurgeryPackOrganBleed";
    private static readonly EntProtoId<CMSurgeryComponent> OpenBoneCavitySurgery = "CMUSurgeryOpenBoneCavity";
    private static readonly EntProtoId MendRibcageStep = "CMSurgeryStepMendRibcage";
    private static readonly EntProtoId TieVascularTearStep = "CMUSurgeryStepTieVascularTear";
    private static readonly EntProtoId ExtractForeignBodyStep = "CMUSurgeryStepExtractForeignBody";
    private static readonly EntProtoId RelieveCompartmentPressureStep = "CMUSurgeryStepRelieveCompartmentPressure";
    private static readonly EntProtoId DebrideContaminatedWoundStep = "CMUSurgeryStepDebrideContaminatedWound";
    private static readonly EntProtoId RemoveBoneFragmentsStep = "CMUSurgeryStepRemoveBoneFragments";
    private static readonly EntProtoId FreeOrganAdhesionsStep = "CMUSurgeryStepFreeOrganAdhesions";
    private static readonly EntProtoId PackOrganBleedStep = "CMUSurgeryStepPackOrganBleed";
    private static readonly CMUMedicalWorkKey ArmedStepExpiryWork = new("surgery-armed-step-expiry");
    private static readonly CMUMedicalWorkKey SessionTargetValidationWork = new("surgery-session-target-validation");

    public override void Initialize()
    {
        base.Initialize();

        BuildToolCategoryTable();
        RebuildRegistry();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, InteractUsingEvent>(OnArmedInteractUsing);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, DoAfterAttemptEvent<CMUSurgeryStepDoAfterEvent>>(OnStepDoAfterAttempt);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, CMUSurgeryStepDoAfterEvent>(OnStepDoAfter);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, CMUSurgeryAttemptActorLostEvent>(OnAttemptActorLost);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, CMUMedicalWorkDueEvent>(OnArmedStepExpiryDue);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnSessionBodyPartRemoved);
        SubscribeLocalEvent<BodyComponent, CMUMedicalWorkDueEvent>(OnSessionTargetValidationDue);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<CMUSurgeryStepMetadataPrototype>() && !args.WasModified<EntityPrototype>())
            return;

        var invalidatesLiveSessions = args.WasModified<CMUSurgeryStepMetadataPrototype>();
        if (!invalidatesLiveSessions
            && args.TryGetModified<EntityPrototype>(out var modifiedEntities))
        {
            foreach (var prototypeId in modifiedEntities)
            {
                if (_registry.TryGetDefinition(prototypeId, out _)
                    || _registry.ContainsStep(prototypeId))
                {
                    invalidatesLiveSessions = true;
                    break;
                }
            }
        }

        RebuildRegistry();
        if (!Net.IsServer || !invalidatesLiveSessions)
            return;

        // A live attempt is compiled against one exact step definition. Hot
        // reload invalidates that contract instead of allowing an old action
        // to execute whatever now happens to occupy the same list index.
        var patients = new List<EntityUid>();
        var query = EntityQueryEnumerator<CMUSurgerySessionComponent>();
        while (query.MoveNext(out var patient, out _))
        {
            patients.Add(patient);
        }

        foreach (var patient in patients)
        {
            if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed))
                ClearArmed(patient, armed);
            ClearSurgeryInFlight(patient);
            OnSurgerySessionStateChanged(patient);
        }
    }

    private void RebuildRegistry()
    {
        var metadataBySurgery = new Dictionary<EntProtoId<CMSurgeryComponent>, CMUSurgeryStepMetadataPrototype>();
        var orderedMetadata = new List<CMUSurgeryStepMetadataPrototype>();
        foreach (var metadata in Prototypes.EnumeratePrototypes<CMUSurgeryStepMetadataPrototype>())
        {
            if (!metadataBySurgery.TryAdd(metadata.Surgery, metadata))
            {
                throw new InvalidOperationException(
                    $"Surgery '{metadata.Surgery}' has more than one CMU surgery metadata prototype.");
            }

            orderedMetadata.Add(metadata);
        }

        var definitions = new Dictionary<EntProtoId<CMSurgeryComponent>, CMUSurgeryDefinition>();
        foreach (var prototype in Prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            if (!prototype.TryComp(out CMSurgeryComponent? surgery, ComponentFactory))
                continue;

            var surgeryId = new EntProtoId<CMSurgeryComponent>(prototype.ID);
            metadataBySurgery.TryGetValue(surgeryId, out var metadata);
            definitions.Add(surgeryId, CompileDefinition(surgeryId, prototype, surgery, metadata));
        }

        var metadataDefinitions = ImmutableArray.CreateBuilder<CMUSurgeryDefinition>(orderedMetadata.Count);
        var eligibleByPart = new Dictionary<BodyPartType, List<CMUSurgeryDefinition>>();
        foreach (var metadata in orderedMetadata)
        {
            if (!definitions.TryGetValue(metadata.Surgery, out var definition))
            {
                throw new InvalidOperationException(
                    $"CMU surgery metadata '{metadata.ID}' references unknown surgery '{metadata.Surgery}'.");
            }

            metadataDefinitions.Add(definition);
            foreach (var partType in definition.ValidParts)
            {
                if (!eligibleByPart.TryGetValue(partType, out var eligible))
                {
                    eligible = new List<CMUSurgeryDefinition>();
                    eligibleByPart.Add(partType, eligible);
                }

                eligible.Add(definition);
            }
        }

        _registry = new CMUSurgeryRegistry(
            definitions.ToFrozenDictionary(),
            metadataDefinitions.MoveToImmutable(),
            eligibleByPart.ToFrozenDictionary(
                pair => pair.Key,
                pair => pair.Value.ToImmutableArray()));
    }

    private CMUSurgeryDefinition CompileDefinition(
        EntProtoId<CMSurgeryComponent> surgeryId,
        EntityPrototype surgeryPrototype,
        CMSurgeryComponent surgery,
        CMUSurgeryStepMetadataPrototype? metadata)
    {
        var actualStepIds = new List<EntProtoId<CMSurgeryStepComponent>>(surgery.Steps.Count);
        var actualStepSet = new HashSet<EntProtoId<CMSurgeryStepComponent>>();
        foreach (var untypedStepId in surgery.Steps)
        {
            var stepId = new EntProtoId<CMSurgeryStepComponent>(untypedStepId.Id);
            if (!actualStepSet.Add(stepId))
            {
                throw new InvalidOperationException(
                    $"Surgery '{surgeryId}' contains duplicate step '{stepId}', which cannot be indexed by StepId.");
            }

            actualStepIds.Add(stepId);
        }

        Dictionary<EntProtoId<CMSurgeryStepComponent>, CMUSurgeryStepMetadataEntry>? metadataByStep = null;
        if (metadata is { Steps.Count: > 0 })
        {
            metadataByStep = new Dictionary<EntProtoId<CMSurgeryStepComponent>, CMUSurgeryStepMetadataEntry>();
            foreach (var stepMetadata in metadata.Steps)
            {
                if (!metadataByStep.TryAdd(stepMetadata.StepId, stepMetadata))
                {
                    throw new InvalidOperationException(
                        $"CMU surgery metadata '{metadata.ID}' contains duplicate step metadata for '{stepMetadata.StepId}'.");
                }

                if (!actualStepSet.Contains(stepMetadata.StepId))
                {
                    throw new InvalidOperationException(
                        $"CMU surgery metadata '{metadata.ID}' references unknown step '{stepMetadata.StepId}' " +
                        $"for surgery '{surgeryId}'.");
                }
            }

            foreach (var stepId in actualStepIds)
            {
                if (!metadataByStep.ContainsKey(stepId))
                {
                    throw new InvalidOperationException(
                        $"CMU surgery metadata '{metadata.ID}' is missing step metadata for '{stepId}' " +
                        $"in surgery '{surgeryId}'.");
                }
            }
        }

        var steps = ImmutableArray.CreateBuilder<CMUSurgeryStepDefinition>(actualStepIds.Count);
        var stepsById = new Dictionary<EntProtoId<CMSurgeryStepComponent>, CMUSurgeryStepDefinition>(actualStepIds.Count);
        for (var index = 0; index < actualStepIds.Count; index++)
        {
            var stepId = actualStepIds[index];
            if (!Prototypes.TryIndex<EntityPrototype>(stepId.Id, out var stepPrototype)
                || !stepPrototype.TryComp(out CMSurgeryStepComponent? step, ComponentFactory))
            {
                throw new InvalidOperationException(
                    $"Surgery '{surgeryId}' references unknown surgery step prototype '{stepId}'.");
            }

            CMUSurgeryStepMetadataEntry? stepMetadata = null;
            metadataByStep?.TryGetValue(stepId, out stepMetadata);
            var label = stepMetadata?.Label ?? stepPrototype.Name;
            var toolCategory = stepMetadata is null
                ? ResolveLegacyStepToolCategory(step)
                : stepMetadata.ToolCategory;

            CMUSurgeryOrganCondition? organCondition = null;
            if (stepPrototype.TryComp(out CMUOrganDamagedSurgeryConditionComponent? condition, ComponentFactory))
                organCondition = new CMUSurgeryOrganCondition(condition.OrganSlot, condition.MinStage);

            string? reinsertOrganSlot = null;
            if (stepPrototype.TryComp(out CMUSurgeryStepReinsertOrganEffectComponent? reinsert, ComponentFactory))
                reinsertOrganSlot = reinsert.OrganSlot;

            var definition = new CMUSurgeryStepDefinition(
                stepId,
                index,
                label,
                toolCategory,
                organCondition,
                reinsertOrganSlot);
            steps.Add(definition);
            stepsById.Add(stepId, definition);
        }

        EntProtoId<CMSurgeryComponent>? requirement = default;
        if (surgery.Requirement is { } requirementId)
            requirement = new EntProtoId<CMSurgeryComponent>(requirementId.Id);
        var validParts = metadata?.ValidParts.ToFrozenSet() ?? FrozenSet<BodyPartType>.Empty;
        var selfSurgeryValidParts = metadata?.SelfSurgeryValidParts.ToFrozenSet() ?? FrozenSet<BodyPartType>.Empty;

        return new CMUSurgeryDefinition(
            surgeryId,
            surgeryPrototype,
            surgery.Priority,
            requirement,
            metadata?.DisplayName ?? surgeryPrototype.Name,
            metadata?.Category ?? string.Empty,
            metadata?.MinSkill ?? 0,
            metadata?.AllowSelfSurgery ?? false,
            validParts,
            selfSurgeryValidParts,
            steps.MoveToImmutable(),
            stepsById.ToFrozenDictionary(),
            metadata);
    }

    private void BuildToolCategoryTable()
    {
        _toolCategories.Clear();

        _toolCategories["scalpel"] = new[] { typeof(CMScalpelComponent) };
        _toolCategories["hemostat"] = new[] { typeof(CMHemostatComponent) };
        _toolCategories["retractor"] = new[] { typeof(CMRetractorComponent) };
        _toolCategories["cautery"] = new[] { typeof(CMCauteryComponent) };
        _toolCategories["bone_saw"] = new[] { typeof(CMBoneSawComponent), typeof(CMSurgicalDrillComponent) };
        _toolCategories["bone_setter"] = new[] { typeof(CMBoneSetterComponent) };
        _toolCategories["bone_gel"] = new[] { typeof(CMBoneGelComponent) };
        _toolCategories["bone_graft"] = new[] { typeof(CMUBoneGraftComponent) };
        _toolCategories["fix_o_vein"] = new[] { typeof(CMUFixOVeinComponent) };
        _toolCategories["organ_clamp"] = new[] { typeof(CMUOrganClampComponent) };
        _toolCategories["scalpel_or_burn_kit"] = new[] { typeof(CMUBurnDebridementToolComponent) };
        // Resolver only checks "is this a BodyPart" — the matching-symmetry
        // check (right leg slot ↔ right leg part) lives in
        // OnArmedInteractUsing's reattach-surgery branch.
        _toolCategories["severed_limb"] = new[] { typeof(BodyPartComponent) };
        // Synth surgery tools.
        _toolCategories["blowtorch"] = new[] { typeof(BlowtorchComponent) };
        _toolCategories["cable_coil"] = new[] { typeof(RMCCableCoilComponent) };
    }

    public CMUSurgeryArmedStepComponent? TryArmStep(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        int stepIndex,
        BodyPartType? fallbackType = null,
        BodyPartSymmetry? fallbackSymmetry = null,
        bool allowSamePartInFlightSwitch = false,
        bool allowOptionalHemostasis = false)
    {
        return TryArmStepInternal(
            surgeon,
            patient,
            targetPart,
            surgeryId,
            fallbackType,
            fallbackSymmetry,
            allowSamePartInFlightSwitch,
            allowOptionalHemostasis,
            null);
    }

    /// <summary>
    ///     Arms one exact validated action instead of walking the procedure's
    ///     linear requirement chain. UI-less intent uses this for independent
    ///     access actions such as retracting before clamping bleeders.
    /// </summary>
    public CMUSurgeryArmedStepComponent? TryArmExactStep(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        int stepIndex,
        BodyPartType? fallbackType = null,
        BodyPartSymmetry? fallbackSymmetry = null,
        bool allowSamePartInFlightSwitch = false,
        bool allowOptionalHemostasis = false,
        string? leafSurgeryId = null)
    {
        if (!TryResolveStepAt(surgeryId, stepIndex, out var resolved, targetPart))
            return null;

        return TryArmStepInternal(
            surgeon,
            patient,
            targetPart,
            leafSurgeryId ?? surgeryId,
            fallbackType,
            fallbackSymmetry,
            allowSamePartInFlightSwitch,
            allowOptionalHemostasis,
            resolved);
    }

    private CMUSurgeryArmedStepComponent? TryArmStepInternal(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        BodyPartType? fallbackType,
        BodyPartSymmetry? fallbackSymmetry,
        bool allowSamePartInFlightSwitch,
        bool allowOptionalHemostasis,
        CMUResolvedStep? exactStep)
    {
        // Missing-limb reattach rows do not have a limb entity yet, so they
        // resolve through a real body-part anchor while keeping the missing
        // slot type/symmetry as the logical target.
        if (!CanOperateOnPatient(patient, surgeon, popup: true))
            return null;

        BodyPartType armedType;
        BodyPartSymmetry armedSymmetry;
        var operationPart = targetPart;
        var isReattach = IsReattachSurgeryId(surgeryId);
        if (TryComp<BodyPartComponent>(targetPart, out var partComp)
            && (!isReattach
                || fallbackType is null
                || (partComp.PartType == fallbackType && partComp.Symmetry == fallbackSymmetry)))
        {
            armedType = partComp.PartType;
            armedSymmetry = partComp.Symmetry;
        }
        else if (fallbackType is { } t && fallbackSymmetry is { } s)
        {
            armedType = t;
            armedSymmetry = s;
            if (isReattach && !TryGetReattachAnchorPart(patient, out operationPart))
                return null;
        }
        else
        {
            return null;
        }

        if (surgeon == patient && !CanSelfOperateSurgery(surgeryId, armedType))
        {
            SurgeryConditionPopup(surgeon, "cmu-medical-surgery-self-not-allowed", true);
            return null;
        }

        var requestedSite = new CMUMedicalBodyPartKey(armedType, armedSymmetry);
        if (SurgerySessions.TryGetSession(patient, out var session))
        {
            if (session.Phase == CMUSurgerySessionPhase.Performing)
            {
                if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var performing)
                    && performing.LeafSurgeryId == surgeryId
                    && performing.TargetPartType == armedType
                    && performing.TargetSymmetry == armedSymmetry)
                {
                    return performing;
                }

                return null;
            }

            if (session.Site != requestedSite)
            {
                // No step has committed yet, so an idle first attempt may be
                // replaced without leaving an invisible site lock behind.
                if (HasComp<CMUSurgeryInProgressComponent>(patient))
                    return null;

                SurgerySessions.EndSession(patient);
            }
        }

        // Patient-level lock: only one in-flight surgery per patient. A
        // mismatch refuses the arm so the BUI can surface "finish or abandon"
        // instead of silently switching surgeries.
        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
        {
            if (lockComp.Part != operationPart)
                return null;
            if (!allowSamePartInFlightSwitch
                && !lockComp.AwaitingClosureChoice
                && lockComp.LeafSurgeryId != surgeryId)
                return null;
            // Reattach may share the same socket anchor for several missing
            // slots, so pin the in-flight surgery to the slot it started on.
            if (isReattach
                && (lockComp.TargetPartType != armedType || lockComp.TargetSymmetry != armedSymmetry))
                return null;
        }

        if (TryComp<CMUSurgeryArmedStepComponent>(patient, out var existing)
            && existing.LeafSurgeryId == surgeryId
            && existing.TargetPartType == armedType
            && existing.TargetSymmetry == armedSymmetry
            && (exactStep is null
                || existing.SurgeryId == exactStep.Value.ResolvedSurgeryId
                && existing.StepIndex == exactStep.Value.StepIndex))
        {
            existing.LastOperator = surgeon;
            existing.AllowOptionalHemostasis = allowOptionalHemostasis;
            RefreshArmedStateId(existing);
            existing.ArmedAt = Timing.CurTime;
            Dirty(patient, existing);
            ScheduleArmedExpiry(patient, existing);
            return existing;
        }

        // Resolve via the requirement chain so prereqs (open-incision,
        // open-ribcage, etc.) can't be skipped. Legacy RMC prereqs without
        // a CMU metadata entry get a synthesized label from the step proto.
        CMUResolvedStep resolved;
        if (exactStep is { } exact)
        {
            resolved = exact;
        }
        else if (!TryResolveNextStep(
                     patient,
                     operationPart,
                     surgeryId,
                     out resolved,
                     allowOptionalHemostasis))
        {
            return null;
        }

        var preserveLeafProgress = existing is not null
            && existing.LeafSurgeryId == surgeryId
            && existing.TargetPartType == armedType
            && existing.TargetSymmetry == armedSymmetry;
        var armed = EnsureComp<CMUSurgeryArmedStepComponent>(patient);
        armed.LastOperator = surgeon;
        RefreshArmedStateId(armed);
        // SurgeryId = resolved (drives V1 step-event raise in RunStepEffect).
        // LeafSurgeryId = what the medic picked (drives BUI display).
        armed.SurgeryId = resolved.ResolvedSurgeryId;
        armed.StepIndex = resolved.StepIndex;
        armed.TargetPartType = armedType;
        armed.TargetSymmetry = armedSymmetry;
        armed.RequiredToolCategory = resolved.ToolCategory;
        armed.StepLabel = resolved.StepLabel;
        armed.LeafSurgeryId = surgeryId;
        armed.AllowOptionalHemostasis = allowOptionalHemostasis;
        if (!preserveLeafProgress)
            armed.LastCompletedLeafStepIndex = -1;
        armed.ArmedAt = Timing.CurTime;
        Dirty(patient, armed);
        ScheduleArmedExpiry(patient, armed);
        return armed;
    }

    public void EnsureSurgeryInFlight(EntityUid patient, EntityUid part, EntityUid surgeon, string leafSurgeryId, string leafDisplayName, BodyPartType targetType = default, BodyPartSymmetry targetSymmetry = default)
    {
        var lockComp = EnsureComp<CMUSurgeryInProgressComponent>(patient);
        var alreadyInFlight = lockComp.LeafSurgeryId == leafSurgeryId && lockComp.Part == part;
        var previousPart = lockComp.Part;
        if (previousPart.IsValid()
            && previousPart != part
            && HasComp<CMUSurgeryInFlightComponent>(previousPart))
        {
            RemComp<CMUSurgeryInFlightComponent>(previousPart);
        }

        lockComp.Part = part;
        lockComp.LeafSurgeryId = leafSurgeryId;
        lockComp.TargetPartType = targetType;
        lockComp.TargetSymmetry = targetSymmetry;
        lockComp.AwaitingClosureChoice = false;
        Dirty(patient, lockComp);

        var inFlight = EnsureComp<CMUSurgeryInFlightComponent>(part);
        inFlight.LeafSurgeryId = leafSurgeryId;
        inFlight.LeafSurgeryDisplayName = leafDisplayName;
        inFlight.Surgeon = surgeon;
        inFlight.SurgeonName = Name(surgeon);
        if (!alreadyInFlight)
            inFlight.StartedAt = Timing.CurTime;
        Dirty(part, inFlight);
    }

    public bool SetAwaitingClosureChoice(EntityUid patient, EntityUid part)
    {
        if (!TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
            return false;
        if (lockComp.Part != part)
            return false;

        lockComp.AwaitingClosureChoice = true;
        Dirty(patient, lockComp);
        SurgerySessions.SetAwaitingDecision(patient);
        return true;
    }

    public void ClearSurgeryInFlight(EntityUid patient)
    {
        MedicalScheduler.Cancel(patient, SessionTargetValidationWork);
        SurgerySessions.EndSession(patient);

        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp))
        {
            ClearAbandonedReattachState(patient, lockComp);

            if (lockComp.Part.IsValid() && HasComp<CMUSurgeryInFlightComponent>(lockComp.Part))
                RemComp<CMUSurgeryInFlightComponent>(lockComp.Part);
            RemComp<CMUSurgeryInProgressComponent>(patient);
        }
    }

    private void ClearAbandonedReattachState(EntityUid patient, CMUSurgeryInProgressComponent lockComp)
    {
        // Reattach starts on a real socket anchor because the target limb
        // does not exist yet. If that temporary flow is abandoned before the
        // limb is attached, remove the progress markers so another missing
        // slot cannot inherit them. Once the limb exists, the normal open
        // part state should remain so it can still be closed.
        if (!IsReattachSurgeryId(lockComp.LeafSurgeryId))
            return;

        if (TryComp<BodyPartComponent>(lockComp.Part, out var part)
            && part.PartType == lockComp.TargetPartType
            && part.Symmetry == lockComp.TargetSymmetry)
        {
            return;
        }

        if (lockComp.Part.IsValid())
            ClearReattachMarkers(lockComp.Part);
        if (lockComp.Part != patient)
            ClearReattachMarkers(patient);
    }

    private void ClearReattachMarkers(EntityUid uid)
    {
        RemComp<CMIncisionOpenComponent>(uid);
        RemComp<CMBleedersClampedComponent>(uid);
        RemComp<CMSkinRetractedComponent>(uid);
        RemComp<CMUStumpRemovedComponent>(uid);
        RemComp<CMUReattachPreppedComponent>(uid);
        RemComp<CMUReattachCompleteComponent>(uid);
    }

    public void ClearArmed(
        EntityUid patient,
        CMUSurgeryArmedStepComponent? armed = null,
        bool expired = false,
        bool popup = true)
    {
        if (!Resolve(patient, ref armed, false))
            return;

        var surgeon = armed.LastOperator;
        SurgerySessions.CancelActiveAttempt(patient);
        if (!HasComp<CMUSurgeryInProgressComponent>(patient))
            SurgerySessions.EndSession(patient);
        MedicalScheduler.Cancel(patient, SessionTargetValidationWork);
        MedicalScheduler.Cancel(patient, ArmedStepExpiryWork);
        RemComp<CMUSurgeryArmedStepComponent>(patient);

        if (popup && Net.IsServer && surgeon.IsValid())
        {
            var msg = expired
                ? "cmu-medical-surgery-armed-expired"
                : "cmu-medical-surgery-armed-cancelled";
            Popup.PopupEntity(Loc.GetString(msg), surgeon, surgeon, PopupType.SmallCaution);
        }

        OnSurgerySessionStateChanged(patient);
    }

    public bool TryCancelPendingAmputation(EntityUid patient, EntityUid user, EntityUid targetPart)
    {
        if (!TryComp<CMUSurgeryArmedStepComponent>(patient, out var armed)
            || armed.LeafSurgeryId != "CMUSurgeryRemoveLimb"
            || !TryComp<BodyPartComponent>(targetPart, out var bodyPart)
            || bodyPart.PartType != armed.TargetPartType
            || bodyPart.Symmetry != armed.TargetSymmetry)
        {
            return false;
        }

        ClearArmed(patient, armed, popup: false);
        ClearSurgeryInFlight(patient);
        SurgeryConditionPopup(user, "cmu-medical-surgery-amputation-cancelled", true);
        return true;
    }

    /// <summary>
    ///     Replaces the sparse expiry deadline whenever an armed surgery step is refreshed or advanced.
    /// </summary>
    protected void ScheduleArmedExpiry(EntityUid patient, CMUSurgeryArmedStepComponent armed)
    {
        if (Net.IsServer)
            MedicalScheduler.Schedule(patient, ArmedStepExpiryWork, armed.ArmedAt + armed.ExpireAfter);
    }

    private void OnArmedStepExpiryDue(
        Entity<CMUSurgeryArmedStepComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != ArmedStepExpiryWork)
            return;

        var expiresAt = ent.Comp.ArmedAt + ent.Comp.ExpireAfter;
        if (expiresAt > Timing.CurTime)
        {
            MedicalScheduler.Schedule(ent.Owner, ArmedStepExpiryWork, expiresAt);
            return;
        }

        ClearArmed(ent.Owner, ent.Comp, expired: true);
    }

    public bool CanOperateOnPatient(EntityUid patient, EntityUid surgeon, bool popup = false)
    {
        if (HasComp<CMUAutodocContainedPatientComponent>(patient))
            return true;

        if (RmcSurgery.IsLyingDown(patient))
            return true;

        if (patient == surgeon && IsBuckledToStrap(patient))
            return true;

        if (patient == surgeon)
        {
            SurgeryConditionPopup(surgeon, "cmu-medical-surgery-self-not-secured", popup);
            return false;
        }

        SurgeryConditionPopup(surgeon, "cmu-medical-surgery-patient-not-lying", popup);
        return false;
    }

    public CMUSurgicalSiteState GetSiteState(EntityUid part)
    {
        if (!HasComp<CMIncisionOpenComponent>(part))
            return new CMUSurgicalSiteState(CMUSurgicalAccess.Closed, CMUSurgicalHemostasis.None);

        var hemostasis = HasComp<CMBleedersClampedComponent>(part)
            ? CMUSurgicalHemostasis.Clamped
            : CMUSurgicalHemostasis.Uncontrolled;

        if (!HasComp<CMSkinRetractedComponent>(part))
            return new CMUSurgicalSiteState(CMUSurgicalAccess.Incised, hemostasis);

        var access = CMUSurgicalAccess.Shallow;
        if (HasComp<CMRibcageOpenComponent>(part)
            || TryComp<FractureComponent>(part, out var fracture)
            && fracture.Severity is FractureSeverity.Compound or FractureSeverity.Shattered)
        {
            access = CMUSurgicalAccess.Deep;
        }
        else if (HasComp<CMRibcageSawedComponent>(part))
        {
            access = CMUSurgicalAccess.BoneCut;
        }

        return new CMUSurgicalSiteState(access, hemostasis);
    }

    private void SurgeryConditionPopup(EntityUid user, string locKey, bool popup)
    {
        if (!popup || !Net.IsServer)
            return;

        Popup.PopupEntity(Loc.GetString(locKey), user, user, PopupType.SmallCaution);
    }

    private bool IsPainControlledForSurgery(EntityUid patient)
    {
        if (TryComp<MobStateComponent>(patient, out var mobState)
            && mobState.CurrentState != MobState.Alive)
        {
            return true;
        }

        if (HasComp<SleepingComponent>(patient)
            || Status.HasStatusEffect(patient, SurgeryForcedSleepingStatus)
            || Status.HasStatusEffect(patient, SurgeryUnconsciousStatus))
        {
            return true;
        }

        return HasPainSuppressionForSurgery(patient);
    }

    private bool HasPainSuppressionForSurgery(EntityUid patient)
    {
        return Pain.GetAccumulationSuppression(patient) >= SurgeryPainSuppressionMinimum
            || Pain.GetTierSuppression(patient) >= SurgeryPainSuppressionTierMinimum;
    }

    private bool ShouldRejectSurgeryStepForPain(EntityUid patient)
    {
        if (IsPainControlledForSurgery(patient))
            return false;

        return TryComp<PainShockComponent>(patient, out var pain)
            && Pain.GetEffectiveTier(patient, pain) >= PainTier.Severe;
    }

    private bool IsBuckledToStrap(EntityUid patient)
    {
        return TryComp<BuckleComponent>(patient, out var buckle)
            && buckle.BuckledTo is { } strapUid
            && HasComp<StrapComponent>(strapUid);
    }

    private void OnArmedInteractUsing(Entity<CMUSurgeryArmedStepComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var (patient, armed) = ent;

        if (!TryHandleArmedToolUse(patient, armed, args.User, args.Used, args.Target, out var handled, out _))
            return;

        args.Handled = handled;
    }

    public bool TryHandleArmedToolUse(
        EntityUid patient,
        CMUSurgeryArmedStepComponent armed,
        EntityUid user,
        EntityUid used,
        EntityUid? clickTarget,
        out bool handled,
        out bool started)
    {
        handled = false;
        started = false;

        var isRightTool = ToolMatchesCategory(used, armed.RequiredToolCategory);
        var hasWrongDamage = TryGetWrongToolDamage(used, out var damageType, out var amount);

        // Non-surgery items (analyzer, bandage, meds, etc.) pass through
        // so the medic can still treat the patient between steps.
        if (!isRightTool && !hasWrongDamage)
            return false;
        // A wrong-tool scalpel click is also the normal way to reopen the
        // surgery menu. Let the surgery dispatch path handle that click
        // instead of cutting the patient.
        if (!isRightTool && HasComp<CMScalpelComponent>(used))
            return false;

        handled = true;

        if (!CanOperateOnPatient(patient, user, popup: true))
            return true;

        var hasTargetPart = TryFindClickedPart(patient, clickTarget, armed.TargetPartType, armed.TargetSymmetry, out var targetPart);
        if (!hasTargetPart && !TryResolveReattachAnchorForUse(patient, clickTarget, armed, out targetPart))
        {
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-part"), patient, user, PopupType.SmallCaution);
            return true;
        }

        if (isRightTool)
        {
            if (!CanStartArmedProcedure(patient, armed, user))
                return true;

            if (!TryResolveArmedStepEntity(armed, out var stepEnt))
            {
                ClearArmed(patient, armed);
                return true;
            }

            if (!RmcSurgery.CanPerformStep(user, patient, armed.TargetPartType, stepEnt, true, used, out var popup, out var reason, out _))
            {
                ShowStepInvalidPopup(patient, user, armed.TargetPartType, reason, popup);

                return true;
            }

            if (armed.RequiredToolCategory == "severed_limb"
                && !LimbMatchesMissingSlot(patient, used, armed.TargetPartType, armed.TargetSymmetry))
            {
                Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-limb"), patient, user, PopupType.SmallCaution);
                return true;
            }

            if (RequiresActivatedSurgeryTool(used, armed.RequiredToolCategory))
            {
                Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-welder-not-lit"), patient, user, PopupType.SmallCaution);
                return true;
            }

            if (ShouldRejectSurgeryStepForPain(patient))
            {
                ShowSurgeryPainFailure(patient, user, applyReaction: false);
                return true;
            }

            if (Net.IsServer)
            {
                var previousOperator = armed.LastOperator;
                armed.LastOperator = user;
                Dirty(patient, armed);
                started = StartStepDoAfter(patient, armed, user, used, targetPart);
                if (!started)
                {
                    armed.LastOperator = previousOperator;
                    Dirty(patient, armed);
                }
                else
                {
                    armed.ArmedAt = Timing.CurTime;
                    Dirty(patient, armed);
                    ScheduleArmedExpiry(patient, armed);
                    OnSurgerySessionStateChanged(patient);
                }
            }
            return true;
        }

        ApplyWrongToolDamage(user, patient, used, damageType, amount);
        return true;
    }

    private void ShowStepInvalidPopup(EntityUid patient, EntityUid user, BodyPartType partType, StepInvalidReason reason, string? existingPopup)
    {
        if (existingPopup is not null)
            return;

        var locKey = reason switch
        {
            StepInvalidReason.MissingSkills => "cmu-medical-surgery-missing-skills",
            StepInvalidReason.NeedsOperatingTable => "cmu-medical-surgery-needs-operating-table",
            StepInvalidReason.Armor => partType == BodyPartType.Head
                ? "cmu-medical-surgery-remove-helmet"
                : "cmu-medical-surgery-remove-armor",
            StepInvalidReason.MissingTool => "cmu-medical-surgery-wrong-tool",
            _ => null,
        };

        if (locKey is null)
            return;

        Popup.PopupEntity(Loc.GetString(locKey), patient, user, PopupType.SmallCaution);
    }

    private bool TryResolveArmedStepEntity(CMUSurgeryArmedStepComponent armed, out EntityUid stepEnt)
    {
        stepEnt = default;

        if (!TryGetDefinition(armed.SurgeryId, out var surgery)
            || !surgery.TryGetStepAt(armed.StepIndex, out var step))
            return false;
        if (RmcSurgery.GetSingleton(step.Id) is not { } resolvedStepEnt)
            return false;

        stepEnt = resolvedStepEnt;
        return true;
    }

    private bool RequiresActivatedSurgeryTool(EntityUid tool, string? requiredToolCategory)
    {
        if (requiredToolCategory is not ("cautery" or "blowtorch"))
            return false;

        if (TryComp<SmokableComponent>(tool, out var smokable))
            return smokable.State != SmokableState.Lit;

        return HasComp<BlowtorchComponent>(tool) || HasComp<ItemToggleHotComponent>(tool)
            ? !ItemToggle.IsActivated(tool)
            : false;
    }

    private bool TryResolveReattachAnchorForUse(EntityUid patient, EntityUid? clickTarget, CMUSurgeryArmedStepComponent armed, out EntityUid anchor)
    {
        anchor = default;
        if (!IsReattachSurgeryId(armed.LeafSurgeryId))
            return false;
        if (!TryGetReattachAnchorPart(patient, out anchor))
            return false;

        return clickTarget is null || clickTarget == patient || clickTarget == anchor;
    }

    public static bool IsReattachSurgeryId(string surgeryId)
    {
        return surgeryId == "CMUSurgeryReattachLimb" || surgeryId == "RMCSynthSurgeryReattachLimb";
    }

    /// <summary>
    ///     Override in the sealed server class so prediction rollback can't
    ///     re-raise the step event on the client.
    /// </summary>
    protected virtual bool StartStepDoAfter(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid tool, EntityUid targetPart)
    {
        return false;
    }

    protected virtual bool CanStartArmedProcedure(
        EntityUid patient,
        CMUSurgeryArmedStepComponent armed,
        EntityUid surgeon)
    {
        return true;
    }

    protected virtual void OnSurgerySessionStateChanged(EntityUid patient)
    {
    }

    protected void RefreshArmedStateId(CMUSurgeryArmedStepComponent armed)
    {
        if (!Net.IsServer)
            return;

        _lastArmedStateId = unchecked(_lastArmedStateId + 1);
        if (_lastArmedStateId == 0)
            _lastArmedStateId = 1;

        armed.StateId = new CMUSurgeryArmedStateId(_lastArmedStateId);
    }

    protected virtual void ApplyWrongToolDamage(EntityUid surgeon, EntityUid patient, EntityUid tool, string damageType, float amount)
    {
    }

    protected virtual void ApplySurgeryPainFailureFeedback(EntityUid patient)
    {
    }

    private void ShowSurgeryPainFailure(EntityUid patient, EntityUid surgeon, bool applyReaction)
    {
        if (!Net.IsServer)
            return;

        if (applyReaction)
            ApplySurgeryPainFailureFeedback(patient);

        Popup.PopupEntity(
            Loc.GetString("cmu-medical-surgery-step-pain-uncontrolled"),
            surgeon,
            surgeon,
            PopupType.MediumCaution);
    }

    /// <summary>
    ///     Server-only — raises V1 <c>CMSurgeryStepEvent</c> + either re-arms
    ///     or raises <c>CMSurgeryCompleteEvent</c>. Shared no-ops so
    ///     prediction rollback can't double-apply state mutations.
    /// </summary>
    protected virtual void RunStepEffect(
        EntityUid patient,
        CMUSurgeryArmedStepComponent armed,
        EntityUid surgeon,
        EntityUid? tool,
        EntityUid? targetPart,
        EntProtoId<CMSurgeryStepComponent>? committedStep = null)
    {
    }

    public bool TryCompleteAutomatedStep(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon)
    {
        if (!Net.IsServer)
            return false;

        if (!CanOperateOnPatient(patient, surgeon, popup: true)
            || !CanStartArmedProcedure(patient, armed, surgeon))
            return false;

        EntityUid targetPart;
        if (TryFindClickedPart(patient, null, armed.TargetPartType, armed.TargetSymmetry, out var foundPart))
        {
            targetPart = foundPart;
        }
        else if (TryResolveReattachAnchorForUse(patient, null, armed, out var anchor))
        {
            targetPart = anchor;
        }
        else
        {
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-part"), patient, surgeon, PopupType.SmallCaution);
            ClearArmed(patient, armed);
            return false;
        }

        RunStepEffect(patient, armed, surgeon, null, targetPart);
        return true;
    }

    private void OnStepDoAfterAttempt(Entity<CMUSurgeryArmedStepComponent> ent, ref DoAfterAttemptEvent<CMUSurgeryStepDoAfterEvent> args)
    {
        var (patient, armed) = ent;
        var ev = args.Event;

        if (!ArmedMatchesDoAfter(armed, ev)
            || (Net.IsServer && !SurgerySessions.IsAttemptCurrent(patient, ev.Attempt, ev.User, ev.Used, ev.Target, ev.StepId))
            || (Net.IsServer && !IsAttemptTargetStillValid(patient, armed, ev.Target))
            || !CanOperateOnPatient(patient, ev.User)
            || ShouldRejectSurgeryStepForPain(patient))
        {
            args.Cancel();
        }
    }

    private void OnStepDoAfter(Entity<CMUSurgeryArmedStepComponent> ent, ref CMUSurgeryStepDoAfterEvent args)
    {
        var (patient, armed) = ent;

        if (!ArmedMatchesDoAfter(armed, args))
            return;

        if (!Net.IsServer
            || !SurgerySessions.IsAttemptCurrent(patient, args.Attempt, args.User, args.Used, args.Target, args.StepId))
        {
            return;
        }

        if (args.Cancelled)
        {
            if (SurgerySessions.TryConsumeAttempt(patient, args.Attempt, args.User, args.Used, args.Target, args.StepId))
            {
                if (!IsAttemptTargetStillValid(patient, armed, args.Target))
                {
                    AbandonInvalidTarget(patient, armed);
                    return;
                }

                if (ShouldRejectSurgeryStepForPain(patient))
                    ShowSurgeryPainFailure(patient, args.User, applyReaction: true);

                ReturnToAwaitingAction(patient, armed);
            }
            return;
        }

        if (args.Handled)
            return;
        args.Handled = true;

        if (!CanOperateOnPatient(patient, args.User, popup: true))
        {
            SurgerySessions.TryConsumeAttempt(patient, args.Attempt, args.User, args.Used, args.Target, args.StepId);
            ReturnToAwaitingAction(patient, armed);
            return;
        }

        if (ShouldRejectSurgeryStepForPain(patient))
        {
            ShowSurgeryPainFailure(patient, args.User, applyReaction: true);
            SurgerySessions.TryConsumeAttempt(patient, args.Attempt, args.User, args.Used, args.Target, args.StepId);
            ReturnToAwaitingAction(patient, armed);
            return;
        }

        if (!IsAttemptTargetStillValid(patient, armed, args.Target))
        {
            if (SurgerySessions.TryConsumeAttempt(patient, args.Attempt, args.User, args.Used, args.Target, args.StepId))
                AbandonInvalidTarget(patient, armed);
            return;
        }

        if (!SurgerySessions.TryConsumeAttempt(patient, args.Attempt, args.User, args.Used, args.Target, args.StepId))
            return;

        RunStepEffect(patient, armed, args.User, args.Used, args.Target, args.StepId);
    }

    private void ReturnToAwaitingAction(EntityUid patient, CMUSurgeryArmedStepComponent armed)
    {
        RefreshArmedStateId(armed);
        armed.ArmedAt = Timing.CurTime;
        Dirty(patient, armed);
        ScheduleArmedExpiry(patient, armed);
        OnSurgerySessionStateChanged(patient);
    }

    private void OnAttemptActorLost(
        Entity<CMUSurgeryArmedStepComponent> ent,
        ref CMUSurgeryAttemptActorLostEvent args)
    {
        ReturnToAwaitingAction(ent.Owner, ent.Comp);
    }

    private void OnSessionBodyPartRemoved(Entity<BodyComponent> ent, ref BodyPartRemovedEvent args)
    {
        if (Net.IsServer && SurgerySessions.TryGetSession(ent.Owner, out _))
            MedicalScheduler.Schedule(ent.Owner, SessionTargetValidationWork, Timing.CurTime);
    }

    private void OnSessionTargetValidationDue(Entity<BodyComponent> ent, ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != SessionTargetValidationWork
            || !SurgerySessions.TryGetSession(ent.Owner, out var session))
        {
            return;
        }

        bool targetAttached;
        if (TryComp<CMUSurgeryInProgressComponent>(ent.Owner, out var inProgress))
        {
            targetAttached = IsIndexedBodyPart(ent.Owner, inProgress.Part);
        }
        else if (session.ActiveTarget is { } activeTarget)
        {
            targetAttached = IsIndexedBodyPart(ent.Owner, activeTarget);
        }
        else if ((TryComp<CMUSurgeryArmedStepComponent>(ent.Owner, out var armed)
                  && IsReattachSurgeryId(armed.LeafSurgeryId))
                 || IsReattachSurgeryId(session.Procedure.Id))
        {
            targetAttached = TryGetReattachAnchorPart(ent.Owner, out var anchor)
                && IsIndexedBodyPart(ent.Owner, anchor);
        }
        else
        {
            targetAttached = MedicalIndex.TryGetBodyPart(ent.Owner, session.Site, out _);
        }

        if (targetAttached)
            return;

        if (TryComp<CMUSurgeryArmedStepComponent>(ent.Owner, out var currentArmed))
            ClearArmed(ent.Owner, currentArmed);
        ClearSurgeryInFlight(ent.Owner);
        OnSurgerySessionStateChanged(ent.Owner);
    }

    private bool IsIndexedBodyPart(EntityUid patient, EntityUid part)
    {
        foreach (var indexed in MedicalIndex.GetBodyParts(patient))
        {
            if (indexed.Owner == part)
                return true;
        }

        return false;
    }

    private bool IsAttemptTargetStillValid(
        EntityUid patient,
        CMUSurgeryArmedStepComponent armed,
        EntityUid? target)
    {
        if (target is not { } targetUid)
            return false;

        if (TryFindClickedPart(
                patient,
                targetUid,
                armed.TargetPartType,
                armed.TargetSymmetry,
                out var currentPart))
        {
            return currentPart == targetUid;
        }

        return TryResolveReattachAnchorForUse(patient, targetUid, armed, out var anchor)
            && anchor == targetUid;
    }

    private void AbandonInvalidTarget(EntityUid patient, CMUSurgeryArmedStepComponent armed)
    {
        ClearArmed(patient, armed);
        ClearSurgeryInFlight(patient);
        OnSurgerySessionStateChanged(patient);
    }

    private bool ArmedMatchesDoAfter(CMUSurgeryArmedStepComponent armed, CMUSurgeryStepDoAfterEvent args)
    {
        if (armed.SurgeryId != args.SurgeryId
            || armed.LeafSurgeryId != args.LeafSurgeryId
            || armed.StepIndex != args.StepIndex
            || armed.TargetPartType != args.TargetPartType
            || armed.TargetSymmetry != args.TargetSymmetry)
        {
            return false;
        }

        return TryGetDefinition(armed.SurgeryId, out var surgery)
            && surgery.TryGetStepAt(armed.StepIndex, out var step)
            && step.Id == args.StepId;
    }

    /// <summary>
    ///     Gets the optional CMU metadata associated with a compiled surgery definition.
    /// </summary>
    public bool TryGetMetadata(string surgeryId, out CMUSurgeryStepMetadataPrototype metadata)
    {
        if (_registry.TryGetDefinition(surgeryId, out var definition)
            && definition.Metadata is { } source)
        {
            metadata = source;
            return true;
        }

        metadata = default!;
        return false;
    }

    /// <summary>
    ///     Gets an immutable surgery definition from the current validated registry.
    /// </summary>
    public bool TryGetDefinition(string surgeryId, out CMUSurgeryDefinition definition)
    {
        return _registry.TryGetDefinition(surgeryId, out definition!);
    }

    /// <summary>
    ///     Gets the pre-indexed surgery definitions eligible for a body-part type.
    /// </summary>
    public ImmutableArray<CMUSurgeryDefinition> GetEligibleDefinitions(BodyPartType partType)
    {
        return _registry.GetEligibleDefinitions(partType);
    }

    public bool CanSelfOperateSurgery(string surgeryId, BodyPartType partType)
    {
        if (!TryGetDefinition(surgeryId, out var definition) || definition.Metadata is null)
            return IsSelfCloseUpSurgery(surgeryId, partType);

        if (!definition.AllowSelfSurgery)
            return false;

        var validParts = definition.SelfSurgeryValidParts.Count > 0
            ? definition.SelfSurgeryValidParts
            : definition.ValidParts;

        return validParts.Contains(partType);
    }

    private static bool IsSelfCloseUpSurgery(string surgeryId, BodyPartType partType)
    {
        if (!IsSelfSurgeryPart(partType))
            return false;

        return surgeryId is "CMUSurgeryCloseIncision"
            or "CMUSurgeryCloseBoneCavity"
            or "CMSurgeryCloseIncision"
            or "CMSurgeryCloseRibcage";
    }

    private static bool IsSelfSurgeryPart(BodyPartType partType)
    {
        return partType is BodyPartType.Arm
            or BodyPartType.Hand
            or BodyPartType.Leg
            or BodyPartType.Foot;
    }

    public IEnumerable<CMUSurgeryStepMetadataPrototype> EnumerateMetadata()
    {
        foreach (var definition in _registry.MetadataDefinitions)
        {
            if (definition.Metadata is { } metadata)
                yield return metadata;
        }
    }

    /// <summary>
    ///     Walks RMC's <c>GetNextStep</c> — honours the <c>Requirement</c>
    ///     chain so picking "Set Compound Fracture" arms open-incision first
    ///     when the part isn't yet incised.
    /// </summary>
    public bool TryResolveNextStep(
        EntityUid patient,
        EntityUid? targetPart,
        string surgeryId,
        out CMUResolvedStep resolved,
        bool allowOptionalHemostasis = false)
    {
        resolved = default!;
        if (targetPart is null)
            return false;

        if (TryResolveReattachNextStep(
                patient,
                targetPart.Value,
                surgeryId,
                out resolved,
                allowOptionalHemostasis))
            return true;

        if (allowOptionalHemostasis
            && TryResolveAccessibleInternalBleedNextStep(patient, targetPart.Value, surgeryId, out resolved))
        {
            return true;
        }

        if (TryResolveBrokenCavityAccessNextStep(
                patient,
                targetPart.Value,
                surgeryId,
                allowOptionalHemostasis,
                out resolved))
        {
            return true;
        }

        if (allowOptionalHemostasis
            && TryResolveSoftTissueAccessNextStep(patient, targetPart.Value, surgeryId, out resolved))
        {
            return true;
        }

        if (RmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt)
            return false;

        var next = RmcSurgery.GetNextStep(patient, targetPart.Value, surgeryEnt);
        if (next is null)
            return false; // surgery already complete on this part — nothing to arm.

        var (resolvedSurgery, stepIdx) = next.Value;
        var resolvedSurgeryProtoId = MetaData(resolvedSurgery.Owner).EntityPrototype?.ID;
        if (resolvedSurgeryProtoId is null)
            return false;

        if (ShouldInjectSurgicalTraits(surgeryId, resolvedSurgeryProtoId)
            && TryResolveSurgicalTraitCleanupStep(targetPart.Value, surgeryId, out var traitStep))
        {
            resolved = traitStep;
            return true;
        }

        var stepProtoId = resolvedSurgery.Comp.Steps[stepIdx];
        var typedStepId = new EntProtoId<CMSurgeryStepComponent>(stepProtoId.Id);
        if (!TryGetDefinition(resolvedSurgeryProtoId, out var definition)
            || !definition.TryGetStep(typedStepId, out var step))
            return false;

        resolved = new CMUResolvedStep(
            resolvedSurgeryProtoId,
            stepIdx,
            ResolveContextualStepLabel(step.Id, step.Label, targetPart),
            step.ToolCategory,
            definition.Steps.Length,
            // Gating prereq id only when the leaf surgery isn't the one
            // being armed - lets the BUI flag "(via Open Incision)".
            resolvedSurgeryProtoId == surgeryId ? null : resolvedSurgeryProtoId);
        return true;
    }

    private bool TryResolveSoftTissueAccessNextStep(
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        out CMUResolvedStep resolved)
    {
        var access = GetSiteState(targetPart).Access;
        if (!RequiresSoftTissueAccess(surgeryId)
            || access is not (CMUSurgicalAccess.Shallow or CMUSurgicalAccess.BoneCut or CMUSurgicalAccess.Deep))
        {
            resolved = default!;
            return false;
        }

        if (ShouldInjectSurgicalTraits(surgeryId, surgeryId)
            && TryResolveSurgicalTraitCleanupStep(targetPart, surgeryId, out resolved))
        {
            return true;
        }

        return TryResolveIncompleteStepFromIndex(patient, targetPart, surgeryId, 0, out resolved);
    }

    private bool RequiresSoftTissueAccess(string surgeryId)
    {
        return TryGetDefinition(surgeryId, out var surgery)
            && surgery.Requirement is { } requirement
            && requirement == "CMUSurgeryOpenSoftTissue";
    }

    private bool TryResolveAccessibleInternalBleedNextStep(
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        out CMUResolvedStep resolved)
    {
        var access = GetSiteState(targetPart).Access;
        if (surgeryId != "CMUSurgeryCauterizeInternalBleeding"
            || !HasComp<InternalBleedingComponent>(targetPart)
            || access is not (CMUSurgicalAccess.Shallow or CMUSurgicalAccess.Deep))
        {
            resolved = default!;
            return false;
        }

        return TryResolveIncompleteStepFromIndex(patient, targetPart, surgeryId, 0, out resolved);
    }

    private bool TryResolveBrokenCavityAccessNextStep(
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        bool allowOptionalHemostasis,
        out CMUResolvedStep resolved)
    {
        if (!RequiresBoneCavityAccess(surgeryId)
            || !HasBrokenCavityAccess(targetPart, allowOptionalHemostasis))
        {
            resolved = default!;
            return false;
        }

        if (ShouldInjectSurgicalTraits(surgeryId, surgeryId)
            && TryResolveSurgicalTraitCleanupStep(targetPart, surgeryId, out resolved))
        {
            return true;
        }

        return TryResolveIncompleteStepFromIndex(patient, targetPart, surgeryId, 0, out resolved);
    }

    private bool RequiresBoneCavityAccess(string surgeryId)
    {
        return TryGetDefinition(surgeryId, out var surgery)
            && surgery.Requirement is { } requirement
            && requirement == OpenBoneCavitySurgery;
    }

    private bool HasBrokenCavityAccess(EntityUid targetPart, bool allowOptionalHemostasis)
    {
        if (!TryComp<BodyPartComponent>(targetPart, out var bodyPart))
            return false;
        if (bodyPart.PartType is not (BodyPartType.Head or BodyPartType.Torso))
            return false;

        var site = GetSiteState(targetPart);
        return site.Access == CMUSurgicalAccess.Deep
            && (allowOptionalHemostasis || site.Hemostasis == CMUSurgicalHemostasis.Clamped);
    }

    protected bool TryResolveNextStepAfterCompletedStep(
        EntityUid patient,
        EntityUid targetPart,
        string leafSurgeryId,
        string completedSurgeryId,
        int completedStepIndex,
        int resumeAfterLeafStepIndex,
        out CMUResolvedStep resolved,
        bool allowOptionalHemostasis = false)
    {
        if (completedSurgeryId != leafSurgeryId)
        {
            if (resumeAfterLeafStepIndex >= 0 && IsSurgicalTraitCleanupSurgeryId(completedSurgeryId))
            {
                if (ShouldInjectSurgicalTraits(leafSurgeryId, leafSurgeryId)
                    && TryResolveSurgicalTraitCleanupStep(targetPart, leafSurgeryId, out resolved))
                {
                    return true;
                }

                return TryResolveIncompleteStepFromIndex(
                    patient,
                    targetPart,
                    leafSurgeryId,
                    resumeAfterLeafStepIndex + 1,
                    out resolved);
            }

            return TryResolveNextStep(
                patient,
                targetPart,
                leafSurgeryId,
                out resolved,
                allowOptionalHemostasis);
        }

        if (ShouldInjectSurgicalTraits(leafSurgeryId, leafSurgeryId)
            && TryResolveSurgicalTraitCleanupStep(targetPart, leafSurgeryId, out resolved))
        {
            return true;
        }

        return TryResolveIncompleteStepFromIndex(
            patient,
            targetPart,
            leafSurgeryId,
            completedStepIndex + 1,
            out resolved);
    }

    private bool TryResolveIncompleteStepFromIndex(
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        int startIndex,
        out CMUResolvedStep resolved)
    {
        resolved = default!;
        if (!TryGetDefinition(surgeryId, out var surgery))
            return false;

        for (var i = Math.Max(0, startIndex); i < surgery.Steps.Length; i++)
        {
            if (RmcSurgery.IsStepComplete(patient, targetPart, surgery.Steps[i].Id))
                continue;

            return TryResolveStepAt(surgeryId, i, out resolved, targetPart);
        }

        return false;
    }

    protected bool TryResolveInjectedCleanupStep(EntityUid targetPart, string leafSurgeryId, out CMUResolvedStep resolved)
    {
        if (!ShouldInjectSurgicalTraits(leafSurgeryId, leafSurgeryId))
        {
            resolved = default!;
            return false;
        }

        return TryResolveSurgicalTraitCleanupStep(targetPart, leafSurgeryId, out resolved);
    }

    private bool TryResolveSurgicalTraitCleanupStep(EntityUid targetPart, string leafSurgeryId, out CMUResolvedStep resolved)
    {
        foreach (var trait in SurgicalTraits.EnumerateOrderedTraits(targetPart))
        {
            if (!CanResolveTraitForAccess(trait, leafSurgeryId))
                continue;

            var surgeryId = TraitCleanupSurgeryId(trait);
            if (surgeryId is null)
                continue;
            if (!TryResolveGatedStep(surgeryId, 0, targetPart, out resolved))
                continue;

            return true;
        }

        resolved = default!;
        return false;
    }

    private bool CanResolveTraitForAccess(CMUSurgicalTrait trait, string leafSurgeryId)
    {
        if (!IsDeepAccessTrait(trait))
            return true;

        return RequiresBoneCavityAccess(leafSurgeryId)
            || leafSurgeryId is "CMUSurgeryCloseBoneCavity" or "CMSurgeryCloseRibcage";
    }

    private static bool IsDeepAccessTrait(CMUSurgicalTrait trait)
    {
        return trait is CMUSurgicalTrait.OrganAdhesion or CMUSurgicalTrait.OrganHemorrhage;
    }

    private static string? TraitCleanupSurgeryId(CMUSurgicalTrait trait)
    {
        return trait switch
        {
            CMUSurgicalTrait.VascularTear => TieVascularTearSurgery,
            CMUSurgicalTrait.EmbeddedForeignBody => ExtractForeignBodySurgery,
            CMUSurgicalTrait.CompartmentPressure => RelieveCompartmentPressureSurgery,
            CMUSurgicalTrait.ContaminatedWound => DebrideContaminatedWoundSurgery,
            CMUSurgicalTrait.BoneSplintered => RemoveBoneFragmentsSurgery,
            CMUSurgicalTrait.OrganAdhesion => FreeOrganAdhesionsSurgery,
            CMUSurgicalTrait.OrganHemorrhage => PackOrganBleedSurgery,
            _ => null,
        };
    }

    private static bool IsSurgicalTraitCleanupSurgeryId(string surgeryId)
    {
        return surgeryId is TieVascularTearSurgery
            or ExtractForeignBodySurgery
            or RelieveCompartmentPressureSurgery
            or DebrideContaminatedWoundSurgery
            or RemoveBoneFragmentsSurgery
            or FreeOrganAdhesionsSurgery
            or PackOrganBleedSurgery;
    }

    private static bool ShouldInjectSurgicalTraits(string leafSurgeryId, string resolvedSurgeryId)
    {
        if (resolvedSurgeryId != leafSurgeryId)
            return false;

        return IsFractureSurgeryId(leafSurgeryId)
            || IsDeepOrganRepairSurgeryId(leafSurgeryId)
            || IsCloseUpSurgeryId(leafSurgeryId);
    }

    public static bool IsFractureSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgerySetSimpleFracture"
            or "CMUSurgerySetSimpleFractureCavity"
            or "CMUSurgerySetCompoundFracture"
            or "CMUSurgerySetCompoundFractureCavity"
            or "CMUSurgerySetShatteredFracture"
            or "CMUSurgerySetShatteredFractureCavity";
    }

    public static bool IsCloseUpSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgeryCloseIncision"
            or "CMUSurgeryCloseBoneCavity"
            or "CMSurgeryCloseIncision"
            or "CMSurgeryCloseRibcage";
    }

    public static bool IsOrganRepairSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgeryRepairLiver"
            or "CMUSurgeryRepairLungs"
            or "CMUSurgeryRepairKidneys"
            or "CMUSurgeryRepairHeart"
            or "CMUSurgeryRepairStomach"
            or "CMUSurgeryRepairBrain"
            or "CMUSurgeryRepairEyes";
    }

    private static bool IsDeepOrganRepairSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgeryRepairLiver"
            or "CMUSurgeryRepairLungs"
            or "CMUSurgeryRepairKidneys"
            or "CMUSurgeryRepairHeart"
            or "CMUSurgeryRepairStomach"
            or "CMUSurgeryRepairBrain";
    }

    private bool TryResolveReattachNextStep(
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        out CMUResolvedStep resolved,
        bool allowOptionalHemostasis)
    {
        resolved = default!;
        if (targetPart == default)
            return false;

        if (surgeryId == "RMCSynthSurgeryReattachLimb")
        {
            if (HasComp<CMUReattachCompleteComponent>(targetPart))
                return TryResolveStepAt(surgeryId, 3, out resolved, targetPart);
            if (HasComp<CMUReattachPreppedComponent>(targetPart))
                return TryResolveStepAt(surgeryId, 2, out resolved, targetPart);
            if (HasComp<CMUStumpRemovedComponent>(targetPart))
                return TryResolveStepAt(surgeryId, 1, out resolved, targetPart);

            return TryResolveStepAt(surgeryId, 0, out resolved, targetPart);
        }

        if (surgeryId != "CMUSurgeryReattachLimb")
            return false;

        if (!HasComp<CMIncisionOpenComponent>(targetPart))
            return TryResolveGatedStep("CMUSurgeryOpenSoftTissue", 0, targetPart, out resolved);
        if (!allowOptionalHemostasis && !HasComp<CMBleedersClampedComponent>(targetPart))
            return TryResolveGatedStep("CMUSurgeryOpenSoftTissue", 1, targetPart, out resolved);
        if (!HasComp<CMSkinRetractedComponent>(targetPart))
            return TryResolveGatedStep("CMUSurgeryOpenSoftTissue", 2, targetPart, out resolved);

        if (HasComp<CMUReattachCompleteComponent>(targetPart))
            return TryResolveStepAt(surgeryId, 3, out resolved, targetPart);
        if (HasComp<CMUReattachPreppedComponent>(targetPart))
            return TryResolveStepAt(surgeryId, 2, out resolved, targetPart);
        if (HasComp<CMUStumpRemovedComponent>(targetPart))
            return TryResolveStepAt(surgeryId, 1, out resolved, targetPart);

        return TryResolveStepAt(surgeryId, 0, out resolved, targetPart);
    }

    private bool TryResolveGatedStep(string surgeryId, int stepIndex, EntityUid targetPart, out CMUResolvedStep resolved)
    {
        if (!TryResolveStepAt(surgeryId, stepIndex, out var step, targetPart))
        {
            resolved = default!;
            return false;
        }

        resolved = new CMUResolvedStep(
            step.ResolvedSurgeryId,
            step.StepIndex,
            step.StepLabel,
            step.ToolCategory,
            step.TotalSteps,
            step.ResolvedSurgeryId);
        return true;
    }

    public bool TryResolveStepAt(string surgeryId, int stepIndex, out CMUResolvedStep resolved, EntityUid? targetPart = null)
    {
        resolved = default!;
        if (!TryGetDefinition(surgeryId, out var surgery)
            || !surgery.TryGetStepAt(stepIndex, out var step))
            return false;

        resolved = new CMUResolvedStep(
            surgeryId,
            stepIndex,
            ResolveContextualStepLabel(step.Id, step.Label, targetPart),
            step.ToolCategory,
            surgery.Steps.Length,
            null);
        return true;
    }

    private string ResolveContextualStepLabel(EntProtoId stepProtoId, string fallback, EntityUid? targetPart)
    {
        if (stepProtoId == TieVascularTearStep)
            return Loc.GetString("cmu-medical-surgery-step-tie-vessel-label");
        if (stepProtoId == ExtractForeignBodyStep)
            return Loc.GetString("cmu-medical-surgery-step-extract-foreign-body-label");
        if (stepProtoId == RelieveCompartmentPressureStep)
            return Loc.GetString("cmu-medical-surgery-step-relieve-pressure-label");
        if (stepProtoId == DebrideContaminatedWoundStep)
            return Loc.GetString("cmu-medical-surgery-step-debride-contamination-label");
        if (stepProtoId == RemoveBoneFragmentsStep)
            return Loc.GetString("cmu-medical-surgery-step-remove-bone-fragments-label");
        if (stepProtoId == FreeOrganAdhesionsStep)
            return Loc.GetString("cmu-medical-surgery-step-free-organ-adhesions-label");
        if (stepProtoId == PackOrganBleedStep)
            return Loc.GetString("cmu-medical-surgery-step-pack-organ-bleed-label");

        if (stepProtoId != MendRibcageStep)
            return fallback;

        if (targetPart is { } part && TryComp<BodyPartComponent>(part, out var bodyPart))
        {
            return bodyPart.PartType switch
            {
                BodyPartType.Head => Loc.GetString("cmu-medical-surgery-step-mend-skull-label"),
                BodyPartType.Torso => Loc.GetString("cmu-medical-surgery-step-mend-ribcage-label"),
                _ => Loc.GetString("cmu-medical-surgery-step-mend-bones-label"),
            };
        }

        return fallback;
    }

    private string? ResolveLegacyStepToolCategory(CMSurgeryStepComponent step)
    {
        if (step.Tool is null)
            return null;

        foreach (var (_, reg) in step.Tool)
        {
            if (reg.Component is null)
                continue;
            var componentType = reg.Component.GetType();

            foreach (var (categoryName, categoryTypes) in _toolCategories)
            {
                foreach (var t in categoryTypes)
                {
                    if (t == componentType)
                        return categoryName;
                }
            }
        }
        return null;
    }

    public bool TryFindClickedPart(EntityUid patient, EntityUid? clickTarget, BodyPartType type, BodyPartSymmetry symmetry, out EntityUid part)
    {
        part = default;
        if (!MedicalIndex.TryGetBodyPart(patient, new CMUMedicalBodyPartKey(type, symmetry), out var indexedPart))
            return false;

        if (clickTarget is { } direct && direct != patient && direct != indexedPart)
            return false;

        part = indexedPart;
        return true;
    }

    public bool TryGetReattachAnchorPart(EntityUid patient, out EntityUid anchor)
    {
        anchor = default;
        if (!MedicalIndex.TryGetRootPart(patient, out var root))
            return false;

        anchor = root.Owner;
        return true;
    }

    public bool LimbMatchesMissingSlot(EntityUid patient, EntityUid heldLimb, BodyPartType targetType, BodyPartSymmetry targetSymmetry)
    {
        if (!TryComp<BodyPartComponent>(heldLimb, out var heldBp))
            return false;
        if (heldBp.PartType != targetType || heldBp.Symmetry != targetSymmetry)
            return false;
        if (!CanPatientAcceptLimb(patient, heldLimb))
            return false;
        if (targetType is not (BodyPartType.Arm or BodyPartType.Leg))
            return false;

        if (!MedicalIndex.TryGetRootPart(patient, out var root))
            return false;

        var targetSide = targetSymmetry switch
        {
            BodyPartSymmetry.Left => "left",
            BodyPartSymmetry.Right => "right",
            _ => null,
        };
        if (targetSide is null)
            return false;

        foreach (var slot in MedicalIndex.GetBodyPartSlots(root.Owner))
        {
            if (slot.Type != targetType)
                continue;
            // Slot id encodes side — left_arm / right_leg / etc.
            if (!slot.SlotId.Contains(targetSide, System.StringComparison.Ordinal))
                continue;
            // Accept the matching slot — if it's filled, the attach call
            // no-ops with a "slot occupied" popup, which is the right UX.
            return true;
        }

        return false;
    }

    public bool CanPatientAcceptLimb(EntityUid patient, EntityUid heldLimb)
    {
        return !HasComp<CMUDroneAndroidComponent>(patient) ||
               HasComp<CMURoboticLimbComponent>(heldLimb);
    }

    public bool ToolMatchesCategory(EntityUid tool, string? category)
    {
        if (category is null)
            return true;
        if (!_toolCategories.TryGetValue(category, out var componentTypes))
            return false;

        foreach (var ct in componentTypes)
        {
            if (HasComp(tool, ct))
                return true;
        }
        return false;
    }

    public bool TryGetWrongToolDamage(EntityUid tool, out string damageType, out float amount)
    {
        foreach (var (componentType, dmgType, amt) in CMUWrongToolDamageTable.Entries)
        {
            if (!HasComp(tool, componentType))
                continue;
            damageType = dmgType;
            amount = amt;
            return true;
        }
        damageType = string.Empty;
        amount = 0f;
        return false;
    }

    public CMUSurgeryBuiState BuildBuiState(
        EntityUid patient,
        string patientName,
        List<CMUSurgeryPartEntry> parts,
        CMUSurgeryArmedStepComponent? armed)
    {
        CMUArmedStepInfo? armedInfo = null;
        if (armed is not null)
        {
            // Surface the leaf the medic picked — SurgeryId may differ when
            // a prereq is currently being run.
            var leafId = string.IsNullOrEmpty(armed.LeafSurgeryId) ? armed.SurgeryId : armed.LeafSurgeryId;
            string leafDisplayName = ResolveSurgeryDisplayName(leafId);
            armedInfo = new CMUArmedStepInfo(armed.SurgeryId, leafDisplayName, armed.StepIndex, armed.StepLabel, armed.RequiredToolCategory);
        }

        CMUSurgeryInFlightInfo? inFlight = null;
        if (TryComp<CMUSurgeryInProgressComponent>(patient, out var lockComp)
            && TryComp<CMUSurgeryInFlightComponent>(lockComp.Part, out var flight))
        {
            var partDisplay = string.Empty;
            if (TryComp<BodyPartComponent>(lockComp.Part, out var partComp))
                partDisplay = FormatPartName(partComp.PartType, partComp.Symmetry);
            else if (lockComp.TargetPartType != default)
                partDisplay = FormatPartName(lockComp.TargetPartType, lockComp.TargetSymmetry);
            inFlight = new CMUSurgeryInFlightInfo(
                GetNetEntity(lockComp.Part),
                partDisplay,
                flight.LeafSurgeryId,
                flight.LeafSurgeryDisplayName,
                flight.SurgeonName,
                flight.StartedAt);
        }

        CMUSurgerySessionId? sessionId = null;
        CMUSurgeryAttemptToken? activeAttempt = null;
        CMUSurgerySessionPhase? sessionPhase = null;
        BodyPartType? sessionPartType = null;
        BodyPartSymmetry? sessionPartSymmetry = null;
        if (SurgerySessions.TryGetSession(patient, out var session))
        {
            sessionId = session.Id;
            activeAttempt = session.ActiveAttempt;
            sessionPhase = session.Phase;
            sessionPartType = session.Site.Type;
            sessionPartSymmetry = session.Site.Symmetry;
        }

        return new CMUSurgeryBuiState(
            GetNetEntity(patient),
            patientName,
            parts,
            armedInfo,
            inFlight,
            sessionId,
            activeAttempt,
            armed?.StateId,
            sessionPhase,
            sessionPartType,
            sessionPartSymmetry);
    }

    public string ResolveSurgeryDisplayName(string surgeryId)
    {
        if (TryGetDefinition(surgeryId, out var definition))
            return definition.DisplayName;
        return surgeryId;
    }

    public static string FormatPartName(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var side = symmetry switch
        {
            BodyPartSymmetry.Left => "Left ",
            BodyPartSymmetry.Right => "Right ",
            _ => string.Empty,
        };
        return side + type;
    }
}
