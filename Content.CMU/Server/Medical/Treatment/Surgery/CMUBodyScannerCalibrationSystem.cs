using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.CMU14.Medical.Core;
using Content.Shared.CMU14.Medical.Anatomy.BodyParts;
using Content.Shared.CMU14.Medical.Anatomy.Bones;
using Content.Shared.CMU14.Medical.Treatment.FirstAid;
using Content.Shared.CMU14.Medical.Anatomy.Organs;
using Content.Shared.CMU14.Medical.Anatomy.Organs.Heart;
using Content.Shared.CMU14.Medical.Treatment.Surgery;
using Content.Shared.CMU14.Medical.Injuries.Wounds;
using Content.Shared._RMC14.Body;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Medical.Treatment.Surgery;

public readonly record struct CMUBodyScannerCalibrationView(
    bool PuzzleComplete,
    TimeSpan? BoostExpiresAt,
    TimeSpan? LockoutExpiresAt,
    TimeSpan? StartedAt,
    TimeSpan? EndsAt,
    TimeSpan? PulseStartedAt,
    float PulsePeriod,
    float PulseTargetPhase,
    float PulseWindowSize,
    float PulseGraceSize,
    TimeSpan? LastPenaltyAt,
    float LastPenaltySeconds,
    TimeSpan? LastFeedbackAt,
    CMUBodyScannerFeedbackKind LastFeedbackKind,
    List<CMUBodyScannerPuzzleChoice> Layers,
    List<CMUBodyScannerSliceSignal> Targets,
    List<CMUBodyScannerPuzzleAssignment> Assignments,
    ulong AttemptId);

public readonly record struct CMUBodyScannerPuzzleSignal(string Id, string LayerId, string Text, string Detail, int Priority);

public sealed partial class CMUBodyScannerCalibrationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private CMUMedicalBodyIndexSystem _medicalIndex = default!;
    [Dependency] private CMUMedicalChangeSystem _medicalChanges = default!;
    [Dependency] private SharedRMCBloodstreamSystem _bloodstream = default!;
    [Dependency] private CMUBodyScannerReadoutSystem _readout = default!;
    [Dependency] private CMUMedicalSchedulerSystem _scheduler = default!;
    [Dependency] private CMUWoundLedgerSystem _woundLedger = default!;

    private const int MaxPuzzleSignals = 8;
    private ulong _nextAttemptId;
    private const string SliceVitals = "vitals";
    private const string SliceSkeleton = "skeleton";
    private const string SliceOrgans = "organs";
    private const string SliceTissue = "tissue";
    private const string DecoySignalPrefix = "noise:";
    private static readonly CMUMedicalWorkKey BoostExpiryWork = new("body-scanner-boost-expiry");
    private static readonly CMUMedicalWorkKey LockoutExpiryWork = new("body-scanner-lockout-expiry");
    private static readonly CMUMedicalWorkKey AttemptExpiryWork = new("body-scanner-attempt-expiry");
    private readonly List<EntityUid> _expiredPatients = new();

    private static readonly List<CMUBodyScannerPuzzleChoice> ScannerSlices =
    [
        new(SliceVitals, "Vitals"),
        new(SliceSkeleton, "Skeleton"),
        new(SliceOrgans, "Organs"),
        new(SliceTissue, "Tissue"),
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CMUBodyScannerSurgerySpeedComponent, CMUMedicalWorkDueEvent>(OnBoostExpiryDue);
        SubscribeLocalEvent<CMUBodyScannerCalibrationLockoutComponent, CMUMedicalWorkDueEvent>(OnLockoutExpiryDue);
        SubscribeLocalEvent<CMUBodyScannerCalibrationLockoutComponent, EntityUnpausedEvent>(OnLockoutsUnpaused);
        SubscribeLocalEvent<CMUBodyScannerPuzzleProgressComponent, CMUMedicalWorkDueEvent>(OnAttemptExpiryDue);
    }

    private void OnBoostExpiryDue(
        Entity<CMUBodyScannerSurgerySpeedComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != BoostExpiryWork)
            return;

        if (ent.Comp.ExpiresAt > _timing.CurTime)
        {
            _scheduler.Schedule(ent.Owner, BoostExpiryWork, ent.Comp.ExpiresAt);
            return;
        }

        RemComp<CMUBodyScannerSurgerySpeedComponent>(ent);
    }

    private void OnLockoutExpiryDue(
        Entity<CMUBodyScannerCalibrationLockoutComponent> ent,
        ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != LockoutExpiryWork)
            return;

        ent.Comp.NextExpiry = TimeSpan.Zero;
        PruneLockouts(ent.Owner, ent.Comp);
    }

    private void OnLockoutsUnpaused(Entity<CMUBodyScannerCalibrationLockoutComponent> ent, ref EntityUnpausedEvent args)
    {
        foreach (var patient in ent.Comp.Expiries.Keys.ToArray())
            ent.Comp.Expiries[patient] += args.PausedTime;
        ent.Comp.NextExpiry += args.PausedTime;
    }

    private void OnAttemptExpiryDue(Entity<CMUBodyScannerPuzzleProgressComponent> ent, ref CMUMedicalWorkDueEvent args)
    {
        if (args.Key != AttemptExpiryWork)
            return;
        if (!ExpireAttempt(ent.Owner))
            _scheduler.Schedule(ent.Owner, AttemptExpiryWork, ent.Comp.EndsAt);
    }

    public ulong GetRevision(EntityUid user) => CompOrNull<CMUBodyScannerOperatorComponent>(user)?.Revision ?? 0;

    public ulong GetAttempt(EntityUid user) => CompOrNull<CMUBodyScannerPuzzleProgressComponent>(user)?.AttemptId ?? 0;

    private void AdvanceRevision(EntityUid user) => EnsureComp<CMUBodyScannerOperatorComponent>(user).Revision++;

    public bool HasOtherAttempt(EntityUid user, EntityUid patient, CMUBodyScannerOrigin origin)
    {
        return TryComp<CMUBodyScannerPuzzleProgressComponent>(user, out var progress)
            && (progress.Patient != patient || progress.Origin != origin);
    }

    public bool CanStart(EntityUid user, EntityUid patient)
    {
        ExpireAttempt(user);
        if (HasComp<CMUBodyScannerPuzzleProgressComponent>(user) || GetCalibrationLockoutExpiry(user, patient) != null)
            return false;
        return !TryComp<CMUBodyScannerCalibrationLockoutComponent>(user, out var lockouts)
            || lockouts.Expiries.Count < CMUBodyScannerCalibrationLockoutComponent.MaximumPatients;
    }

    public float GetSurgeryDelayMultiplier(EntityUid surgeon, EntityUid patient)
    {
        if (!TryComp<CMUBodyScannerSurgerySpeedComponent>(surgeon, out var boost))
            return 1f;

        if (boost.Patient != patient || _timing.CurTime >= boost.ExpiresAt)
            return 1f;

        return Math.Clamp(boost.DelayMultiplier, 0.1f, 1f);
    }

    public bool TryConfirmPuzzle(
        EntityUid user,
        EntityUid patient,
        CMUBodyScannerConsoleComponent scanner,
        string layerId,
        string signalId,
        float clientPhase,
        ulong? expectedAttempt = null,
        int? expectedAssignments = null,
        CMUBodyScannerOrigin? origin = null)
    {
        if (Paused(user) || TerminatingOrDeleted(patient) || !float.IsFinite(clientPhase))
            return false;
        ExpireAttempt(user);
        if (GetCalibrationLockoutExpiry(user, patient) is not null)
            return true;

        var signals = GetPuzzleProjection(patient).Signals;
        if (signals.Count == 0 || !IsValidLayerId(layerId))
            return false;

        if (!TryComp<CMUBodyScannerPuzzleProgressComponent>(user, out var progress) ||
            progress.Patient != patient ||
            progress.Origin != origin)
        {
            return true;
        }

        if (expectedAttempt is { } attempt && progress.AttemptId != attempt
            || expectedAssignments is { } count && progress.Assignments.Count != count
            || progress.Assignments.Exists(assignment => assignment.SignalId == signalId))
        {
            return false;
        }

        if (_timing.CurTime >= progress.EndsAt)
        {
            ApplyCalibrationLockout(user, progress, progress.EndsAt);
            return true;
        }

        if (!TryGetSignal(signals, signalId, out var signal))
        {
            if (!IsDecoySignal(signalId))
                return false;

            ApplyPuzzlePenalty(user, progress, scanner, CMUBodyScannerFeedbackKind.WrongLayer);
            if (_timing.CurTime >= progress.EndsAt)
                ApplyCalibrationLockout(user, progress, _timing.CurTime);

            return true;
        }

        var correctLayer = signal.LayerId == layerId;
        var assignments = GetPuzzleAssignments(progress, signals);
        var completedLayers = CountCompletedSignalLayers(signals, assignments);
        var layerCount = CountSignalLayers(signals);
        var targetPhase = GetPulseTargetPhase(scanner, progress.Assignments.Count);
        var windowSize = GetPulseWindowSize(scanner, completedLayers, layerCount);
        var graceSize = GetPulseGraceSize(scanner, completedLayers, layerCount);
        // The configured grace window is the bounded latency allowance. Client animation
        // phase is diagnostic input only; it cannot prove a successful timed action.
        var phaseOk = PhaseInWindow(GetServerPulsePhase(progress, scanner, completedLayers, layerCount),
            targetPhase, windowSize + graceSize);

        if (!correctLayer)
        {
            ApplyPuzzlePenalty(user, progress, scanner, CMUBodyScannerFeedbackKind.WrongLayer);
            if (_timing.CurTime >= progress.EndsAt)
                ApplyCalibrationLockout(user, progress, _timing.CurTime);

            return true;
        }

        if (!phaseOk)
        {
            ApplyPuzzlePenalty(user, progress, scanner, CMUBodyScannerFeedbackKind.WrongTiming);
            if (_timing.CurTime >= progress.EndsAt)
                ApplyCalibrationLockout(user, progress, _timing.CurTime);

            return true;
        }

        progress.Assignments.RemoveAll(assignment => assignment.SignalId == signal.Id);
        progress.Assignments.Add(new CMUBodyScannerPuzzleAssignment(layerId, signal.Id));
        progress.LastFeedbackAt = _timing.CurTime;
        progress.LastFeedbackKind = CMUBodyScannerFeedbackKind.Correct;
        AdvanceRevision(user);

        assignments = GetPuzzleAssignments(progress, signals);
        if (PuzzleSolved(signals, assignments))
            CompletePuzzle(user, patient, scanner);

        return true;
    }

    public bool ResetPuzzle(EntityUid user, EntityUid patient, CMUBodyScannerConsoleComponent scanner,
        CMUBodyScannerOrigin? origin = null)
    {
        if (Paused(user) || TerminatingOrDeleted(patient))
            return false;
        ExpireAttempt(user);
        if (GetCalibrationLockoutExpiry(user, patient) is not null)
            return false;

        var signals = GetPuzzleProjection(patient).Signals;
        if (signals.Count == 0)
            return true;

        // Starting another scan must never reset an unfinished patient's countdown.
        if (!CanStart(user, patient))
            return false;
        var progress = EnsurePuzzleProgress(user, patient, scanner);
        progress.Origin = origin;
        progress.LockoutDurationSeconds = MathF.Max(0, scanner.CalibrationLockoutSeconds);
        _scheduler.Schedule(user, AttemptExpiryWork, progress.EndsAt);
        AdvanceRevision(user);
        return true;
    }

    public CMUBodyScannerCalibrationView BuildView(
        EntityUid? viewer,
        EntityUid? patient,
        bool canScan,
        CMUBodyScannerConsoleComponent scanner,
        CMUBodyScannerOrigin? origin = null)
    {
        if (viewer is { } activeViewer)
            ExpireAttempt(activeViewer);
        var boostExpires = GetBoostExpiry(viewer, patient);
        var lockoutExpires = GetCalibrationLockoutExpiry(viewer, patient);
        var projection = canScan && patient is { } puzzlePatient
            ? GetPuzzleProjection(puzzlePatient)
            : default;
        var signals = projection.Signals ?? [];
        var targets = projection.Targets ?? [];
        CMUBodyScannerPuzzleProgressComponent? progress = null;
        if (canScan &&
            viewer is { } progressViewer &&
            patient is { } progressPatient &&
            signals.Count > 0 &&
            lockoutExpires is null &&
            TryComp<CMUBodyScannerPuzzleProgressComponent>(progressViewer, out progress))
        {
            if (progress.Patient != progressPatient || progress.Origin != origin)
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

        return new CMUBodyScannerCalibrationView(
            puzzleComplete,
            boostExpires,
            lockoutExpires,
            progress?.StartedAt,
            progress?.EndsAt,
            progress?.PulseStartedAt,
            GetPulsePeriod(scanner, completedLayers, layerCount),
            GetPulseTargetPhase(scanner, locked),
            GetPulseWindowSize(scanner, completedLayers, layerCount),
            GetPulseGraceSize(scanner, completedLayers, layerCount),
            progress?.LastPenaltyAt,
            progress?.LastPenaltySeconds ?? 0f,
            progress?.LastFeedbackAt,
            progress?.LastFeedbackKind ?? CMUBodyScannerFeedbackKind.None,
            layers,
            targets,
            assignments,
            progress?.AttemptId ?? 0);
    }

    private CMUBodyScannerPuzzleProgressComponent EnsurePuzzleProgress(
        EntityUid user,
        EntityUid patient,
        CMUBodyScannerConsoleComponent scanner)
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
        progress.AttemptId = ++_nextAttemptId;
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
        _scheduler.Schedule(user, BoostExpiryWork, boost.ExpiresAt);
        _scheduler.Cancel(user, AttemptExpiryWork);
        RemComp<CMUBodyScannerPuzzleProgressComponent>(user);
        AdvanceRevision(user);
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

        PruneLockouts(user, lockout);
        return lockout.Expiries.TryGetValue(body, out var expiry) ? expiry : null;
    }

    private TimeSpan ApplyCalibrationLockout(EntityUid user, CMUBodyScannerPuzzleProgressComponent progress, TimeSpan expiredAt)
    {
        var lockout = EnsureComp<CMUBodyScannerCalibrationLockoutComponent>(user);
        var expires = expiredAt + TimeSpan.FromSeconds(progress.LockoutDurationSeconds);
        // One slot was reserved when this attempt began. No live patient's penalty is evicted.
        lockout.Expiries[progress.Patient] = expires;
        _scheduler.Cancel(user, AttemptExpiryWork);
        RemComp<CMUBodyScannerPuzzleProgressComponent>(user);
        AdvanceRevision(user);
        PruneLockouts(user, lockout);
        return expires;
    }

    private bool ExpireAttempt(EntityUid user)
    {
        if (Paused(user) || !TryComp<CMUBodyScannerPuzzleProgressComponent>(user, out var progress))
            return false;
        if (TerminatingOrDeleted(progress.Patient))
        {
            _scheduler.Cancel(user, AttemptExpiryWork);
            RemComp<CMUBodyScannerPuzzleProgressComponent>(user);
            AdvanceRevision(user);
            return true;
        }
        if (_timing.CurTime < progress.EndsAt)
            return false;
        ApplyCalibrationLockout(user, progress, progress.EndsAt);
        return true;
    }

    private void PruneLockouts(EntityUid user, CMUBodyScannerCalibrationLockoutComponent lockouts)
    {
        if (Paused(user))
            return;
        _expiredPatients.Clear();
        TimeSpan? earliest = null;
        foreach (var (patient, expiry) in lockouts.Expiries)
        {
            if (TerminatingOrDeleted(patient) || expiry <= _timing.CurTime)
                _expiredPatients.Add(patient);
            else if (earliest == null || expiry < earliest)
                earliest = expiry;
        }
        foreach (var patient in _expiredPatients)
            lockouts.Expiries.Remove(patient);
        if (earliest is { } next)
        {
            if (lockouts.NextExpiry != next)
            {
                lockouts.NextExpiry = next;
                _scheduler.Schedule(user, LockoutExpiryWork, next);
            }
        }
        else
        {
            _scheduler.Cancel(user, LockoutExpiryWork);
            RemComp<CMUBodyScannerCalibrationLockoutComponent>(user);
        }
    }

    private (List<CMUBodyScannerPuzzleSignal> Signals, List<CMUBodyScannerSliceSignal> Targets)
        GetPuzzleProjection(EntityUid patient)
    {
        var cache = EnsureComp<CMUBodyScannerAnatomyCacheComponent>(patient);
        var revision = _medicalChanges.GetRevision(patient);
        var tick = _timing.CurTick;
        if (!cache.PuzzleValid ||
            cache.PuzzleMedicalRevision != revision ||
            cache.PuzzleBuiltAt != tick)
        {
            cache.PuzzleSignals = BuildPuzzleSignals(patient);
            cache.PuzzleTargets = BuildPuzzleTargets(cache.PuzzleSignals);
            cache.PuzzleMedicalRevision = revision;
            cache.PuzzleBuiltAt = tick;
            cache.PuzzleValid = true;
        }

        return (cache.PuzzleSignals, cache.PuzzleTargets);
    }

    private List<CMUBodyScannerPuzzleSignal> BuildPuzzleSignals(EntityUid patient)
    {
        var signals = new List<CMUBodyScannerPuzzleSignal>();

        foreach (var organ in _medicalIndex.GetOrgans(patient))
        {
            if (TryComp<HeartComponent>(organ.Owner, out var heart) && heart.Stopped)
            {
                AddPuzzleSignal(
                    signals,
                    $"cardiac:{organ.Owner}",
                    Loc.GetString("cmu-body-scanner-signal-heart-stopped"),
                    Loc.GetString("cmu-body-scanner-slice-detail-cardiac"),
                    SliceVitals,
                    0);
            }

            if (TryComp<OrganHealthComponent>(organ.Owner, out var organHealth) && organHealth.Stage != OrganDamageStage.Healthy)
            {
                AddPuzzleSignal(
                    signals,
                    $"organ:{organ.Owner}",
                    Loc.GetString("cmu-body-scanner-signal-organ-damage", ("organ", _readout.OrganName(organ.Owner)), ("stage", CMUBodyScannerReadoutSystem.FormatOrganStage(organHealth.Stage))),
                    Loc.GetString("cmu-body-scanner-slice-detail-organ"),
                    SliceOrgans,
                    organHealth.Stage.IsAtLeast(OrganDamageStage.Failing) ? 1 : 4);
            }
        }

        if (_bloodstream.TryGetBloodReadout(patient, out var blood, out var normalBlood))
        {
            if (normalBlood > FixedPoint2.Zero && blood < normalBlood * (FixedPoint2) 0.75f)
            {
                AddPuzzleSignal(
                    signals,
                    "blood:low",
                    Loc.GetString("cmu-body-scanner-signal-low-blood", ("blood", blood), ("max", normalBlood)),
                    Loc.GetString("cmu-body-scanner-slice-detail-blood"),
                    SliceVitals,
                    2);
            }
        }

        foreach (var (part, partComp) in _medicalIndex.GetBodyParts(patient))
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
                foreach (var entry in _woundLedger.GetEntries(wounds))
                {
                    if (!entry.Wound.Treated)
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
                health.Current < health.Max * (FixedPoint2) 0.75f)
            {
                AddPuzzleSignal(
                    signals,
                    $"trauma:{part}",
                    Loc.GetString("cmu-body-scanner-signal-trauma", ("part", partName), ("current", health.Current), ("max", health.Max)),
                    Loc.GetString("cmu-body-scanner-slice-detail-trauma"),
                    SliceTissue,
                    6);
            }

            foreach (var slot in _medicalIndex.GetOrganSlots(part))
            {
                if (slot.Organ is not null)
                    continue;

                AddPuzzleSignal(
                    signals,
                    $"missing:{part}:{slot.SlotId}",
                    Loc.GetString("cmu-body-scanner-signal-missing-organ", ("organ", _readout.OrganSlotName(slot.SlotId)), ("part", partName)),
                    Loc.GetString("cmu-body-scanner-slice-detail-missing-organ"),
                    SliceOrgans,
                    0);
            }
        }

        foreach (var (type, symmetry) in _readout.GetMissingLimbSlots(patient))
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

    private static void AddPuzzleSignal(
        List<CMUBodyScannerPuzzleSignal> signals,
        string id,
        string text,
        string detail,
        string layerId,
        int priority)
    {
        foreach (var signal in signals)
        {
            if (signal.Id == id)
                return;
        }

        signals.Add(new CMUBodyScannerPuzzleSignal(id, layerId, text, detail, priority));
    }

    private List<CMUBodyScannerSliceSignal> BuildPuzzleTargets(List<CMUBodyScannerPuzzleSignal> signals)
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

    private static List<CMUBodyScannerPuzzleAssignment> GetPuzzleAssignments(
        CMUBodyScannerPuzzleProgressComponent? progress,
        List<CMUBodyScannerPuzzleSignal> signals)
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

    private static bool HasSignalLayer(List<CMUBodyScannerPuzzleSignal> signals, string layerId)
    {
        foreach (var signal in signals)
        {
            if (signal.LayerId == layerId)
                return true;
        }

        return false;
    }

    private static int CountSignalsForLayer(List<CMUBodyScannerPuzzleSignal> signals, string layerId)
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

    private static bool TryGetSignal(
        List<CMUBodyScannerPuzzleSignal> signals,
        string id,
        out CMUBodyScannerPuzzleSignal signal)
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
        EntityUid user,
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
        AdvanceRevision(user);
        _scheduler.Schedule(user, AttemptExpiryWork, progress.EndsAt);
    }

    private float GetServerPulsePhase(
        CMUBodyScannerPuzzleProgressComponent progress,
        CMUBodyScannerConsoleComponent scanner,
        int completedLayers,
        int layerCount)
    {
        var period = MathF.Max(0.1f, GetPulsePeriod(scanner, completedLayers, layerCount));
        var elapsed = (_timing.CurTime - progress.PulseStartedAt).TotalSeconds;
        var phase = (float) (elapsed / period);
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

    private static int CountSignalLayers(List<CMUBodyScannerPuzzleSignal> signals)
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
        List<CMUBodyScannerPuzzleSignal> signals,
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

    private static bool PuzzleSolved(
        List<CMUBodyScannerPuzzleSignal> signals,
        List<CMUBodyScannerPuzzleAssignment> assignments)
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
