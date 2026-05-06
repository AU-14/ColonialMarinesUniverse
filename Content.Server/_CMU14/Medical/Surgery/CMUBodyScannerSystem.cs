using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared.Body.Components;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared._CMU14.Medical.BodyPart;
using Content.Shared._CMU14.Medical.Bones;
using Content.Shared._CMU14.Medical.Items;
using Content.Shared._CMU14.Medical.Organs;
using Content.Shared._CMU14.Medical.Organs.Heart;
using Content.Shared._CMU14.Medical.Surgery;
using Content.Shared._CMU14.Medical.Wounds;
using Content.Shared._RMC14.Body;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Physics;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Medical.Surgery;

public sealed class CMUBodyScannerSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedRMCBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SkillsSystem _skills = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> SurgerySkill = "RMCSkillSurgery";
    private const int MaxPuzzleSignals = 8;
    private const string SliceVitals = "vitals";
    private const string SliceSkeleton = "skeleton";
    private const string SliceOrgans = "organs";
    private const string SliceTissue = "tissue";
    private const string DecoySignalPrefix = "noise:";

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

    private static readonly List<CMUBodyScannerPuzzleChoice> ScannerSlices =
    [
        new(SliceVitals, "Vitals"),
        new(SliceSkeleton, "Skeleton"),
        new(SliceOrgans, "Organs"),
        new(SliceTissue, "Tissue"),
    ];

    private readonly record struct PuzzleSignal(string Id, string LayerId, string Text, string Detail, int Priority);

    private float _uiAccumulator;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<CMUBodyScannerConsoleComponent>(CMUBodyScannerUIKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
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
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var boostQuery = EntityQueryEnumerator<CMUBodyScannerSurgerySpeedComponent>();
        while (boostQuery.MoveNext(out var uid, out var boost))
        {
            if (now < boost.ExpiresAt)
                continue;

            RemCompDeferred<CMUBodyScannerSurgerySpeedComponent>(uid);
        }

        var lockoutQuery = EntityQueryEnumerator<CMUBodyScannerCalibrationLockoutComponent>();
        while (lockoutQuery.MoveNext(out var uid, out var lockout))
        {
            if (now < lockout.ExpiresAt)
                continue;

            RemCompDeferred<CMUBodyScannerCalibrationLockoutComponent>(uid);
        }

        _uiAccumulator += frameTime;
        if (_uiAccumulator < 1f)
            return;

        _uiAccumulator = 0f;
        var consoleQuery = EntityQueryEnumerator<CMUBodyScannerConsoleComponent>();
        while (consoleQuery.MoveNext(out var uid, out var comp))
            RefreshUi(uid, comp, comp.LastViewer);
    }

    public float GetSurgeryDelayMultiplier(EntityUid surgeon, EntityUid patient)
    {
        if (!TryComp<CMUBodyScannerSurgerySpeedComponent>(surgeon, out var boost))
            return 1f;

        if (boost.Patient != patient || _timing.CurTime >= boost.ExpiresAt)
            return 1f;

        return Math.Clamp(boost.DelayMultiplier, 0.1f, 1f);
    }

    private void OnUiOpened(Entity<CMUBodyScannerConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        ent.Comp.LastViewer = args.Actor;
        RefreshUi(ent.Owner, ent.Comp, args.Actor);
    }

    private void OnConfirmPuzzle(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerConfirmPuzzleMessage msg)
    {
        if (!CanUsePuzzle(ent.Owner, ent.Comp, msg.Actor, out var patient))
            return;
        ent.Comp.LastViewer = msg.Actor;
        if (GetCalibrationLockoutExpiry(msg.Actor, patient) is not null)
        {
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        var signals = BuildPuzzleSignals(patient);
        if (signals.Count == 0 || !IsValidLayerId(msg.LayerId))
            return;

        if (!TryComp<CMUBodyScannerPuzzleProgressComponent>(msg.Actor, out var progress) ||
            progress.Patient != patient ||
            progress.StartedAt == TimeSpan.Zero)
        {
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        if (_timing.CurTime >= progress.EndsAt)
        {
            ApplyCalibrationLockout(msg.Actor, patient, ent.Comp);
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        if (!TryGetSignal(signals, msg.SignalId, out var signal))
        {
            if (IsDecoySignal(msg.SignalId))
            {
                ApplyPuzzlePenalty(progress, ent.Comp, CMUBodyScannerFeedbackKind.WrongLayer);
                if (_timing.CurTime >= progress.EndsAt)
                    ApplyCalibrationLockout(msg.Actor, patient, ent.Comp);

                RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            }

            return;
        }

        var correctLayer = signal.LayerId == msg.LayerId;
        var assignments = GetPuzzleAssignments(progress, signals);
        var completedLayers = CountCompletedSignalLayers(signals, assignments);
        var layerCount = CountSignalLayers(signals);
        var targetPhase = GetPulseTargetPhase(ent.Comp, progress.Assignments.Count);
        var windowSize = GetPulseWindowSize(ent.Comp, completedLayers, layerCount);
        var graceSize = GetPulseGraceSize(ent.Comp, completedLayers, layerCount);
        var phaseOk = PhaseInWindow(msg.ClientPhase, targetPhase, windowSize + graceSize) ||
                      PhaseInWindow(GetServerPulsePhase(progress, ent.Comp, completedLayers, layerCount), targetPhase, windowSize + graceSize);

        if (!correctLayer)
        {
            ApplyPuzzlePenalty(progress, ent.Comp, CMUBodyScannerFeedbackKind.WrongLayer);
            if (_timing.CurTime >= progress.EndsAt)
                ApplyCalibrationLockout(msg.Actor, patient, ent.Comp);
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        if (!phaseOk)
        {
            ApplyPuzzlePenalty(progress, ent.Comp, CMUBodyScannerFeedbackKind.WrongTiming);
            if (_timing.CurTime >= progress.EndsAt)
                ApplyCalibrationLockout(msg.Actor, patient, ent.Comp);
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        progress.Assignments.RemoveAll(assignment => assignment.SignalId == signal.Id);
        progress.Assignments.Add(new CMUBodyScannerPuzzleAssignment(msg.LayerId, signal.Id));
        progress.LastFeedbackAt = _timing.CurTime;
        progress.LastFeedbackKind = CMUBodyScannerFeedbackKind.Correct;

        assignments = GetPuzzleAssignments(progress, signals);
        if (PuzzleSolved(signals, assignments))
            CompletePuzzle(msg.Actor, patient, ent.Comp);

        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnResetPuzzle(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerResetPuzzleMessage msg)
    {
        if (!CanUsePuzzle(ent.Owner, ent.Comp, msg.Actor, out var patient))
            return;

        ent.Comp.LastViewer = msg.Actor;
        if (GetCalibrationLockoutExpiry(msg.Actor, patient) is not null)
        {
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        var signals = BuildPuzzleSignals(patient);
        if (signals.Count == 0)
        {
            RefreshUi(ent.Owner, ent.Comp, msg.Actor);
            return;
        }

        if (TryComp<CMUBodyScannerPuzzleProgressComponent>(msg.Actor, out var progress))
        {
            if (progress.Patient != patient)
            {
                RemComp<CMUBodyScannerPuzzleProgressComponent>(msg.Actor);
            }
            else if (_timing.CurTime >= progress.EndsAt)
            {
                ApplyCalibrationLockout(msg.Actor, progress.Patient, ent.Comp);
                RefreshUi(ent.Owner, ent.Comp, msg.Actor);
                return;
            }
            else
            {
                RefreshUi(ent.Owner, ent.Comp, msg.Actor);
                return;
            }
        }

        EnsurePuzzleProgress(msg.Actor, patient, ent.Comp);

        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private void OnEjectPatient(Entity<CMUBodyScannerConsoleComponent> ent, ref CMUBodyScannerEjectPatientMessage msg)
    {
        ent.Comp.LastViewer = msg.Actor;
        if (!_skills.HasSkill(msg.Actor, SurgerySkill, 1))
            return;

        if (!TryFindLinkedScanner(ent.Owner, ent.Comp, out var pod, out var podComp)
            || podComp.BodyContainer.ContainedEntity is not { } patient)
        {
            return;
        }

        EjectPatient(pod, podComp);
        RefreshUi(ent.Owner, ent.Comp, msg.Actor);
    }

    private bool CanUsePuzzle(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid user, out EntityUid patient)
    {
        patient = default;
        if (!_skills.HasSkill(user, SurgerySkill, 1))
            return false;

        if (!TryFindLinkedScanner(console, comp, out _, out var scanner)
            || scanner.BodyContainer.ContainedEntity is not { } contained)
        {
            return false;
        }

        patient = contained;
        return true;
    }

    private void RefreshUi(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid? viewer = null)
    {
        if (!_ui.HasUi(console, CMUBodyScannerUIKey.Key))
            return;

        if (viewer is not { } validViewer || !validViewer.IsValid())
            viewer = comp.LastViewer.IsValid() ? comp.LastViewer : null;

        var state = BuildState(console, comp, viewer);
        _ui.SetUiState(console, CMUBodyScannerUIKey.Key, state);
    }

    private CMUBodyScannerBuiState BuildState(EntityUid console, CMUBodyScannerConsoleComponent comp, EntityUid? viewer)
    {
        var podLinked = TryFindLinkedScanner(console, comp, out var pod, out var scanner);
        EntityUid? patient = podLinked ? scanner.BodyContainer.ContainedEntity : null;
        var canScan = viewer is { } user && patient is { } body && _skills.HasSkill(user, SurgerySkill, 1);
        var boostExpires = GetBoostExpiry(viewer, patient);
        var lockoutExpires = GetCalibrationLockoutExpiry(viewer, patient);
        var signals = canScan && patient is { } puzzlePatient ? BuildPuzzleSignals(puzzlePatient) : [];
        var targets = BuildPuzzleTargets(signals);
        CMUBodyScannerPuzzleProgressComponent? progress = null;
        if (canScan &&
            viewer is { } progressViewer &&
            patient is { } progressPatient &&
            signals.Count > 0 &&
            lockoutExpires is null &&
            TryComp<CMUBodyScannerPuzzleProgressComponent>(progressViewer, out progress))
        {
            if (progress.Patient == progressPatient &&
                progress.StartedAt != TimeSpan.Zero &&
                _timing.CurTime >= progress.EndsAt)
            {
                lockoutExpires = ApplyCalibrationLockout(progressViewer, progressPatient, comp);
                progress = null;
            }
            else if (progress.Patient != progressPatient || progress.StartedAt == TimeSpan.Zero)
            {
                progress = null;
            }
        }

        var assignments = GetPuzzleAssignments(progress, signals);
        var layers = signals.Count > 0 ? ScannerSlices : [];
        var puzzleComplete = signals.Count > 0 && progress is not null && _timing.CurTime < progress.EndsAt && PuzzleSolved(signals, assignments);
        var completedLayers = CountCompletedSignalLayers(signals, assignments);
        var layerCount = CountSignalLayers(signals);
        var locked = progress?.Assignments.Count ?? assignments.Count;

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
            puzzleComplete,
            status,
            boostExpires,
            lockoutExpires,
            progress?.StartedAt,
            progress?.EndsAt,
            progress?.PulseStartedAt,
            GetPulsePeriod(comp, completedLayers, layerCount),
            GetPulseTargetPhase(comp, locked),
            GetPulseWindowSize(comp, completedLayers, layerCount),
            GetPulseGraceSize(comp, completedLayers, layerCount),
            progress?.LastPenaltyAt,
            progress?.LastPenaltySeconds ?? 0f,
            progress?.LastFeedbackAt,
            progress?.LastFeedbackKind ?? CMUBodyScannerFeedbackKind.None,
            canScan && patient is { } scanPatient ? BuildScanLines(scanPatient) : [],
            layers,
            targets,
            assignments);
    }

    private CMUBodyScannerPuzzleProgressComponent EnsurePuzzleProgress(EntityUid user, EntityUid patient, CMUBodyScannerConsoleComponent scanner)
    {
        var progress = EnsureComp<CMUBodyScannerPuzzleProgressComponent>(user);
        if (progress.Patient == patient && progress.StartedAt != TimeSpan.Zero)
            return progress;

        progress.Patient = patient;
        ResetPuzzleProgress(progress, scanner);
        return progress;
    }

    private void ResetPuzzleProgress(CMUBodyScannerPuzzleProgressComponent progress, CMUBodyScannerConsoleComponent scanner)
    {
        progress.Assignments.Clear();
        progress.StartedAt = _timing.CurTime;
        progress.EndsAt = _timing.CurTime + TimeSpan.FromSeconds(scanner.CalibrationDurationSeconds);
        progress.PulseStartedAt = _timing.CurTime;
        progress.LastPenaltyAt = TimeSpan.Zero;
        progress.LastPenaltySeconds = 0f;
        progress.LastFeedbackAt = TimeSpan.Zero;
        progress.LastFeedbackKind = CMUBodyScannerFeedbackKind.None;
    }

    private void CompletePuzzle(EntityUid user, EntityUid patient, CMUBodyScannerConsoleComponent scanner)
    {
        var boost = EnsureComp<CMUBodyScannerSurgerySpeedComponent>(user);
        boost.Patient = patient;
        boost.DelayMultiplier = 0.5f;
        boost.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(scanner.BoostDurationSeconds);
        RemComp<CMUBodyScannerPuzzleProgressComponent>(user);
    }

    private static List<CMUBodyScannerPuzzleAssignment> GetPuzzleAssignments(CMUBodyScannerPuzzleProgressComponent? progress, List<PuzzleSignal> signals)
    {
        if (progress is null)
            return [];

        var assignments = new List<CMUBodyScannerPuzzleAssignment>();
        foreach (var assignment in progress.Assignments)
        {
            if (!TryGetSignal(signals, assignment.SignalId, out var signal) || signal.LayerId != assignment.LayerId)
                continue;

            assignments.Add(assignment);
        }

        return assignments;
    }

    private TimeSpan? GetBoostExpiry(EntityUid? viewer, EntityUid? patient)
    {
        if (viewer is not { } user || patient is not { } body)
            return null;

        if (!TryComp<CMUBodyScannerSurgerySpeedComponent>(user, out var boost))
            return null;

        if (boost.Patient != body || _timing.CurTime >= boost.ExpiresAt)
            return null;

        return boost.ExpiresAt;
    }

    private TimeSpan? GetCalibrationLockoutExpiry(EntityUid? viewer, EntityUid? patient)
    {
        if (viewer is not { } user || patient is not { } body)
            return null;

        if (!TryComp<CMUBodyScannerCalibrationLockoutComponent>(user, out var lockout))
            return null;

        if (lockout.Patient != body || _timing.CurTime >= lockout.ExpiresAt)
            return null;

        return lockout.ExpiresAt;
    }

    private TimeSpan ApplyCalibrationLockout(EntityUid user, EntityUid patient, CMUBodyScannerConsoleComponent scanner)
    {
        var lockout = EnsureComp<CMUBodyScannerCalibrationLockoutComponent>(user);
        lockout.Patient = patient;
        lockout.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(scanner.CalibrationLockoutSeconds);

        if (HasComp<CMUBodyScannerPuzzleProgressComponent>(user))
            RemComp<CMUBodyScannerPuzzleProgressComponent>(user);

        return lockout.ExpiresAt;
    }

    private bool TryFindLinkedScanner(
        EntityUid console,
        CMUBodyScannerConsoleComponent comp,
        out EntityUid scanner,
        out CMUBodyScannerPodComponent scannerComp)
    {
        scanner = default;
        scannerComp = default!;
        var consoleCoords = Transform(console).Coordinates;
        var bestDistance = float.MaxValue;

        foreach (var candidate in _lookup.GetEntitiesInRange<CMUBodyScannerPodComponent>(consoleCoords, comp.LinkRange))
        {
            if (!consoleCoords.TryDistance(EntityManager, Transform(candidate).Coordinates, out var distance))
                continue;

            if (distance >= bestDistance)
                continue;

            scanner = candidate;
            scannerComp = Comp<CMUBodyScannerPodComponent>(candidate);
            bestDistance = distance;
        }

        return scanner.IsValid();
    }

    private void OnPodInit(Entity<CMUBodyScannerPodComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _containers.EnsureContainer<ContainerSlot>(ent.Owner, CMUBodyScannerPodComponent.BodyContainerId);
        UpdatePodAppearance(ent.Owner, ent.Comp);
    }

    private void OnPodDestroyed(Entity<CMUBodyScannerPodComponent> ent, ref DestructionEventArgs args)
    {
        EjectPatient(ent.Owner, ent.Comp);
    }

    private void OnPodDragDrop(Entity<CMUBodyScannerPodComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled || !CanInsertPatient(ent.Comp, args.Dragged))
            return;

        StartInsertDoAfter(ent.Owner, ent.Comp, args.User, args.Dragged);
        args.Handled = true;
    }

    private void OnPodAlternativeVerbs(Entity<CMUBodyScannerPodComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;

        if (ent.Comp.BodyContainer.ContainedEntity is { } contained)
        {
            var patient = contained;
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => EjectPatient(ent.Owner, ent.Comp),
                Category = VerbCategory.Eject,
                Text = Loc.GetString("medical-scanner-verb-noun-occupant"),
                Priority = 1,
            });
            return;
        }

        if (!CanInsertPatient(ent.Comp, user))
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
        if (ent.Comp.BodyContainer.ContainedEntity != args.Entity)
            return;

        EjectPatient(ent.Owner, ent.Comp);
    }

    private void StartInsertDoAfter(EntityUid pod, CMUBodyScannerPodComponent comp, EntityUid user, EntityUid target)
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

    private void OnPodInsertDoAfter(Entity<CMUBodyScannerPodComponent> ent, ref CMUMedicalPodInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        InsertPatient(ent.Owner, ent.Comp, target);
        args.Handled = true;
    }

    private bool CanInsertPatient(CMUBodyScannerPodComponent comp, EntityUid patient)
    {
        return comp.BodyContainer.ContainedEntity is null && HasComp<BodyComponent>(patient);
    }

    private bool InsertPatient(EntityUid pod, CMUBodyScannerPodComponent comp, EntityUid patient)
    {
        if (!CanInsertPatient(comp, patient))
            return false;

        if (!_containers.Insert(patient, comp.BodyContainer))
            return false;

        UpdatePodAppearance(pod, comp);
        RefreshLinkedConsoles(pod);
        return true;
    }

    private EntityUid? EjectPatient(EntityUid pod, CMUBodyScannerPodComponent comp)
    {
        if (comp.BodyContainer.ContainedEntity is not { } patient)
            return null;

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

    private void UpdatePodAppearance(EntityUid pod, CMUBodyScannerPodComponent comp)
    {
        _appearance.SetData(pod, CMUMedicalPodVisuals.Occupied, comp.BodyContainer.ContainedEntity is not null);
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

            RefreshUi(console, consoleComp, consoleComp.LastViewer);
        }
    }

    private List<CMUBodyScannerScanLine> BuildScanLines(EntityUid patient)
    {
        var lines = new List<CMUBodyScannerScanLine>();
        if (TryComp<MobStateComponent>(patient, out var mob))
            lines.Add(VitalsLine(Loc.GetString("cmu-body-scanner-line-state", ("state", mob.CurrentState))));

        if (TryComp<DamageableComponent>(patient, out var damageable))
        {
            lines.Add(VitalsLine(Loc.GetString(
                "cmu-body-scanner-line-damage",
                ("total", damageable.TotalDamage),
                ("brute", damageable.DamagePerGroup.GetValueOrDefault("Brute")),
                ("burn", damageable.DamagePerGroup.GetValueOrDefault("Burn")))));
        }

        if (_bloodstream.TryGetBloodSolution(patient, out var blood))
            lines.Add(VitalsLine(Loc.GetString("cmu-body-scanner-line-blood", ("blood", blood.Volume), ("max", blood.MaxVolume))));

        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (TryComp<HeartComponent>(organ.Id, out var heart))
            {
                var state = heart.Stopped
                    ? Loc.GetString("cmu-body-scanner-heart-stopped")
                    : Loc.GetString("cmu-body-scanner-heart-active", ("bpm", heart.BeatsPerMinute));
                lines.Add(VitalsLine(state));
                break;
            }
        }

        AddPartLines(patient, lines);
        AddOrganLines(patient, lines);

        if (lines.Count == 0)
            lines.Add(VitalsLine(Loc.GetString("cmu-body-scanner-line-no-data")));

        return lines;
    }

    private void AddPartLines(EntityUid patient, List<CMUBodyScannerScanLine> lines)
    {
        foreach (var (part, partComp) in _body.GetBodyChildren(patient))
        {
            var details = new List<string>();
            if (TryComp<BodyPartHealthComponent>(part, out var health))
                details.Add(Loc.GetString("cmu-body-scanner-part-health", ("current", health.Current), ("max", health.Max)));

            if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            {
                var untreated = 0;
                foreach (var wound in wounds.Wounds)
                {
                    if (!wound.Treated)
                        untreated++;
                }

                if (untreated > 0)
                    details.Add(Loc.GetString("cmu-body-scanner-part-wounds", ("count", untreated)));
            }

            if (TryComp<FractureComponent>(part, out var fracture) && fracture.Severity != FractureSeverity.None)
                details.Add(Loc.GetString("cmu-body-scanner-part-fracture", ("severity", fracture.Severity)));

            if (TryComp<InternalBleedingComponent>(part, out var bleed))
                details.Add(Loc.GetString("cmu-body-scanner-part-bleed", ("rate", bleed.BloodlossPerSecond)));

            if (HasComp<CMUEscharComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-eschar"));
            if (HasComp<CMUSplintedComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-splinted"));
            if (HasComp<CMUCastComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-cast"));
            if (HasComp<CMUTourniquetComponent>(part))
                details.Add(Loc.GetString("cmu-body-scanner-part-tourniquet"));

            if (details.Count == 0)
                continue;

            lines.Add(BodyLine(Loc.GetString(
                "cmu-body-scanner-line-part",
                ("part", SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry)),
                ("details", string.Join(", ", details)))));
        }

        foreach (var (type, symmetry) in GetMissingLimbSlots(patient))
        {
            lines.Add(BodyLine(Loc.GetString(
                "cmu-body-scanner-line-part",
                ("part", SharedCMUSurgeryFlowSystem.FormatPartName(type, symmetry)),
                ("details", Loc.GetString("cmu-body-scanner-part-missing-limb")))));
        }
    }

    private void AddOrganLines(EntityUid patient, List<CMUBodyScannerScanLine> lines)
    {
        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (!TryComp<OrganHealthComponent>(organ.Id, out var health))
                continue;

            lines.Add(OrganLine(Loc.GetString(
                "cmu-body-scanner-line-organ",
                ("organ", OrganName(organ.Id)),
                ("stage", FormatOrganStage(health.Stage)),
                ("current", health.Current),
                ("max", health.Max))));
        }

        foreach (var (part, partComp) in _body.GetBodyChildren(patient))
        {
            foreach (var (slotId, _) in partComp.Organs)
            {
                var containerId = SharedBodySystem.OrganSlotContainerIdPrefix + slotId;
                if (!_containers.TryGetContainer(part, containerId, out var container))
                    continue;
                if (container.ContainedEntities.Count > 0)
                    continue;

                lines.Add(OrganLine(Loc.GetString(
                    "cmu-body-scanner-line-missing-organ",
                    ("organ", OrganSlotName(slotId)),
                ("part", SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry)))));
            }
        }
    }

    private List<(BodyPartType Type, BodyPartSymmetry Symmetry)> GetMissingLimbSlots(EntityUid patient)
    {
        var missing = new List<(BodyPartType Type, BodyPartSymmetry Symmetry)>();
        if (!TryComp<BodyComponent>(patient, out var bodyComp))
            return missing;
        if (_body.GetRootPartOrNull(patient, bodyComp) is not { } root)
            return missing;

        foreach (var (slotId, slot) in root.BodyPart.Children)
        {
            if (slot.Type is not (BodyPartType.Arm or BodyPartType.Leg))
                continue;

            var symmetry = slotId.Contains("left", StringComparison.Ordinal)
                ? BodyPartSymmetry.Left
                : slotId.Contains("right", StringComparison.Ordinal)
                    ? BodyPartSymmetry.Right
                    : BodyPartSymmetry.None;
            if (symmetry == BodyPartSymmetry.None)
                continue;

            var containerId = SharedBodySystem.GetPartSlotContainerId(slotId);
            if (!_containers.TryGetContainer(root.Entity, containerId, out var container) ||
                container.ContainedEntities.Count == 0)
            {
                missing.Add((slot.Type, symmetry));
            }
        }

        return missing;
    }

    private static CMUBodyScannerScanLine VitalsLine(string text)
    {
        return new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Vitals, text);
    }

    private static CMUBodyScannerScanLine BodyLine(string text)
    {
        return new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Body, text);
    }

    private static CMUBodyScannerScanLine OrganLine(string text)
    {
        return new CMUBodyScannerScanLine(CMUBodyScannerScanCategory.Organs, text);
    }

    private string OrganName(EntityUid organ)
    {
        var meta = MetaData(organ);
        if (meta.EntityPrototype?.ID is { } protoId && OrganDisplayName(protoId) is { } protoName)
            return protoName;

        var name = Name(organ);
        return string.IsNullOrWhiteSpace(name)
            ? CapitalizeFirst(meta.EntityPrototype?.ID ?? organ.ToString())
            : CapitalizeFirst(name);
    }

    private string OrganSlotName(string slotId)
    {
        return OrganDisplayName(slotId) ?? CapitalizeFirst(slotId);
    }

    private static string FormatOrganStage(OrganDamageStage stage)
    {
        return CapitalizeFirst(stage.ToString());
    }

    private string? OrganDisplayName(string idOrSlot)
    {
        return idOrSlot switch
        {
            "CMUOrganHumanHeart" or "heart" => Loc.GetString("cmu-medical-scanner-organ-heart"),
            "CMUOrganHumanLungs" or "lungs" => Loc.GetString("cmu-medical-scanner-organ-lungs"),
            "CMUOrganHumanLiver" or "liver" => Loc.GetString("cmu-medical-scanner-organ-liver"),
            "CMUOrganHumanBrain" or "brain" => Loc.GetString("cmu-medical-scanner-organ-brain"),
            "CMUOrganHumanKidneys" or "kidneys" => Loc.GetString("cmu-medical-scanner-organ-kidneys"),
            "CMUOrganHumanStomach" or "stomach" => Loc.GetString("cmu-medical-scanner-organ-stomach"),
            "CMUOrganHumanEyes" or "eyes" => Loc.GetString("cmu-medical-scanner-organ-eyes"),
            "CMUOrganHumanEars" or "ears" => Loc.GetString("cmu-medical-scanner-organ-ears"),
            _ => null,
        };
    }

    private static string CapitalizeFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private List<PuzzleSignal> BuildPuzzleSignals(EntityUid patient)
    {
        var signals = new List<PuzzleSignal>();

        foreach (var organ in _body.GetBodyOrgans(patient))
        {
            if (TryComp<HeartComponent>(organ.Id, out var heart) && heart.Stopped)
            {
                AddPuzzleSignal(
                    signals,
                    $"cardiac:{organ.Id}",
                    Loc.GetString("cmu-body-scanner-signal-heart-stopped"),
                    Loc.GetString("cmu-body-scanner-slice-detail-cardiac"),
                    SliceVitals,
                    0);
            }

            if (TryComp<OrganHealthComponent>(organ.Id, out var organHealth) && organHealth.Stage != OrganDamageStage.Healthy)
            {
                AddPuzzleSignal(
                    signals,
                    $"organ:{organ.Id}",
                    Loc.GetString("cmu-body-scanner-signal-organ-damage", ("organ", OrganName(organ.Id)), ("stage", FormatOrganStage(organHealth.Stage))),
                    Loc.GetString("cmu-body-scanner-slice-detail-organ"),
                    SliceOrgans,
                    organHealth.Stage.IsAtLeast(OrganDamageStage.Failing) ? 1 : 4);
            }
        }

        if (_bloodstream.TryGetBloodSolution(patient, out var blood))
        {
            if (blood.MaxVolume > FixedPoint2.Zero && blood.Volume < blood.MaxVolume * (FixedPoint2)0.75f)
            {
                AddPuzzleSignal(
                    signals,
                    "blood:low",
                    Loc.GetString("cmu-body-scanner-signal-low-blood", ("blood", blood.Volume), ("max", blood.MaxVolume)),
                    Loc.GetString("cmu-body-scanner-slice-detail-blood"),
                    SliceVitals,
                    2);
            }
        }

        foreach (var (part, partComp) in _body.GetBodyChildren(patient))
        {
            var partName = SharedCMUSurgeryFlowSystem.FormatPartName(partComp.PartType, partComp.Symmetry);

            if (TryComp<InternalBleedingComponent>(part, out var bleed))
            {
                AddPuzzleSignal(
                    signals,
                    $"bleed:{part}",
                    Loc.GetString("cmu-body-scanner-signal-internal-bleed", ("part", partName), ("rate", bleed.BloodlossPerSecond)),
                    Loc.GetString("cmu-body-scanner-slice-detail-bleed"),
                    SliceTissue,
                    1);
            }

            if (TryComp<FractureComponent>(part, out var fracture) && fracture.Severity != FractureSeverity.None)
            {
                AddPuzzleSignal(
                    signals,
                    $"fracture:{part}",
                    Loc.GetString("cmu-body-scanner-signal-fracture", ("part", partName), ("severity", fracture.Severity)),
                    Loc.GetString("cmu-body-scanner-slice-detail-fracture"),
                    SliceSkeleton,
                    3);
            }

            if (TryComp<BodyPartWoundComponent>(part, out var wounds))
            {
                var untreated = 0;
                foreach (var wound in wounds.Wounds)
                {
                    if (!wound.Treated)
                        untreated++;
                }

                if (untreated > 0)
                {
                    AddPuzzleSignal(
                        signals,
                        $"wound:{part}",
                        Loc.GetString("cmu-body-scanner-signal-wounds", ("part", partName), ("count", untreated)),
                        Loc.GetString("cmu-body-scanner-slice-detail-wound"),
                        SliceTissue,
                        5);
                }
            }

            if (TryComp<BodyPartHealthComponent>(part, out var health) &&
                health.Max > FixedPoint2.Zero &&
                health.Current < health.Max * (FixedPoint2)0.75f)
            {
                AddPuzzleSignal(
                    signals,
                    $"trauma:{part}",
                    Loc.GetString("cmu-body-scanner-signal-trauma", ("part", partName), ("current", health.Current), ("max", health.Max)),
                    Loc.GetString("cmu-body-scanner-slice-detail-trauma"),
                    SliceTissue,
                    6);
            }

            foreach (var (slotId, _) in partComp.Organs)
            {
                var containerId = SharedBodySystem.OrganSlotContainerIdPrefix + slotId;
                if (!_containers.TryGetContainer(part, containerId, out var container))
                    continue;
                if (container.ContainedEntities.Count > 0)
                    continue;

                AddPuzzleSignal(
                    signals,
                    $"missing:{part}:{slotId}",
                    Loc.GetString("cmu-body-scanner-signal-missing-organ", ("organ", OrganSlotName(slotId)), ("part", partName)),
                    Loc.GetString("cmu-body-scanner-slice-detail-missing-organ"),
                    SliceOrgans,
                    0);
            }
        }

        foreach (var (type, symmetry) in GetMissingLimbSlots(patient))
        {
            var partName = SharedCMUSurgeryFlowSystem.FormatPartName(type, symmetry);
            AddPuzzleSignal(
                signals,
                $"missing-limb:{type}:{symmetry}",
                Loc.GetString("cmu-body-scanner-signal-missing-limb", ("part", partName)),
                Loc.GetString("cmu-body-scanner-slice-detail-missing-limb"),
                SliceSkeleton,
                0);
        }

        signals.Sort((a, b) =>
        {
            var priority = a.Priority.CompareTo(b.Priority);
            return priority != 0 ? priority : string.Compare(a.Text, b.Text, StringComparison.Ordinal);
        });

        if (signals.Count > MaxPuzzleSignals)
            signals.RemoveRange(MaxPuzzleSignals, signals.Count - MaxPuzzleSignals);

        return signals;
    }

    private static void AddPuzzleSignal(List<PuzzleSignal> signals, string id, string text, string detail, string layerId, int priority)
    {
        foreach (var signal in signals)
        {
            if (signal.Id == id)
                return;
        }

        signals.Add(new PuzzleSignal(id, layerId, text, detail, priority));
    }

    private List<CMUBodyScannerSliceSignal> BuildPuzzleTargets(List<PuzzleSignal> signals)
    {
        var targets = new List<CMUBodyScannerSliceSignal>();
        foreach (var signal in signals)
            targets.Add(new CMUBodyScannerSliceSignal(signal.Id, signal.LayerId, signal.Text, signal.Detail));

        foreach (var layer in ScannerSlices)
        {
            if (!HasSignalLayer(signals, layer.Id))
                continue;

            var decoys = GetDecoySignals(layer.Id);
            var decoyCount = CountSignalsForLayer(signals, layer.Id) >= 3 ? 2 : 1;
            for (var i = 0; i < decoys.Length && i < decoyCount; i++)
            {
                var decoy = decoys[i];
                targets.Add(new CMUBodyScannerSliceSignal(
                    $"{DecoySignalPrefix}{layer.Id}:{i}",
                    layer.Id,
                    decoy.Text,
                    decoy.Detail,
                    true));
            }
        }

        return targets;
    }

    private static bool HasSignalLayer(List<PuzzleSignal> signals, string layerId)
    {
        foreach (var signal in signals)
        {
            if (signal.LayerId == layerId)
                return true;
        }

        return false;
    }

    private static int CountSignalsForLayer(List<PuzzleSignal> signals, string layerId)
    {
        var count = 0;
        foreach (var signal in signals)
        {
            if (signal.LayerId == layerId)
                count++;
        }

        return count;
    }

    private static bool IsDecoySignal(string id)
    {
        return id.StartsWith(DecoySignalPrefix, StringComparison.Ordinal);
    }

    private (string Text, string Detail)[] GetDecoySignals(string layerId)
    {
        return layerId switch
        {
            SliceVitals =>
            [
                (Loc.GetString("cmu-body-scanner-decoy-vitals-1"), Loc.GetString("cmu-body-scanner-decoy-detail-vitals")),
                (Loc.GetString("cmu-body-scanner-decoy-vitals-2"), Loc.GetString("cmu-body-scanner-decoy-detail-vitals")),
            ],
            SliceSkeleton =>
            [
                (Loc.GetString("cmu-body-scanner-decoy-skeleton-1"), Loc.GetString("cmu-body-scanner-decoy-detail-skeleton")),
                (Loc.GetString("cmu-body-scanner-decoy-skeleton-2"), Loc.GetString("cmu-body-scanner-decoy-detail-skeleton")),
            ],
            SliceOrgans =>
            [
                (Loc.GetString("cmu-body-scanner-decoy-organs-1"), Loc.GetString("cmu-body-scanner-decoy-detail-organs")),
                (Loc.GetString("cmu-body-scanner-decoy-organs-2"), Loc.GetString("cmu-body-scanner-decoy-detail-organs")),
            ],
            SliceTissue =>
            [
                (Loc.GetString("cmu-body-scanner-decoy-tissue-1"), Loc.GetString("cmu-body-scanner-decoy-detail-tissue")),
                (Loc.GetString("cmu-body-scanner-decoy-tissue-2"), Loc.GetString("cmu-body-scanner-decoy-detail-tissue")),
            ],
            _ => [],
        };
    }

    private static bool IsValidLayerId(string id)
    {
        foreach (var layer in ScannerSlices)
        {
            if (layer.Id == id)
                return true;
        }

        return false;
    }

    private static bool TryGetSignal(List<PuzzleSignal> signals, string id, out PuzzleSignal signal)
    {
        foreach (var candidate in signals)
        {
            if (candidate.Id != id)
                continue;

            signal = candidate;
            return true;
        }

        signal = default;
        return false;
    }

    private void ApplyPuzzlePenalty(
        CMUBodyScannerPuzzleProgressComponent progress,
        CMUBodyScannerConsoleComponent scanner,
        CMUBodyScannerFeedbackKind feedback)
    {
        var penalty = MathF.Max(1f, scanner.WrongMovePenaltySeconds);
        progress.EndsAt -= TimeSpan.FromSeconds(penalty);
        if (progress.EndsAt < _timing.CurTime)
            progress.EndsAt = _timing.CurTime;

        progress.LastPenaltyAt = _timing.CurTime;
        progress.LastPenaltySeconds = penalty;
        progress.LastFeedbackAt = _timing.CurTime;
        progress.LastFeedbackKind = feedback;
    }

    private float GetServerPulsePhase(
        CMUBodyScannerPuzzleProgressComponent progress,
        CMUBodyScannerConsoleComponent scanner,
        int completedLayers,
        int layerCount)
    {
        var period = MathF.Max(0.1f, GetPulsePeriod(scanner, completedLayers, layerCount));
        var elapsed = (_timing.CurTime - progress.PulseStartedAt).TotalSeconds;
        var phase = (float)(elapsed / period);
        phase -= MathF.Floor(phase);
        return phase;
    }

    private static float GetPulsePeriod(CMUBodyScannerConsoleComponent scanner, int completedLayers, int layerCount)
    {
        var ratio = GetLayerDifficultyRatio(completedLayers, layerCount);
        return Lerp(
            MathF.Max(0.1f, scanner.PulsePeriodSeconds),
            MathF.Max(0.1f, scanner.MinPulsePeriodSeconds),
            ratio);
    }

    private static float GetPulseTargetPhase(CMUBodyScannerConsoleComponent scanner, int lockedSignals)
    {
        var phase = scanner.PulseTargetPhase + MathF.Max(0, lockedSignals) * scanner.PulseTargetShiftPerLock;
        phase -= MathF.Floor(phase);
        return phase;
    }

    private static float GetPulseWindowSize(CMUBodyScannerConsoleComponent scanner, int completedLayers, int layerCount)
    {
        var ratio = GetLayerDifficultyRatio(completedLayers, layerCount);
        return Lerp(
            Math.Clamp(scanner.PulseWindowSize, 0.04f, 1f),
            Math.Clamp(scanner.MinPulseWindowSize, 0.04f, 1f),
            ratio);
    }

    private static float GetPulseGraceSize(CMUBodyScannerConsoleComponent scanner, int completedLayers, int layerCount)
    {
        var ratio = GetLayerDifficultyRatio(completedLayers, layerCount);
        return Lerp(
            Math.Clamp(scanner.PulseGraceSize, 0.02f, 1f),
            Math.Clamp(scanner.PulseGraceSize * 0.65f, 0.02f, 1f),
            ratio);
    }

    private static float GetLayerDifficultyRatio(int completedLayers, int layerCount)
    {
        if (layerCount <= 1)
            return 0f;

        return Math.Clamp((float) completedLayers / (layerCount - 1), 0f, 1f);
    }

    private static float Lerp(float from, float to, float ratio)
    {
        return from + (to - from) * Math.Clamp(ratio, 0f, 1f);
    }

    private static int CountSignalLayers(List<PuzzleSignal> signals)
    {
        var count = 0;
        foreach (var layer in ScannerSlices)
        {
            foreach (var signal in signals)
            {
                if (signal.LayerId != layer.Id)
                    continue;

                count++;
                break;
            }
        }

        return count;
    }

    private static int CountCompletedSignalLayers(
        List<PuzzleSignal> signals,
        List<CMUBodyScannerPuzzleAssignment> assignments)
    {
        var completed = 0;
        foreach (var layer in ScannerSlices)
        {
            var hasSignal = false;
            var allLocked = true;
            foreach (var signal in signals)
            {
                if (signal.LayerId != layer.Id)
                    continue;

                hasSignal = true;
                if (HasSignalAssignment(assignments, signal.Id, signal.LayerId))
                    continue;

                allLocked = false;
                break;
            }

            if (hasSignal && allLocked)
                completed++;
        }

        return completed;
    }

    private static bool HasSignalAssignment(
        List<CMUBodyScannerPuzzleAssignment> assignments,
        string signalId,
        string layerId)
    {
        foreach (var assignment in assignments)
        {
            if (assignment.SignalId == signalId && assignment.LayerId == layerId)
                return true;
        }

        return false;
    }

    private static bool PhaseInWindow(float phase, float center, float size)
    {
        phase = NormalizePhase(phase);
        center = NormalizePhase(center);
        var distance = MathF.Abs(phase - center);
        if (distance > 0.5f)
            distance = 1f - distance;

        return distance <= Math.Clamp(size, 0f, 1f) / 2f;
    }

    private static float NormalizePhase(float phase)
    {
        if (float.IsNaN(phase) || float.IsInfinity(phase))
            return 0f;

        phase -= MathF.Floor(phase);
        return phase;
    }

    private static bool PuzzleSolved(List<PuzzleSignal> signals, List<CMUBodyScannerPuzzleAssignment> assignments)
    {
        if (signals.Count == 0 || assignments.Count < signals.Count)
            return false;

        foreach (var signal in signals)
        {
            var matched = false;
            foreach (var assignment in assignments)
            {
                if (assignment.SignalId == signal.Id && assignment.LayerId == signal.LayerId)
                {
                    matched = true;
                    break;
                }
            }

            if (!matched)
                return false;
        }

        return true;
    }
}
