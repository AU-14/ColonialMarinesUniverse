using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._CMU14.Medical.Foundation;
using Content.Shared._CMU14.Medical.Human.Components;
using Content.Shared._CMU14.Medical.Human.Data;
using Content.Shared._CMU14.Medical.Human.Diagnostics;
using Content.Shared._CMU14.Medical.Human.Surgery;
using Content.Shared._CMU14.Medical.Human.Systems;
using Content.Shared._CMU14.Medical.Machines;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;
using BodyPartSymmetry = Content.Shared.Body.Part.BodyPartSymmetry;
using BodyPartType = Content.Shared.Body.Part.BodyPartType;

namespace Content.Server._CMU14.Medical.Machines;

public sealed partial class CMUAutodocSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedBodySystem _body = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private HumanSurgerySystem _surgery = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private const float DefaultProcedureSeconds = 8f;
    private const float QuickProcedureSeconds = 5f;
    private const float ComplexProcedureSeconds = 12f;

    private static readonly Dictionary<string, SoundSpecifier> ProcedureSounds = new()
    {
        ["bleed"] = new SoundCollectionSpecifier("RMCSurgeryHemostat"),
        ["burn"] = new SoundCollectionSpecifier("RMCSurgeryScalpel"),
        ["close_up"] = new SoundCollectionSpecifier("RMCSurgeryCautery"),
        ["fracture"] = new SoundCollectionSpecifier("RMCSurgerySplint"),
        ["general"] = new SoundCollectionSpecifier("RMCSurgeryScalpel"),
        ["head_organ"] = new SoundCollectionSpecifier("RMCSurgeryOrgan"),
        ["suture"] = new SoundCollectionSpecifier("RMCSurgeryCautery"),
    };

    private static readonly Vector2[] EjectOffsets =
    [
        Vector2.Zero,
        new(0f, 1f),
        new(1f, 0f),
        new(-1f, 0f),
        new(0f, -1f),
        new(1f, 1f),
        new(-1f, 1f),
        new(1f, -1f),
        new(-1f, -1f),
    ];

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMUAutodocConsoleComponent>(CMUAutodocUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<CMUAutodocQueueStepMessage>(OnQueueStep);
            subs.Event<CMUAutodocRemoveQueueStepMessage>(OnRemoveQueueStep);
            subs.Event<CMUAutodocClearQueueMessage>(OnClearQueue);
            subs.Event<CMUAutodocStartMessage>(OnStart);
            subs.Event<CMUAutodocStopMessage>(OnStop);
            subs.Event<CMUAutodocEjectPatientMessage>(OnEjectPatient);
        });

        SubscribeLocalEvent<CMUAutodocPodComponent, ComponentInit>(OnPodInit);
        SubscribeLocalEvent<CMUAutodocPodComponent, DestructionEventArgs>(OnPodDestroyed);
        SubscribeLocalEvent<CMUAutodocPodComponent, DragDropTargetEvent>(OnPodDragDrop);
        SubscribeLocalEvent<CMUAutodocPodComponent, GetVerbsEvent<AlternativeVerb>>(OnPodAlternativeVerbs);
        SubscribeLocalEvent<CMUAutodocPodComponent, ContainerRelayMovementEntityEvent>(OnPodRelayMovement);
        SubscribeLocalEvent<CMUAutodocPodComponent, CMUMedicalPodInsertDoAfterEvent>(OnPodInsertDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var podQuery = EntityQueryEnumerator<CMUAutodocPodComponent>();
        while (podQuery.MoveNext(out var pod, out var comp))
        {
            if (!comp.IsRunning || now < comp.NextStepAt)
                continue;

            ProcessPod(pod, comp);
        }

        _uiAccumulator += frameTime;
        if (_uiAccumulator < 1f)
            return;

        _uiAccumulator = 0f;
        var consoleQuery = EntityQueryEnumerator<CMUAutodocConsoleComponent>();
        while (consoleQuery.MoveNext(out var console, out var comp))
            RefreshUi(console, comp, comp.LastViewer);
    }

    private void OnUiOpened(Entity<CMUAutodocConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        ent.Comp.LastViewer = args.Actor;
        RefreshUi(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnQueueStep(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocQueueStepMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out _, out var podComp) ||
            podComp.IsRunning ||
            podComp.BodyContainer.ContainedEntity is not { } patient)
        {
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        var parts = BuildAutodocPartEntries(patient);
        foreach (var part in parts)
        {
            if (part.Part != msg.Part ||
                part.Type != msg.TargetPartType ||
                part.Symmetry != msg.TargetSymmetry)
            {
                continue;
            }

            foreach (var surgery in part.EligibleSurgeries)
            {
                if (surgery.SurgeryId != msg.SurgeryId ||
                    surgery.NextStepIndex != msg.StepIndex ||
                    !Enum.TryParse<SurgeryProcedureId>(surgery.SurgeryId, out var procedureId))
                {
                    continue;
                }

                var targetPart = TryGetEntity(part.Part, out var resolvedPart) && resolvedPart is { } validPart
                    ? validPart
                    : patient;
                podComp.Queue.Add(new CMUAutodocQueuedStep(
                    targetPart.IsValid() ? targetPart : patient,
                    part.Type,
                    part.Symmetry,
                    part.Region,
                    procedureId,
                    surgery.DisplayName,
                    surgery.Category,
                    surgery.NextStepIndex,
                    surgery.NextStepLabel,
                    part.DisplayName,
                    GetProcedureDurationSeconds(procedureId)));
                RefreshUi(ent.Owner, ent.Comp, msg.Actor);
                return;
            }
        }

        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnRemoveQueueStep(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocRemoveQueueStepMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        if (msg.Index < 0 || msg.Index >= podComp.Queue.Count)
            return;

        podComp.Queue.RemoveAt(msg.Index);
        if (podComp.Queue.Count == 0)
            StopPod(pod, podComp);

        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnClearQueue(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocClearQueueMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        StopPod(pod, podComp);
        podComp.Queue.Clear();
        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnStart(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocStartMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp) ||
            podComp.Queue.Count == 0 ||
            podComp.BodyContainer.ContainedEntity is not { } patient)
        {
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        podComp.Operator = msg.Actor;
        if (!StartCurrentQueuedProcedure(patient, podComp))
        {
            podComp.Queue.Clear();
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        podComp.IsRunning = true;
        _appearance.SetData(pod, CMUAutodocVisuals.Operating, true);
        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnStop(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocStopMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        StopPod(pod, podComp);
        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnEjectPatient(Entity<CMUAutodocConsoleComponent> ent, ref CMUAutodocEjectPatientMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!TryFindLinkedPod(ent.Owner, ent.Comp, out var pod, out var podComp))
            return;

        EjectPatient(pod, podComp);
        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void ProcessPod(EntityUid pod, CMUAutodocPodComponent comp)
    {
        if (comp.Queue.Count == 0 ||
            comp.Operator == default ||
            !comp.Operator.IsValid() ||
            comp.BodyContainer.ContainedEntity is not { } patient)
        {
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        var attempt = comp.CurrentAttempt;
        if (attempt.Region == BodyRegion.None ||
            !TryComp<HumanMedicalComponent>(patient, out var medical))
        {
            DropCompletedQueueItem(patient, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        if (!_surgery.TryReserveOperation(patient, attempt, out _))
        {
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        var result = _surgery.TryApplySurgery(patient, attempt, medical, comp.Operator);
        if (!result.Applied)
        {
            _surgery.CancelUncommittedOperation(patient, attempt);
            StopPod(pod, comp);
            RefreshLinkedConsoles(pod);
            return;
        }

        _surgery.MarkOperationApplied(patient, attempt);

        if (!StartCurrentQueuedProcedure(patient, comp))
            DropCompletedQueueItem(patient, comp);

        if (comp.Queue.Count == 0)
        {
            EjectPatient(pod, comp);
            return;
        }

        RefreshLinkedConsoles(pod);
    }

    private bool StartCurrentQueuedProcedure(EntityUid patient, CMUAutodocPodComponent comp)
    {
        while (comp.Queue.Count > 0)
        {
            var queued = comp.Queue[0];
            if (TryCreateNextAttempt(patient, queued, out var attempt, out var stepLabel))
            {
                comp.CurrentAttempt = attempt;
                comp.CurrentStep = FormatQueuedStep(queued, stepLabel);
                comp.NextStepAt = _timing.CurTime + TimeSpan.FromSeconds(MathF.Max(1f, queued.DurationSeconds));
                PlayProcedureSound(patient, queued);
                return true;
            }

            comp.Queue.RemoveAt(0);
        }

        comp.CurrentAttempt = default;
        comp.CurrentStep = null;
        return false;
    }

    private void DropCompletedQueueItem(EntityUid patient, CMUAutodocPodComponent comp)
    {
        if (comp.Queue.Count > 0)
            comp.Queue.RemoveAt(0);

        if (comp.Queue.Count == 0)
        {
            comp.CurrentAttempt = default;
            comp.CurrentStep = null;
            comp.IsRunning = false;
            return;
        }

        StartCurrentQueuedProcedure(patient, comp);
    }

    private void StopPod(EntityUid pod, CMUAutodocPodComponent comp)
    {
        if (comp.BodyContainer.ContainedEntity is { } patient)
            _surgery.ClearActiveOperations(patient);

        comp.IsRunning = false;
        comp.CurrentStep = null;
        comp.CurrentAttempt = default;
        comp.NextStepAt = TimeSpan.Zero;
        _appearance.SetData(pod, CMUAutodocVisuals.Operating, false);
    }

    private bool TryCreateNextAttempt(
        EntityUid patient,
        CMUAutodocQueuedStep queued,
        out SurgeryAttempt attempt,
        out string stepLabel)
    {
        attempt = default;
        stepLabel = "cmu-autodoc-automated-step-label";

        if (!TryComp<HumanMedicalComponent>(patient, out var medical))
            return false;

        var regionState = HumanMedicalLedger.GetRegion(medical, queued.Region);
        if (queued.ProcedureId != SurgeryProcedureId.SutureWound &&
            queued.ProcedureId != SurgeryProcedureId.CloseIncision &&
            regionState.Incision == IncisionDepth.Closed)
        {
            return BuildAttempt(queued, SurgeryStepKind.OpenIncision, 0, OrganSlot.None, 0, 0, out attempt, out stepLabel);
        }

        if (queued.ProcedureId != SurgeryProcedureId.SutureWound &&
            queued.ProcedureId != SurgeryProcedureId.CloseIncision &&
            regionState.Incision == IncisionDepth.OpenSkin)
        {
            if (HasActiveSurgicalBleed(medical, queued.Region))
                return BuildAttempt(queued, SurgeryStepKind.ClampBleeders, 1, OrganSlot.None, 0, 0, out attempt, out stepLabel);

            return BuildAttempt(queued, SurgeryStepKind.RetractIncision, 2, OrganSlot.None, 0, 0, out attempt, out stepLabel);
        }

        if (NeedsDeepAccess(medical, queued) && regionState.Incision == IncisionDepth.Retracted)
            return BuildAttempt(queued, SurgeryStepKind.DeepAccess, 3, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        if (TryCreateRepairAttempt(medical, queued, out attempt, out stepLabel))
            return true;

        if (regionState.Incision == IncisionDepth.DeepAccess)
            return BuildAttempt(queued, SurgeryStepKind.MendBoneAccess, 8, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        if (regionState.Incision != IncisionDepth.Closed)
            return BuildAttempt(queued, SurgeryStepKind.CloseIncision, 9, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        return false;
    }

    private bool TryCreateRepairAttempt(
        HumanMedicalComponent medical,
        CMUAutodocQueuedStep queued,
        out SurgeryAttempt attempt,
        out string stepLabel)
    {
        switch (queued.ProcedureId)
        {
            case SurgeryProcedureId.SutureWound:
                if (HumanSurgeryProcedureRules.TryFindSuturableWound(medical, queued.Region, out var wound))
                    return BuildAttempt(queued, SurgeryStepKind.SutureWound, 4, OrganSlot.None, wound.Id, FindActiveBleedId(medical, queued.Region), out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.SealStump:
                if (HumanSurgeryProcedureRules.TryFindOpenStump(medical, queued.Region, out var stump))
                    return BuildAttempt(queued, SurgeryStepKind.RepairStump, 4, OrganSlot.None, stump.Id, FindActiveBleedId(medical, queued.Region), out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.RepairInternalBleeding:
                if (TryFindInternalBleed(medical, queued.Region, out var bleed))
                    return BuildAttempt(queued, SurgeryStepKind.RepairInternalBleed, 4, OrganSlot.None, 0, bleed.Id, out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.RemoveEschar:
                if (HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, queued.Region, out var burn))
                    return BuildAttempt(queued, SurgeryStepKind.RemoveEschar, 4, OrganSlot.None, burn.Id, 0, out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.RepairOrgan:
                if (TryFindDamagedOrgan(medical, queued.Region, out var organ))
                    return BuildAttempt(queued, SurgeryStepKind.RepairOrgan, 4, organ.Slot, 0, 0, out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.EyeSurgery:
                if (IsOrganDamaged(medical, OrganSlot.Eyes, queued.Region))
                    return BuildAttempt(queued, SurgeryStepKind.RepairEyes, 4, OrganSlot.Eyes, 0, 0, out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.BrainDamageSurgery:
                if (IsOrganDamaged(medical, OrganSlot.Brain, queued.Region))
                    return BuildAttempt(queued, SurgeryStepKind.RepairBrainDamage, 4, OrganSlot.Brain, 0, 0, out attempt, out stepLabel);
                break;
            case SurgeryProcedureId.RepairFracture:
                return TryCreateFractureAttempt(medical, queued, out attempt, out stepLabel);
        }

        attempt = default;
        stepLabel = "cmu-autodoc-automated-step-label";
        return false;
    }

    private bool TryCreateFractureAttempt(
        HumanMedicalComponent medical,
        CMUAutodocQueuedStep queued,
        out SurgeryAttempt attempt,
        out string stepLabel)
    {
        var skeletal = HumanMedicalLedger.GetRegion(medical, queued.Region).Skeletal;
        if (!skeletal.Broken)
        {
            attempt = default;
            stepLabel = "cmu-autodoc-automated-step-label";
            return false;
        }

        if (!skeletal.BoneGelApplied)
            return BuildAttempt(queued, SurgeryStepKind.ApplyBoneGel, 4, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        if (!skeletal.BoneSet)
            return BuildAttempt(queued, SurgeryStepKind.SetBone, 5, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        if (skeletal.Severity.IsAtLeast(FractureSeverity.Shattered) && !skeletal.BoneGrafted)
            return BuildAttempt(queued, SurgeryStepKind.ApplyBoneGraft, 6, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        if (skeletal.Severity.IsAtLeast(FractureSeverity.Shattered))
            return BuildAttempt(queued, SurgeryStepKind.SetGraftedBone, 7, OrganSlot.None, 0, 0, out attempt, out stepLabel);

        return BuildAttempt(queued, SurgeryStepKind.SealBoneWithGel, 6, OrganSlot.None, 0, 0, out attempt, out stepLabel);
    }

    private static bool BuildAttempt(
        CMUAutodocQueuedStep queued,
        SurgeryStepKind step,
        int stepIndex,
        OrganSlot organ,
        int injuryId,
        int bleedSourceId,
        out SurgeryAttempt attempt,
        out string stepLabel)
    {
        stepLabel = LabelForStep(step);
        attempt = new SurgeryAttempt(
            queued.Region,
            step,
            organ,
            injuryId,
            bleedSourceId,
            PatientAnesthetized: true,
            PatientPainkilled: true,
            PainRequirement: SurgeryPainRequirement.None,
            ToolQuality: SurgeryToolQuality.Ideal,
            SurfaceQuality: SurgerySurfaceQuality.Ideal,
            RequiredSurgerySkill: 0,
            LyingRequired: false,
            SelfOperable: true,
            BaseDelay: TimeSpan.FromSeconds(queued.DurationSeconds),
            ProcedureId: queued.ProcedureId,
            StepIndex: stepIndex,
            ToolRole: SurgeryToolRole.None);
        return true;
    }

    private static string LabelForStep(SurgeryStepKind step)
    {
        return step switch
        {
            SurgeryStepKind.OpenIncision => "cmu-autodoc-step-open-incision",
            SurgeryStepKind.ClampBleeders => "cmu-autodoc-step-clamp-bleeders",
            SurgeryStepKind.RetractIncision => "cmu-autodoc-step-retract-incision",
            SurgeryStepKind.DeepAccess => "cmu-autodoc-step-open-bone",
            SurgeryStepKind.MendBoneAccess => "cmu-autodoc-step-mend-bone-access",
            SurgeryStepKind.CloseIncision => "cmu-autodoc-step-close-incision",
            SurgeryStepKind.SutureWound => "cmu-autodoc-step-suture-wound",
            SurgeryStepKind.RepairStump => "cmu-autodoc-step-seal-stump",
            SurgeryStepKind.RepairInternalBleed => "cmu-autodoc-step-repair-ib",
            SurgeryStepKind.RemoveEschar => "cmu-autodoc-step-remove-eschar",
            SurgeryStepKind.RepairOrgan => "cmu-autodoc-step-repair-organ",
            SurgeryStepKind.RepairEyes => "cmu-autodoc-step-repair-eyes",
            SurgeryStepKind.RepairBrainDamage => "cmu-autodoc-step-repair-brain",
            SurgeryStepKind.ApplyBoneGel => "cmu-autodoc-step-apply-bone-gel",
            SurgeryStepKind.SetBone => "cmu-autodoc-step-set-bone",
            SurgeryStepKind.SealBoneWithGel => "cmu-autodoc-step-seal-bone",
            SurgeryStepKind.ApplyBoneGraft => "cmu-autodoc-step-apply-bone-graft",
            SurgeryStepKind.SetGraftedBone => "cmu-autodoc-step-set-grafted-bone",
            _ => "cmu-autodoc-automated-step-label",
        };
    }

    private static bool NeedsDeepAccess(HumanMedicalComponent medical, CMUAutodocQueuedStep queued)
    {
        if (queued.Region is not (BodyRegion.Head or BodyRegion.Chest))
            return false;

        if (queued.ProcedureId is SurgeryProcedureId.RepairOrgan or SurgeryProcedureId.EyeSurgery or SurgeryProcedureId.BrainDamageSurgery)
            return true;

        if (queued.ProcedureId != SurgeryProcedureId.RepairFracture)
            return false;

        var state = HumanMedicalLedger.GetRegion(medical, queued.Region);
        return !(state.Skeletal.Broken &&
                 state.Skeletal.Severity.IsAtLeast(FractureSeverity.Compound) &&
                 state.Incision >= IncisionDepth.Retracted);
    }

    private List<CMUSurgeryPartEntry> BuildAutodocPartEntries(EntityUid patient)
    {
        var parts = new List<CMUSurgeryPartEntry>();
        if (!TryComp<HumanMedicalComponent>(patient, out var medical))
            return parts;

        foreach (var regionState in medical.Regions)
        {
            if (regionState.Region == BodyRegion.None)
                continue;

            var surgeries = BuildSurgeryEntries(medical, regionState);
            if (surgeries.Count == 0)
                continue;

            var (partUid, type, symmetry) = ResolvePartForRegion(patient, regionState.Region);
            parts.Add(new CMUSurgeryPartEntry(
                GetNetEntity(partUid),
                type,
                symmetry,
                regionState.Region,
                Loc.GetString(HumanMedicalScannerBuiSystem.GetRegionLocKey(regionState.Region)),
                BuildConditionSummary(medical, regionState),
                false,
                false,
                surgeries));
        }

        return parts;
    }

    private List<CMUSurgeryEntry> BuildSurgeryEntries(HumanMedicalComponent medical, RegionState regionState)
    {
        var entries = new List<CMUSurgeryEntry>();
        var region = regionState.Region;

        if (HumanSurgeryProcedureRules.TryFindSuturableWound(medical, region, out _))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.SutureWound, "cmu-autodoc-repair-wounds-surgery", "suture"));

        if (HumanSurgeryProcedureRules.TryFindOpenStump(medical, region, out _))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.SealStump, "cmu-autodoc-seal-stump-surgery", "suture"));

        if (TryFindInternalBleed(medical, region, out _))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.RepairInternalBleeding, "cmu-autodoc-repair-ib-surgery", "bleed"));

        if (HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, region, out _))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.RemoveEschar, "cmu-autodoc-remove-eschar-surgery", "burn"));

        if (TryFindDamagedOrgan(medical, region, out _))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.RepairOrgan, "cmu-autodoc-repair-organ-surgery", "general"));

        if (IsOrganDamaged(medical, OrganSlot.Eyes, region))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.EyeSurgery, "cmu-autodoc-eye-surgery", "head_organ"));

        if (IsOrganDamaged(medical, OrganSlot.Brain, region))
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.BrainDamageSurgery, "cmu-autodoc-brain-surgery", "head_organ"));

        if (regionState.Skeletal.Broken)
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.RepairFracture, "cmu-autodoc-repair-fracture-surgery", "fracture"));

        if (regionState.Incision != IncisionDepth.Closed)
            entries.Add(MakeSurgeryEntry(SurgeryProcedureId.CloseIncision, "cmu-autodoc-close-incision-surgery", "close_up"));

        return entries;
    }

    private static CMUSurgeryEntry MakeSurgeryEntry(SurgeryProcedureId id, string displayName, string category)
    {
        return new CMUSurgeryEntry(
            id.ToString(),
            displayName,
            "cmu-autodoc-automated-step-label",
            null,
            0,
            1,
            null,
            category);
    }

    private string BuildConditionSummary(HumanMedicalComponent medical, RegionState regionState)
    {
        var conditions = new List<string>();
        if (regionState.Presence != LimbPresence.Present)
            conditions.Add(regionState.Presence.ToString());
        if (regionState.Incision != IncisionDepth.Closed)
            conditions.Add(Loc.GetString("cmu-medical-surgery-condition-incision-open"));
        if (regionState.Skeletal.Broken)
            conditions.Add(Loc.GetString("cmu-medical-surgery-condition-fracture", ("severity", regionState.Skeletal.Severity)));
        if (HumanSurgeryProcedureRules.TryFindSuturableWound(medical, regionState.Region, out _))
            conditions.Add(Loc.GetString("cmu-medical-surgery-condition-wounds"));
        if (TryFindInternalBleed(medical, regionState.Region, out _))
            conditions.Add(Loc.GetString("cmu-medical-surgery-condition-internal-bleed"));
        if (HumanSurgeryProcedureRules.TryFindDebridementBurn(medical, regionState.Region, out _))
            conditions.Add(Loc.GetString("cmu-medical-surgery-condition-eschar"));

        return conditions.Count == 0
            ? Loc.GetString("cmu-medical-surgery-part-condition-healthy")
            : string.Join(", ", conditions);
    }

    private (EntityUid Part, BodyPartType Type, BodyPartSymmetry Symmetry) ResolvePartForRegion(EntityUid patient, BodyRegion region)
    {
        var (type, symmetry) = PartForRegion(region);
        if (region == BodyRegion.Groin)
            return (patient, type, symmetry);

        foreach (var (candidateUid, candidatePart) in _body.GetBodyChildren(patient))
        {
            if (candidatePart.PartType == type && candidatePart.Symmetry == symmetry)
                return (candidateUid, type, symmetry);
        }

        return (patient, type, symmetry);
    }

    private static (BodyPartType Type, BodyPartSymmetry Symmetry) PartForRegion(BodyRegion region)
    {
        return region switch
        {
            BodyRegion.Head => (BodyPartType.Head, BodyPartSymmetry.None),
            BodyRegion.Groin => (BodyPartType.Torso, BodyPartSymmetry.None),
            BodyRegion.LeftArm => (BodyPartType.Arm, BodyPartSymmetry.Left),
            BodyRegion.RightArm => (BodyPartType.Arm, BodyPartSymmetry.Right),
            BodyRegion.LeftHand => (BodyPartType.Hand, BodyPartSymmetry.Left),
            BodyRegion.RightHand => (BodyPartType.Hand, BodyPartSymmetry.Right),
            BodyRegion.LeftLeg => (BodyPartType.Leg, BodyPartSymmetry.Left),
            BodyRegion.RightLeg => (BodyPartType.Leg, BodyPartSymmetry.Right),
            BodyRegion.LeftFoot => (BodyPartType.Foot, BodyPartSymmetry.Left),
            BodyRegion.RightFoot => (BodyPartType.Foot, BodyPartSymmetry.Right),
            _ => (BodyPartType.Torso, BodyPartSymmetry.None),
        };
    }

    private bool TryFindLinkedPod(
        EntityUid console,
        CMUAutodocConsoleComponent comp,
        out EntityUid pod,
        out CMUAutodocPodComponent podComp)
    {
        pod = default;
        podComp = default!;
        var consoleCoords = Transform(console).Coordinates;
        var bestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<CMUAutodocPodComponent>(consoleCoords, comp.LinkRange))
        {
            if (!consoleCoords.TryDistance(EntityManager, Transform(candidate).Coordinates, out var distance) ||
                distance >= bestDistance)
            {
                continue;
            }

            pod = candidate;
            podComp = Comp<CMUAutodocPodComponent>(candidate);
            bestDistance = distance;
        }

        return pod.IsValid();
    }

    private void OnPodInit(Entity<CMUAutodocPodComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _containers.EnsureContainer<ContainerSlot>(ent.Owner, CMUAutodocPodComponent.BodyContainerId);
        UpdatePodAppearance(ent.Owner, ent.Comp);
    }

    private void OnPodDestroyed(Entity<CMUAutodocPodComponent> ent, ref DestructionEventArgs args)
    {
        EjectPatient(ent.Owner, ent.Comp);
    }

    private void OnPodDragDrop(Entity<CMUAutodocPodComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !CanInsertPatient(ent.Comp, args.Dragged))
            return;

        StartInsertDoAfter(ent.Owner, ent.Comp, args.User, args.Dragged);
        args.Handled = true;
    }

    private void OnPodAlternativeVerbs(Entity<CMUAutodocPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.BodyContainer.ContainedEntity is { })
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => EjectPatient(ent.Owner, ent.Comp),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("medical-scanner-verb-noun-occupant"),
                Priority = 1,
            });
            return;
        }

        var user = args.User;
        if (!CanInsertPatient(ent.Comp, user))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartInsertDoAfter(ent.Owner, ent.Comp, user, user),
            Text = Loc.GetString("medical-scanner-verb-enter"),
            Priority = 2,
        });
    }

    private void OnPodRelayMovement(Entity<CMUAutodocPodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (ent.Comp.BodyContainer.ContainedEntity != args.Entity)
            return;

        EjectPatient(ent.Owner, ent.Comp);
    }

    private void StartInsertDoAfter(EntityUid pod, CMUAutodocPodComponent comp, EntityUid user, EntityUid target)
    {
        var doAfter = new DoAfterArgs(EntityManager, user, comp.EntryDelay, new CMUMedicalPodInsertDoAfterEvent(), pod, target, pod)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
            CancelDuplicate = false,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnPodInsertDoAfter(Entity<CMUAutodocPodComponent> ent, ref CMUMedicalPodInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        InsertPatient(ent.Owner, ent.Comp, target);
        args.Handled = true;
    }

    private bool CanInsertPatient(CMUAutodocPodComponent comp, EntityUid patient)
    {
        return comp.BodyContainer.ContainedEntity is null && HasComp<BodyComponent>(patient);
    }

    private bool InsertPatient(EntityUid pod, CMUAutodocPodComponent comp, EntityUid patient)
    {
        if (!CanInsertPatient(comp, patient) || !_containers.Insert(patient, comp.BodyContainer))
            return false;

        EnsureComp<CMUAutodocContainedPatientComponent>(patient);
        UpdatePodAppearance(pod, comp);
        RefreshLinkedConsoles(pod);
        return true;
    }

    private EntityUid? EjectPatient(EntityUid pod, CMUAutodocPodComponent comp)
    {
        StopPod(pod, comp);
        comp.Queue.Clear();

        if (comp.BodyContainer.ContainedEntity is not { } patient)
            return null;

        RemCompDeferred<CMUAutodocContainedPatientComponent>(patient);
        _containers.Remove(patient, comp.BodyContainer);
        MoveEjectedPatientToPod(pod, patient);
        UpdatePodAppearance(pod, comp);
        RefreshLinkedConsoles(pod);
        return patient;
    }

    private void MoveEjectedPatientToPod(EntityUid pod, EntityUid patient)
    {
        if (TerminatingOrDeleted(patient))
            return;

        var podCoords = Transform(pod).Coordinates;
        _transform.SetCoordinates(patient, GetPodEjectCoordinates(podCoords));
    }

    private EntityCoordinates GetPodEjectCoordinates(EntityCoordinates podCoords)
    {
        foreach (var offset in EjectOffsets)
        {
            var candidate = podCoords.Offset(offset);
            if (CanEjectTo(candidate))
                return candidate;
        }

        return podCoords;
    }

    private bool CanEjectTo(EntityCoordinates coordinates)
    {
        return _turf.TryGetTileRef(coordinates, out var tile) &&
               !tile.Value.Tile.IsEmpty &&
               !_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }

    private void UpdatePodAppearance(EntityUid pod, CMUAutodocPodComponent comp)
    {
        _appearance.SetData(pod, CMUMedicalPodVisuals.Occupied, comp.BodyContainer.ContainedEntity is not null);
        _appearance.SetData(pod, CMUAutodocVisuals.Operating, comp.IsRunning);
    }

    private void RefreshLinkedConsoles(EntityUid pod)
    {
        var query = EntityQueryEnumerator<CMUAutodocConsoleComponent>();
        while (query.MoveNext(out var console, out var consoleComp))
        {
            if (!TryFindLinkedPod(console, consoleComp, out var linkedPod, out _) || linkedPod != pod)
                continue;

            RefreshUi(console, consoleComp, consoleComp.LastViewer);
        }
    }

    private void RefreshUi(EntityUid console, CMUAutodocConsoleComponent comp, EntityUid? viewer = null)
    {
        if (!_ui.HasUi(console, CMUAutodocUIKey.Key))
            return;

        if (viewer is not { } validViewer || !validViewer.IsValid())
            viewer = comp.LastViewer.IsValid() ? comp.LastViewer : null;

        _ui.SetUiState(console, CMUAutodocUIKey.Key, BuildState(console, comp));
    }

    private CMUAutodocBuiState BuildState(EntityUid console, CMUAutodocConsoleComponent comp)
    {
        var podLinked = TryFindLinkedPod(console, comp, out var pod, out var podComp);
        EntityUid? patient = podLinked ? podComp.BodyContainer.ContainedEntity : null;
        var canQueue = podLinked && patient is not null && !podComp.IsRunning;
        var parts = patient is { } patientUid ? BuildAutodocPartEntries(patientUid) : [];
        var queue = podLinked ? BuildQueueEntries(podComp) : [];
        var status = !podLinked
            ? Loc.GetString("cmu-autodoc-status-no-pod")
            : patient is null
                ? Loc.GetString("cmu-autodoc-status-empty")
                : podComp.IsRunning
                    ? Loc.GetString("cmu-autodoc-status-running")
                    : Loc.GetString("cmu-autodoc-status-ready");

        return new CMUAutodocBuiState(
            podLinked ? GetNetEntity(pod) : null,
            patient is { } patientEntity ? GetNetEntity(patientEntity) : null,
            patient is { } named ? Name(named) : Loc.GetString("cmu-autodoc-no-patient"),
            podLinked,
            canQueue,
            podLinked && podComp.IsRunning,
            status,
            podLinked ? podComp.CurrentStep : null,
            podLinked && podComp.IsRunning ? podComp.NextStepAt : null,
            parts,
            queue);
    }

    private List<CMUAutodocQueueEntry> BuildQueueEntries(CMUAutodocPodComponent podComp)
    {
        var entries = new List<CMUAutodocQueueEntry>();
        for (var i = 0; i < podComp.Queue.Count; i++)
        {
            var queued = podComp.Queue[i];
            entries.Add(new CMUAutodocQueueEntry(
                i,
                GetNetEntity(queued.Part),
                queued.Type,
                queued.Symmetry,
                queued.PartDisplayName,
                queued.ProcedureId.ToString(),
                queued.SurgeryDisplayName,
                queued.Category,
                queued.StepIndex,
                queued.StepLabel,
                queued.DurationSeconds));
        }

        return entries;
    }

    private void PlayProcedureSound(EntityUid patient, CMUAutodocQueuedStep queued)
    {
        if (ProcedureSounds.TryGetValue(queued.Category, out var sound))
            _audio.PlayPvs(sound, patient);
    }

    private string FormatQueuedStep(CMUAutodocQueuedStep queued, string stepLabel)
    {
        return Loc.GetString(
            "cmu-autodoc-current-step-detail",
            ("surgery", ResolveLabel(queued.SurgeryDisplayName)),
            ("part", queued.PartDisplayName),
            ("step", ResolveLabel(stepLabel)));
    }

    private string ResolveLabel(string label)
    {
        return Loc.TryGetString(label, out var localized) ? localized : label;
    }

    private static float GetProcedureDurationSeconds(SurgeryProcedureId id)
    {
        return id switch
        {
            SurgeryProcedureId.SutureWound => QuickProcedureSeconds,
            SurgeryProcedureId.CloseIncision => QuickProcedureSeconds,
            SurgeryProcedureId.RepairFracture => ComplexProcedureSeconds,
            SurgeryProcedureId.RepairOrgan => ComplexProcedureSeconds,
            SurgeryProcedureId.EyeSurgery => ComplexProcedureSeconds,
            SurgeryProcedureId.BrainDamageSurgery => ComplexProcedureSeconds,
            _ => DefaultProcedureSeconds,
        };
    }

    private static bool HasActiveSurgicalBleed(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region == region &&
                source.Kind == BleedKind.External &&
                source.Flags.HasFlag(BleedFlags.Surgical) &&
                source.Active)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindInternalBleed(HumanMedicalComponent medical, BodyRegion region, out BleedSource bleed)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region != region ||
                source.Kind != BleedKind.Internal ||
                source.Treatment.HasFlag(TreatmentFlags.Closed) ||
                source.Treatment.HasFlag(TreatmentFlags.Sutured))
            {
                continue;
            }

            bleed = source;
            return true;
        }

        bleed = default;
        return false;
    }

    private static bool TryFindDamagedOrgan(HumanMedicalComponent medical, BodyRegion region, out OrganState organ)
    {
        for (var i = 1; i < medical.Organs.Length; i++)
        {
            organ = medical.Organs[i];
            if (organ.Region == region &&
                organ.Damage > FixedPoint2.Zero &&
                !organ.Missing &&
                organ.Slot is not OrganSlot.Brain and not OrganSlot.Eyes)
            {
                return true;
            }
        }

        organ = default;
        return false;
    }

    private static bool IsOrganDamaged(HumanMedicalComponent medical, OrganSlot slot, BodyRegion region)
    {
        var organ = HumanMedicalLedger.GetOrgan(medical, slot);
        return organ.Slot == slot &&
               organ.Region == region &&
               organ.Damage > FixedPoint2.Zero &&
               !organ.Missing;
    }

    private static int FindActiveBleedId(HumanMedicalComponent medical, BodyRegion region)
    {
        foreach (var source in medical.BleedSources)
        {
            if (source.Region == region && source.Active)
                return source.Id;
        }

        return 0;
    }
}
