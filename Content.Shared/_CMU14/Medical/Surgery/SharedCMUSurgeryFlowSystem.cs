using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Surgery.Markers;
using Content.Shared._CMU14.Medical.Surgery.Traits;
using Content.Shared._CMU14.Medical.StatusEffects;
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

namespace Content.Shared._CMU14.Medical.Surgery;

public abstract partial class SharedCMUSurgeryFlowSystem : EntitySystem
{
    [Dependency] protected IConfigurationManager Cfg = default!;
    [Dependency] protected INetManager Net = default!;
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected IGameTiming Timing = default!;
    [Dependency] protected SharedBodySystem Body = default!;
    [Dependency] protected SharedDoAfterSystem DoAfter = default!;
    [Dependency] protected SharedHandsSystem Hands = default!;
    [Dependency] protected ItemToggleSystem ItemToggle = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;
    [Dependency] protected SharedPainShockSystem Pain = default!;
    [Dependency] protected SharedCMUSurgicalTraitSystem SurgicalTraits = default!;
    [Dependency] protected SharedStatusEffectsSystem Status = default!;
    [Dependency] protected SharedUserInterfaceSystem UserInterface = default!;
    [Dependency] protected SharedCMSurgerySystem RmcSurgery = default!;

    private readonly Dictionary<string, CMUSurgeryStepMetadataPrototype> _bySurgery = new();

    private readonly Dictionary<string, Type[]> _toolCategories = new();

    private const float ArmedStepScanInterval = 0.5f;
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
    private static readonly EntProtoId MendRibcageStep = "CMSurgeryStepMendRibcage";
    private static readonly EntProtoId TieVascularTearStep = "CMUSurgeryStepTieVascularTear";
    private static readonly EntProtoId ExtractForeignBodyStep = "CMUSurgeryStepExtractForeignBody";
    private static readonly EntProtoId RelieveCompartmentPressureStep = "CMUSurgeryStepRelieveCompartmentPressure";
    private static readonly EntProtoId DebrideContaminatedWoundStep = "CMUSurgeryStepDebrideContaminatedWound";
    private static readonly EntProtoId RemoveBoneFragmentsStep = "CMUSurgeryStepRemoveBoneFragments";
    private static readonly EntProtoId FreeOrganAdhesionsStep = "CMUSurgeryStepFreeOrganAdhesions";
    private static readonly EntProtoId PackOrganBleedStep = "CMUSurgeryStepPackOrganBleed";
    private float _armedStepScanAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        BuildToolCategoryTable();
        IndexMetadata();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);

        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, InteractUsingEvent>(OnArmedInteractUsing);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, InteractHandEvent>(OnArmedInteractHand);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, DoAfterAttemptEvent<CMUSurgeryStepDoAfterEvent>>(OnStepDoAfterAttempt);
        SubscribeLocalEvent<CMUSurgeryArmedStepComponent, CMUSurgeryStepDoAfterEvent>(OnStepDoAfter);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<CMUSurgeryStepMetadataPrototype>())
            IndexMetadata();
    }

    private void IndexMetadata()
    {
        _bySurgery.Clear();
        foreach (var proto in Prototypes.EnumeratePrototypes<CMUSurgeryStepMetadataPrototype>())
        {
            _bySurgery[proto.Surgery] = proto;
        }
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

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Server drives expire; the armed component is networked so the
        // server's deletion mirrors over to the client.
        if (!Net.IsServer)
            return;

        _armedStepScanAccumulator += frameTime;
        if (_armedStepScanAccumulator < ArmedStepScanInterval)
            return;
        _armedStepScanAccumulator = 0f;

        var now = Timing.CurTime;
        var query = EntityQueryEnumerator<CMUSurgeryArmedStepComponent>();
        while (query.MoveNext(out var uid, out var armed))
        {
            if (now - armed.ArmedAt < armed.ExpireAfter)
                continue;

            ClearArmed(uid, armed, expired: true);
        }
    }

    public CMUSurgeryArmedStepComponent? TryArmStep(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid targetPart,
        string surgeryId,
        int stepIndex,
        BodyPartType? fallbackType = null,
        BodyPartSymmetry? fallbackSymmetry = null,
        bool allowSamePartInFlightSwitch = false)
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
            && existing.TargetSymmetry == armedSymmetry)
        {
            existing.Surgeon = surgeon;
            existing.ArmedAt = Timing.CurTime;
            Dirty(patient, existing);
            return existing;
        }

        // Resolve via the requirement chain so prereqs (open-incision,
        // open-ribcage, etc.) can't be skipped. Legacy RMC prereqs without
        // a CMU metadata entry get a synthesized label from the step proto.
        if (!TryResolveNextStep(patient, operationPart, surgeryId, out var resolved))
            return null;

        var armed = EnsureComp<CMUSurgeryArmedStepComponent>(patient);
        armed.Surgeon = surgeon;
        // SurgeryId = resolved (drives V1 step-event raise in RunStepEffect).
        // LeafSurgeryId = what the medic picked (drives BUI display).
        armed.SurgeryId = resolved.ResolvedSurgeryId;
        armed.StepIndex = resolved.StepIndex;
        armed.TargetPartType = armedType;
        armed.TargetSymmetry = armedSymmetry;
        armed.RequiredToolCategory = resolved.ToolCategory;
        armed.StepLabel = resolved.StepLabel;
        armed.LeafSurgeryId = surgeryId;
        armed.LastCompletedLeafStepIndex = -1;
        armed.ArmedAt = Timing.CurTime;
        Dirty(patient, armed);
        return armed;
    }

    public void EnsureSurgeryInFlight(EntityUid patient, EntityUid part, EntityUid surgeon, string leafSurgeryId, string leafDisplayName, BodyPartType targetType = default, BodyPartSymmetry targetSymmetry = default)
    {
        var lockComp = EnsureComp<CMUSurgeryInProgressComponent>(patient);
        var alreadyInFlight = lockComp.LeafSurgeryId == leafSurgeryId && lockComp.Part == part;
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
        return true;
    }

    public void ClearSurgeryInFlight(EntityUid patient)
    {
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

    public void ClearArmed(EntityUid patient, CMUSurgeryArmedStepComponent? armed = null, bool expired = false)
    {
        if (!Resolve(patient, ref armed, false))
            return;

        var surgeon = armed.Surgeon;
        RemComp<CMUSurgeryArmedStepComponent>(patient);

        if (Net.IsServer && surgeon.IsValid())
        {
            var msg = expired
                ? "cmu-medical-surgery-armed-expired"
                : "cmu-medical-surgery-armed-cancelled";
            Popup.PopupEntity(Loc.GetString(msg), surgeon, surgeon, PopupType.SmallCaution);
        }
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

    private void SurgeryConditionPopup(EntityUid user, string locKey, bool popup)
    {
        if (!popup || !Net.IsServer)
            return;

        Popup.PopupEntity(Loc.GetString(locKey), user, user, PopupType.SmallCaution);
    }

    private bool IsPatientStableForSurgery(EntityUid patient)
    {
        if (TryComp<MobStateComponent>(patient, out var mobState)
            && mobState.CurrentState != MobState.Alive)
        {
            return true;
        }

        if (IsHorizontallyRestrained(patient))
            return true;

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

    private bool ShouldInterruptSurgeryStep(EntityUid patient)
    {
        if (IsPatientStableForSurgery(patient))
            return false;

        return TryComp<PainShockComponent>(patient, out var pain)
            && Pain.GetEffectiveTier(patient, pain) >= PainTier.Severe;
    }

    private bool IsHorizontallyRestrained(EntityUid patient)
    {
        if (!TryComp<BuckleComponent>(patient, out var buckle)
            || buckle.BuckledTo is not { } strapUid
            || !TryComp<StrapComponent>(strapUid, out var strap))
        {
            return false;
        }

        var rotation = strap.Rotation;
        return rotation.GetCardinalDir() is Direction.West or Direction.East;
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

        if (user != armed.Surgeon)
            return false;

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
        {
            ClearArmed(patient, armed);
            return true;
        }

        var hasTargetPart = TryFindClickedPart(patient, clickTarget, armed.TargetPartType, armed.TargetSymmetry, out var targetPart);
        if (!hasTargetPart && !TryResolveReattachAnchorForUse(patient, clickTarget, armed, out targetPart))
        {
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-wrong-part"), patient, user, PopupType.SmallCaution);
            return true;
        }

        if (isRightTool)
        {
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

            if (Net.IsServer)
            {
                started = StartStepDoAfter(patient, armed, user, used, targetPart);
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

        if (RmcSurgery.GetSingleton(armed.SurgeryId) is not { } surgeryEnt)
            return false;
        if (!TryComp<CMSurgeryComponent>(surgeryEnt, out var surgeryComp))
            return false;
        if (armed.StepIndex < 0 || armed.StepIndex >= surgeryComp.Steps.Count)
            return false;
        if (RmcSurgery.GetSingleton(surgeryComp.Steps[armed.StepIndex]) is not { } resolvedStepEnt)
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

    private void OnArmedInteractHand(Entity<CMUSurgeryArmedStepComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var (patient, armed) = ent;

        if (args.User != armed.Surgeon)
            return;

        Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-no-tool"), patient, args.User, PopupType.SmallCaution);
        ClearArmed(patient, armed);
        args.Handled = true;
    }

    /// <summary>
    ///     Override in the sealed server class so prediction rollback can't
    ///     re-raise the step event on the client.
    /// </summary>
    protected virtual bool StartStepDoAfter(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid tool, EntityUid targetPart)
    {
        return false;
    }

    protected virtual void ApplyWrongToolDamage(EntityUid surgeon, EntityUid patient, EntityUid tool, string damageType, float amount)
    {
    }

    /// <summary>
    ///     Server-only — raises V1 <c>CMSurgeryStepEvent</c> + either re-arms
    ///     or raises <c>CMSurgeryCompleteEvent</c>. Shared no-ops so
    ///     prediction rollback can't double-apply state mutations.
    /// </summary>
    protected virtual void RunStepEffect(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon, EntityUid? tool, EntityUid? targetPart)
    {
    }

    public bool TryCompleteAutomatedStep(EntityUid patient, CMUSurgeryArmedStepComponent armed, EntityUid surgeon)
    {
        if (!Net.IsServer)
            return false;

        if (armed.Surgeon != surgeon)
            return false;

        if (!CanOperateOnPatient(patient, surgeon, popup: true))
        {
            ClearArmed(patient, armed);
            return false;
        }

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

        if (armed.Surgeon != ev.User
            || !ArmedMatchesDoAfter(armed, ev)
            || !CanOperateOnPatient(patient, ev.User)
            || ShouldInterruptSurgeryStep(patient))
        {
            args.Cancel();
        }
    }

    private void OnStepDoAfter(Entity<CMUSurgeryArmedStepComponent> ent, ref CMUSurgeryStepDoAfterEvent args)
    {
        var (patient, armed) = ent;

        if (!ArmedMatchesDoAfter(armed, args))
            return;

        if (args.Cancelled)
        {
            if (args.User == armed.Surgeon)
            {
                if (ShouldInterruptSurgeryStep(patient))
                    Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-step-pain-interrupted"), patient, args.User, PopupType.MediumCaution);

                ClearArmed(patient, armed);
            }
            return;
        }

        if (args.Handled)
            return;
        args.Handled = true;

        if (armed.Surgeon != args.User)
            return;

        if (!CanOperateOnPatient(patient, args.User, popup: true))
        {
            ClearArmed(patient, armed);
            return;
        }

        if (ShouldInterruptSurgeryStep(patient))
        {
            Popup.PopupEntity(Loc.GetString("cmu-medical-surgery-step-pain-interrupted"), patient, args.User, PopupType.MediumCaution);
            ClearArmed(patient, armed);
            return;
        }

        if (Net.IsServer)
            RunStepEffect(patient, armed, args.User, args.Used, args.Target);
    }

    private static bool ArmedMatchesDoAfter(CMUSurgeryArmedStepComponent armed, CMUSurgeryStepDoAfterEvent args)
    {
        return armed.SurgeryId == args.SurgeryId
            && armed.LeafSurgeryId == args.LeafSurgeryId
            && armed.StepIndex == args.StepIndex
            && armed.TargetPartType == args.TargetPartType
            && armed.TargetSymmetry == args.TargetSymmetry;
    }

    public bool TryGetMetadata(string surgeryId, out CMUSurgeryStepMetadataPrototype metadata)
    {
        return _bySurgery.TryGetValue(surgeryId, out metadata!);
    }

    public bool CanSelfOperateSurgery(string surgeryId, BodyPartType partType)
    {
        if (!_bySurgery.TryGetValue(surgeryId, out var metadata))
            return IsSelfCloseUpSurgery(surgeryId, partType);

        if (!metadata.AllowSelfSurgery)
            return false;

        var validParts = metadata.SelfSurgeryValidParts.Count > 0
            ? metadata.SelfSurgeryValidParts
            : metadata.ValidParts;

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
        return partType is BodyPartType.Arm or BodyPartType.Leg;
    }

    public IEnumerable<CMUSurgeryStepMetadataPrototype> EnumerateMetadata()
    {
        return _bySurgery.Values;
    }

    /// <summary>
    ///     Walks RMC's <c>GetNextStep</c> — honours the <c>Requirement</c>
    ///     chain so picking "Set Compound Fracture" arms open-incision first
    ///     when the part isn't yet incised.
    /// </summary>
    public bool TryResolveNextStep(EntityUid patient, EntityUid? targetPart, string surgeryId, out CMUResolvedStep resolved)
    {
        resolved = default!;
        if (targetPart is null)
            return false;

        if (TryResolveReattachNextStep(patient, targetPart.Value, surgeryId, out resolved))
            return true;

        if (RmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt)
            return false;

        var next = RmcSurgery.GetNextStep(patient, targetPart.Value, surgeryEnt);
        if (next is null)
            return false; // surgery already complete on this part — nothing to arm.

        var (resolvedSurgery, stepIdx) = next.Value;
        var resolvedSurgeryProtoId = MetaData(resolvedSurgery.Owner).EntityPrototype?.ID;
        if (resolvedSurgeryProtoId is null)
            return false;

        var totalSteps = resolvedSurgery.Comp.Steps.Count;

        if (ShouldInjectSurgicalTraits(surgeryId, resolvedSurgeryProtoId)
            && TryResolveSurgicalTraitCleanupStep(targetPart.Value, out var traitStep))
        {
            resolved = traitStep;
            return true;
        }

        var stepLabel = string.Empty;
        string? toolCategory = null;

        var stepProtoId = resolvedSurgery.Comp.Steps[stepIdx];
        if (TryGetMetadata(resolvedSurgeryProtoId, out var metadata) && stepIdx < metadata.Steps.Count)
        {
            var stepMeta = metadata.Steps[stepIdx];
            stepLabel = ResolveContextualStepLabel(stepProtoId, stepMeta.Label, targetPart);
            toolCategory = stepMeta.ToolCategory;
        }
        else
        {
            // Fall back to the step entity's prototype name + a heuristic
            // tool category from the step's tool registry.
            if (RmcSurgery.GetSingleton(stepProtoId) is { } stepEnt)
            {
                stepLabel = ResolveContextualStepLabel(stepProtoId, MetaData(stepEnt).EntityName, targetPart);
                toolCategory = ResolveLegacyStepToolCategory(stepEnt);
            }
        }

        resolved = new CMUResolvedStep(
            resolvedSurgeryProtoId,
            stepIdx,
            stepLabel,
            toolCategory,
            totalSteps,
            // Gating prereq id only when the leaf surgery isn't the one
            // being armed - lets the BUI flag "(via Open Incision)".
            resolvedSurgeryProtoId == surgeryId ? null : resolvedSurgeryProtoId);
        return true;
    }

    protected bool TryResolveNextStepAfterCompletedStep(
        EntityUid patient,
        EntityUid targetPart,
        string leafSurgeryId,
        string completedSurgeryId,
        int completedStepIndex,
        int resumeAfterLeafStepIndex,
        out CMUResolvedStep resolved)
    {
        if (completedSurgeryId != leafSurgeryId)
        {
            if (resumeAfterLeafStepIndex >= 0 && IsSurgicalTraitCleanupSurgeryId(completedSurgeryId))
            {
                if (ShouldInjectSurgicalTraits(leafSurgeryId, leafSurgeryId)
                    && TryResolveSurgicalTraitCleanupStep(targetPart, out resolved))
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

            return TryResolveNextStep(patient, targetPart, leafSurgeryId, out resolved);
        }

        if (ShouldInjectSurgicalTraits(leafSurgeryId, leafSurgeryId)
            && TryResolveSurgicalTraitCleanupStep(targetPart, out resolved))
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
        if (RmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt)
            return false;
        if (!TryComp<CMSurgeryComponent>(surgeryEnt, out var surgeryComp))
            return false;

        for (var i = Math.Max(0, startIndex); i < surgeryComp.Steps.Count; i++)
        {
            var stepId = surgeryComp.Steps[i];
            if (RmcSurgery.IsStepComplete(patient, targetPart, stepId))
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

        return TryResolveSurgicalTraitCleanupStep(targetPart, out resolved);
    }

    private bool TryResolveSurgicalTraitCleanupStep(EntityUid targetPart, out CMUResolvedStep resolved)
    {
        foreach (var trait in SurgicalTraits.EnumerateOrderedTraits(targetPart))
        {
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
            || IsOrganRepairSurgeryId(leafSurgeryId)
            || IsCloseUpSurgeryId(leafSurgeryId);
    }

    public static bool IsFractureSurgeryId(string surgeryId)
    {
        return surgeryId is "CMUSurgerySetSimpleFracture"
            or "CMUSurgerySetSimpleFractureCavity"
            or "CMUSurgerySetCompoundFracture"
            or "CMUSurgerySetCompoundFractureCavity"
            or "CMUSurgerySetComminutedFracture"
            or "CMUSurgerySetComminutedFractureCavity";
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
            or "CMUSurgeryRepairEyes"
            or "CMUSurgeryRepairEars";
    }

    private bool TryResolveReattachNextStep(EntityUid patient, EntityUid targetPart, string surgeryId, out CMUResolvedStep resolved)
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
        if (!HasComp<CMBleedersClampedComponent>(targetPart))
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
        if (RmcSurgery.GetSingleton(surgeryId) is not { } surgeryEnt)
            return false;
        if (!TryComp<CMSurgeryComponent>(surgeryEnt, out var surgeryComp))
            return false;
        if (stepIndex < 0 || stepIndex >= surgeryComp.Steps.Count)
            return false;

        var stepLabel = string.Empty;
        string? toolCategory = null;

        var stepProtoId = surgeryComp.Steps[stepIndex];
        if (TryGetMetadata(surgeryId, out var metadata) && stepIndex < metadata.Steps.Count)
        {
            var stepMeta = metadata.Steps[stepIndex];
            stepLabel = ResolveContextualStepLabel(stepProtoId, stepMeta.Label, targetPart);
            toolCategory = stepMeta.ToolCategory;
        }
        else
        {
            if (RmcSurgery.GetSingleton(stepProtoId) is { } stepEnt)
            {
                stepLabel = ResolveContextualStepLabel(stepProtoId, MetaData(stepEnt).EntityName, targetPart);
                toolCategory = ResolveLegacyStepToolCategory(stepEnt);
            }
        }

        resolved = new CMUResolvedStep(
            surgeryId,
            stepIndex,
            stepLabel,
            toolCategory,
            surgeryComp.Steps.Count,
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

    protected string? ResolveLegacyStepToolCategory(EntityUid stepEnt)
    {
        if (!TryComp<CMSurgeryStepComponent>(stepEnt, out var stepComp) || stepComp.Tool is null)
            return null;

        foreach (var (_, reg) in stepComp.Tool)
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

        if (clickTarget is { } direct
            && TryComp<BodyPartComponent>(direct, out var directBp)
            && directBp.PartType == type
            && directBp.Symmetry == symmetry)
        {
            part = direct;
            return true;
        }

        foreach (var (childId, childComp) in Body.GetBodyChildren(patient))
        {
            if (childComp.PartType != type || childComp.Symmetry != symmetry)
                continue;
            part = childId;
            return true;
        }

        return false;
    }

    public bool TryGetReattachAnchorPart(EntityUid patient, out EntityUid anchor)
    {
        anchor = default;
        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return false;
        if (Body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return false;

        anchor = root.Entity;
        return true;
    }

    public bool LimbMatchesMissingSlot(EntityUid patient, EntityUid heldLimb, BodyPartType targetType, BodyPartSymmetry targetSymmetry)
    {
        if (!TryComp<BodyPartComponent>(heldLimb, out var heldBp))
            return false;
        if (heldBp.PartType != targetType || heldBp.Symmetry != targetSymmetry)
            return false;
        if (targetType is not (BodyPartType.Arm or BodyPartType.Leg))
            return false;

        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return false;
        if (Body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return false;

        var targetSide = targetSymmetry switch
        {
            BodyPartSymmetry.Left => "left",
            BodyPartSymmetry.Right => "right",
            _ => null,
        };
        if (targetSide is null)
            return false;

        foreach (var (slotId, slot) in root.BodyPart.Children)
        {
            if (slot.Type != targetType)
                continue;
            // Slot id encodes side — left_arm / right_leg / etc.
            if (!slotId.Contains(targetSide, System.StringComparison.Ordinal))
                continue;
            // Accept the matching slot — if it's filled, the attach call
            // no-ops with a "slot occupied" popup, which is the right UX.
            return true;
        }

        return false;
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
        CMUSurgeryArmedStepComponent? armed,
        EntityUid? viewer = null)
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
                flight.StartedAt,
                viewer is null || flight.Surgeon == viewer.Value);
        }

        return new CMUSurgeryBuiState(GetNetEntity(patient), patientName, parts, armedInfo, inFlight);
    }

    public string ResolveSurgeryDisplayName(string surgeryId)
    {
        if (TryGetMetadata(surgeryId, out var metadata))
            return metadata.DisplayName ?? surgeryId;
        if (Prototypes.TryIndex<EntityPrototype>(surgeryId, out var proto))
            return proto.Name;
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
