using System;
using System.Collections.Generic;
using Content.Shared.Destructible;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Movement.Events;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUBodyScannerSystem : EntitySystem
{
    [Dependency] private CMUBodyScannerCalibrationSystem _calibration = default!;
    [Dependency] private CMUBodyScannerReadoutSystem _readout = default!;
    [Dependency] private CMUMedicalPatientBaySystem _patientBay = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    private readonly HashSet<EntityUid> _openConsoles = new();
    private readonly List<EntityUid> _staleConsoles = new();
    private float _uiAccumulator;
    private ulong _nextOccupantGeneration;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMUBodyScannerConsoleComponent>(CMUBodyScannerUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BoundUIClosedEvent>(OnUiClosed);
            subs.Event<CMUBodyScannerConfirmPuzzleMessage>(OnConfirmPuzzle);
            subs.Event<CMUBodyScannerResetPuzzleMessage>(OnResetPuzzle);
            subs.Event<CMUBodyScannerEjectPatientMessage>(OnEjectPatient);
        });

        SubscribeLocalEvent<CMUBodyScannerPodComponent, ComponentInit>(OnPodInit);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, EntInsertedIntoContainerMessage>(OnPatientInserted);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, EntRemovedFromContainerMessage>(OnPatientRemoved);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, DestructionEventArgs>(OnPodDestroyed);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, DragDropTargetEvent>(OnPodDragDrop);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, GetVerbsEvent<AlternativeVerb>>(OnPodAlternativeVerbs);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, ContainerRelayMovementEntityEvent>(OnPodRelayMovement);
        SubscribeLocalEvent<CMUBodyScannerPodComponent, CMUMedicalPodInsertDoAfterEvent>(OnPodInsertDoAfter);
        SubscribeLocalEvent<CMUBodyScannerConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _uiAccumulator += frameTime;
        if (_uiAccumulator < 1f)
            return;

        _uiAccumulator = 0f;
        _staleConsoles.Clear();
        foreach (var console in _openConsoles)
        {
            if (!TryComp<CMUBodyScannerConsoleComponent>(console, out var comp) ||
                !_ui.IsUiOpen(console, CMUBodyScannerUIKey.Key))
            {
                _staleConsoles.Add(console);
                continue;
            }

            RefreshUi(console, comp);
        }

        foreach (var console in _staleConsoles)
            _openConsoles.Remove(console);
    }

    public float GetSurgeryDelayMultiplier(EntityUid surgeon, EntityUid patient)
    {
        return _calibration.GetSurgeryDelayMultiplier(surgeon, patient);
    }

    private void OnUiOpened(Entity<CMUBodyScannerConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        _openConsoles.Add(ent.Owner);
        RefreshUi(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnUiClosed(Entity<CMUBodyScannerConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!_ui.IsUiOpen(ent.Owner, CMUBodyScannerUIKey.Key))
            _openConsoles.Remove(ent.Owner);
    }

    private void OnConsoleShutdown(Entity<CMUBodyScannerConsoleComponent> ent, ref ComponentShutdown args)
    {
        _openConsoles.Remove(ent.Owner);
    }

    private void OnConfirmPuzzle(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerConfirmPuzzleMessage msg)
    {
        if (!ValidateCommand(ent, msg.Actor, msg.Context, out var patient, out var origin))
            return;

        if (msg.Context.Attempt == 0)
            return;

        if (_calibration.TryConfirmPuzzle(msg.Actor, patient, ent.Comp, msg.LayerId, msg.SignalId, msg.ClientPhase,
                msg.Context.Attempt, msg.ExpectedAssignments, origin))
            RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnResetPuzzle(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerResetPuzzleMessage msg)
    {
        if (!ValidateCommand(ent, msg.Actor, msg.Context, out var patient, out var origin))
            return;

        if (_calibration.ResetPuzzle(msg.Actor, patient, ent.Comp, origin))
            RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectPatient(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerEjectPatientMessage msg)
    {
        if (!ValidateCommand(ent, msg.Actor, msg.Context, out _, out var origin))
            return;

        if (!TryComp<CMUBodyScannerPodComponent>(origin.Pod, out var podComp))
            return;

        EjectPatient(origin.Pod, podComp);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private bool ValidateCommand(Entity<CMUBodyScannerConsoleComponent> console, EntityUid user,
        CMUBodyScannerCommandContext context, out EntityUid patient, out CMUBodyScannerOrigin origin)
    {
        patient = default;
        origin = default;
        if (TerminatingOrDeleted(user) || Paused(user) || !_skills.HasSkill(user, SurgerySkill, 1)
            || !_interaction.InRangeAndAccessible(user, console.Owner))
            return false;

        if (!TryFindLinkedScanner(console.Owner, console.Comp, out var pod, out var scanner)
            || !_patientBay.TryGetPatient(scanner.BodyContainer, out patient)
            || TerminatingOrDeleted(patient))
        {
            return false;
        }
        origin = new CMUBodyScannerOrigin(console.Owner, pod, scanner.OccupantGeneration);
        if (context.Console != GetNetEntity(console.Owner) || context.Pod != GetNetEntity(pod)
            || context.Patient != GetNetEntity(patient) || scanner.Patient != patient
            || context.OccupantGeneration != scanner.OccupantGeneration
            || context.OperatorRevision != _calibration.GetRevision(user)
            || context.Attempt != _calibration.GetAttempt(user))
        {
            RefreshUi(console.Owner, console.Comp, user);
            return false;
        }
        return true;
    }

    private void RefreshUi(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid? viewer = null)
    {
        if (viewer is { } target && target.IsValid())
        {
            if (_ui.IsUiOpen(console, CMUBodyScannerUIKey.Key, target))
                SendState(console, comp, target);
            return;
        }

        foreach (var actor in _ui.GetActors(console, CMUBodyScannerUIKey.Key))
            SendState(console, comp, actor);
    }

    private void SendState(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid viewer)
    {
        var state = BuildStateForViewer(console, comp, viewer);
        _ui.ServerSendUiMessage(
            console,
            CMUBodyScannerUIKey.Key,
            new CMUBodyScannerStateMessage(state),
            viewer);
    }

    public CMUBodyScannerBuiState BuildStateForViewer(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid? viewer)
    {
        var podLinked = TryFindLinkedScanner(console, comp, out var pod, out var scanner);
        EntityUid? patient = podLinked ? scanner.BodyContainer.ContainedEntity : null;
        var canScan = viewer is { } user && patient is { } body && _skills.HasSkill(user, SurgerySkill, 1);
        var origin = new CMUBodyScannerOrigin(console, pod, podLinked ? scanner.OccupantGeneration : 0);
        var calibration = _calibration.BuildView(viewer, patient, canScan, comp, origin);

        var status = !podLinked
            ? Loc.GetString("cmu-body-scanner-status-no-pod")
            : patient is null
                ? Loc.GetString("cmu-body-scanner-status-empty")
                : canScan
                    ? Loc.GetString("cmu-body-scanner-status-ready")
                    : Loc.GetString("cmu-body-scanner-status-no-skill");

        return new CMUBodyScannerBuiState(
            podLinked ? GetNetEntity(pod) : null,
            patient is { } patientUid ? GetNetEntity(patientUid) : null,
            patient is { } named ? Name(named) : Loc.GetString("cmu-body-scanner-no-patient"),
            podLinked,
            canScan,
            calibration.PuzzleComplete,
            status,
            calibration.BoostExpiresAt,
            calibration.LockoutExpiresAt,
            calibration.StartedAt,
            calibration.EndsAt,
            calibration.PulseStartedAt,
            calibration.PulsePeriod,
            calibration.PulseTargetPhase,
            calibration.PulseWindowSize,
            calibration.PulseGraceSize,
            calibration.LastPenaltyAt,
            calibration.LastPenaltySeconds,
            calibration.LastFeedbackAt,
            calibration.LastFeedbackKind,
            canScan && patient is { } scanPatient ? _readout.BuildScanLines(scanPatient) : [],
            calibration.Layers,
            calibration.Targets,
            calibration.Assignments)
        {
            CalibrationAttempt = calibration.AttemptId,
            CommandContext = canScan && viewer is { } actor && patient is { } bodyPatient
                ? new CMUBodyScannerCommandContext(GetNetEntity(console), GetNetEntity(pod), GetNetEntity(bodyPatient),
                    scanner.OccupantGeneration, _calibration.GetRevision(actor), _calibration.GetAttempt(actor))
                : null,
            CanStartCalibration = canScan && viewer is { } startActor && patient is { } startPatient
                && _calibration.CanStart(startActor, startPatient),
            CalibrationActiveElsewhere = canScan && viewer is { } otherActor && patient is { } otherPatient
                && _calibration.HasOtherAttempt(otherActor, otherPatient, origin),
        };
    }

    private bool TryFindLinkedScanner(
        EntityUid console,
        CMUBodyScannerConsoleComponent comp,
        out EntityUid scanner,
        out CMUBodyScannerPodComponent scannerComp)
    {
        return _patientBay.TryFindNearestPod(console, comp.LinkRange, out scanner, out scannerComp);
    }

    private void OnPodInit(Entity<CMUBodyScannerPodComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _patientBay.EnsureBodyContainer(ent.Owner, CMUBodyScannerPodComponent.BodyContainerId);
        SynchronizeOccupant(ent);
        _patientBay.UpdatePodAppearance(ent.Owner, ent.Comp.BodyContainer);
    }

    private void OnPatientInserted(Entity<CMUBodyScannerPodComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container == ent.Comp.BodyContainer)
            SynchronizeOccupant(ent);
    }

    private void OnPatientRemoved(Entity<CMUBodyScannerPodComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container == ent.Comp.BodyContainer)
            SynchronizeOccupant(ent);
    }

    private void SynchronizeOccupant(Entity<CMUBodyScannerPodComponent> ent)
    {
        var patient = ent.Comp.BodyContainer.ContainedEntity;
        if (ent.Comp.Patient == patient)
            return;
        ent.Comp.Patient = patient;
        ent.Comp.OccupantGeneration = ++_nextOccupantGeneration;
        _patientBay.UpdatePodAppearance(ent.Owner, ent.Comp.BodyContainer);
        RefreshLinkedConsoles(ent.Owner);
    }

    private void OnPodDestroyed(Entity<CMUBodyScannerPodComponent> ent, ref DestructionEventArgs args)
    {
        EjectPatient(ent.Owner, ent.Comp);
    }

    private void OnPodDragDrop(Entity<CMUBodyScannerPodComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !_patientBay.CanInsertPatient(ent.Comp.BodyContainer, args.Dragged))
            return;

        StartInsertDoAfter(ent.Owner, ent.Comp, args.User, args.Dragged);
        args.Handled = true;
    }

    private void OnPodAlternativeVerbs(Entity<CMUBodyScannerPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        // Occupant ejection requires the console's visit-bound command context.
        // Generic verbs are reconstructed at execution and cannot bind that visit.
        if (!_patientBay.CanInsertPatient(ent.Comp.BodyContainer, user))
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => StartInsertDoAfter(ent.Owner, ent.Comp, user, user),
            Text = Loc.GetString("medical-scanner-verb-enter"),
            Priority = 2,
        });
    }

    private void OnPodRelayMovement(Entity<CMUBodyScannerPodComponent> ent, ref ContainerRelayMovementEntityEvent args)
    {
        if (!_patientBay.ContainsPatient(ent.Comp.BodyContainer, args.Entity))
            return;

        EjectPatient(ent.Owner, ent.Comp);
    }

    private void StartInsertDoAfter(EntityUid pod, CMUBodyScannerPodComponent comp, EntityUid user, EntityUid target)
    {
        _patientBay.StartInsertDoAfter(pod, user, target, comp.EntryDelay);
    }

    private void OnPodInsertDoAfter(Entity<CMUBodyScannerPodComponent> ent, ref CMUMedicalPodInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        InsertPatient(ent.Owner, ent.Comp, target);
        args.Handled = true;
    }

    private bool InsertPatient(EntityUid pod, CMUBodyScannerPodComponent comp, EntityUid patient)
    {
        if (!_patientBay.TryInsertPatient(pod, comp.BodyContainer, patient))
            return false;

        RefreshLinkedConsoles(pod);
        return true;
    }

    private EntityUid? EjectPatient(EntityUid pod, CMUBodyScannerPodComponent comp)
    {
        if (!_patientBay.TryGetPatient(comp.BodyContainer, out var patient))
            return null;

        if (!_patientBay.TryEjectPatient(pod, comp.BodyContainer, patient))
            return null;
        RefreshLinkedConsoles(pod);
        return patient;
    }

    private void RefreshLinkedConsoles(EntityUid pod)
    {
        var query = EntityQueryEnumerator<CMUBodyScannerConsoleComponent>();
        while (query.MoveNext(out var console, out var consoleComp))
        {
            if (!TryFindLinkedScanner(console, consoleComp, out var linkedPod, out _)
                || linkedPod != pod)
            {
                continue;
            }

            RefreshUi(console, consoleComp);
        }
    }
}
