using System;
using System.Collections.Generic;
using Content.Shared.Destructible;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.DragDrop;
using Content.Shared.Movement.Events;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Medical.Treatment.Surgery;

public sealed partial class CMUBodyScannerSystem : EntitySystem
{
    [Dependency] private CMUBodyScannerCalibrationSystem _calibration = default!;
    [Dependency] private CMUBodyScannerReadoutSystem _readout = default!;
    [Dependency] private CMUMedicalPatientBaySystem _patientBay = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";

    private readonly HashSet<EntityUid> _openConsoles = new();
    private readonly List<EntityUid> _staleConsoles = new();
    private float _uiAccumulator;

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
        if (!CanUsePuzzle(ent.Owner, ent.Comp, msg.Actor, out var patient))
            return;

        if (_calibration.TryConfirmPuzzle(msg.Actor, patient, ent.Comp, msg.LayerId, msg.SignalId, msg.ClientPhase))
            RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnResetPuzzle(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerResetPuzzleMessage msg)
    {
        if (!CanUsePuzzle(ent.Owner, ent.Comp, msg.Actor, out var patient))
            return;

        if (_calibration.ResetPuzzle(msg.Actor, patient, ent.Comp))
            RefreshUi(ent.Owner, ent.Comp);
    }

    private void OnEjectPatient(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerEjectPatientMessage msg)
    {
        if (!_skills.HasSkill(msg.Actor, SurgerySkill, 1))
            return;

        if (!TryFindLinkedScanner(ent.Owner, ent.Comp, out var pod, out var podComp)
            || !_patientBay.TryGetPatient(podComp.BodyContainer, out _))
        {
            return;
        }

        EjectPatient(pod, podComp);
        RefreshUi(ent.Owner, ent.Comp);
    }

    private bool CanUsePuzzle(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid user, out EntityUid patient)
    {
        patient = default;
        if (!_skills.HasSkill(user, SurgerySkill, 1))
            return false;

        if (!TryFindLinkedScanner(console, comp, out _, out var scanner)
            || !_patientBay.TryGetPatient(scanner.BodyContainer, out patient))
        {
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
        var state = BuildState(console, comp, viewer);
        _ui.ServerSendUiMessage(
            console,
            CMUBodyScannerUIKey.Key,
            new CMUBodyScannerStateMessage(state),
            viewer);
    }

    private CMUBodyScannerBuiState BuildState(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid? viewer)
    {
        var podLinked = TryFindLinkedScanner(console, comp, out var pod, out var scanner);
        EntityUid? patient = podLinked ? scanner.BodyContainer.ContainedEntity : null;
        var canScan = viewer is { } user && patient is { } body && _skills.HasSkill(user, SurgerySkill, 1);
        var calibration = _calibration.BuildView(viewer, patient, canScan, comp);

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
            calibration.Assignments);
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
        _patientBay.UpdatePodAppearance(ent.Owner, ent.Comp.BodyContainer);
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

        if (_patientBay.TryGetPatient(ent.Comp.BodyContainer, out _))
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

        _patientBay.TryEjectPatient(pod, comp.BodyContainer, patient);
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
