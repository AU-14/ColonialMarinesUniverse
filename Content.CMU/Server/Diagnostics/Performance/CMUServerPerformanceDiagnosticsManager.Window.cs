using System.Globalization;
using Content.Shared.CCVar;
using Content.Shared.Movement.Components;
using Content.Shared.Projectiles;
using Robust.Server.GameStates;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Content.Server.CMU14.Diagnostics.Performance;

public sealed partial class CMUServerPerformanceDiagnosticsManager
{
    [Dependency] private IServerGameStateManager _windowGameStates = default!;

    private readonly CMUPerformanceTickWindow _windowTimings = new();
    private readonly Dictionary<ICommonSession, (GameTick Tick, TimeSpan At)> _windowAcks = new();
    private readonly Dictionary<(EntityUid Map, int X, int Y), int> _windowCells = new();
    private readonly List<double> _windowPings = new();
    private readonly List<double> _windowAckLags = new();
    private CMUPerformancePvsMetrics? _windowPvs;
    private bool _windowEnabled;
    private TimeSpan _windowStarted;
    private TimeSpan _windowNextReport;
    private TimeSpan _windowGcPause;
    private uint _windowStartTick;
    private long _windowInitialRequests;
    private long _windowRecoveryRequests;
    private long _windowProjectilesAdded;
    private long _windowGunfireBaseline;
    private long _windowSequence;
    private NetworkStats _windowNetworkBaseline;

    private void InitializePerformanceWindow()
    {
        _config.OnValueChanged(CCVars.CMUServerPerformanceWindowEnabled, RefreshPerformanceWindowEnabled, true);
    }

    private void ShutdownPerformanceWindow()
    {
        _config.UnsubValueChanged(CCVars.CMUServerPerformanceWindowEnabled, RefreshPerformanceWindowEnabled);
        SetPerformanceWindowEnabled(false);
    }

    private void RefreshPerformanceWindowEnabled(bool _) =>
        SetPerformanceWindowEnabled(_enabled && _config.GetCVar(CCVars.CMUServerPerformanceWindowEnabled));

    private void SetPerformanceWindowEnabled(bool enabled)
    {
        if (_windowEnabled == enabled)
            return;
        _windowEnabled = enabled;
        if (enabled)
        {
            _windowPvs ??= new(Prometheus.Metrics.DefaultFactory, _timing.RealTime);
            _phases.PhaseCompleted += OnWindowPhase;
            _windowGameStates.ClientAck += OnWindowAck;
            _windowGameStates.ClientRequestFull += OnWindowRequest;
            _playerManager.PlayerStatusChanged += OnWindowPlayerStatus;
            ResetPerformanceWindow(_timing.RealTime, true);
        }
        else
        {
            _phases.PhaseCompleted -= OnWindowPhase;
            _windowGameStates.ClientAck -= OnWindowAck;
            _windowGameStates.ClientRequestFull -= OnWindowRequest;
            _playerManager.PlayerStatusChanged -= OnWindowPlayerStatus;
            _windowTimings.Clear();
            _windowAcks.Clear();
            _windowCells.Clear();
            _windowPings.Clear();
            _windowAckLags.Clear();
        }
        _sawmill.Info($"[CMU-PERF] window-mode enabled={enabled} aggregateOnly=true");
    }

    private void ResetPerformanceWindow(TimeSpan now, bool clearAcks = false)
    {
        if (!_windowEnabled)
            return;
        BeginPerformanceWindow(now);
        if (clearAcks)
            _windowAcks.Clear();
        _windowNetworkBaseline = _netManager.Statistics;
        _windowGunfireBaseline = _entitySystemManager.GetEntitySystem<CMUCombatMetricsSystem>().GunfireEvents;
        _windowGcPause = GC.GetTotalPauseDuration();
        _windowPvs!.Reset(now);
    }

    private void BeginPerformanceWindow(TimeSpan now)
    {
        _windowStarted = now;
        _windowStartTick = _timing.CurTick.Value;
        var interval = _config.GetCVar(CCVars.CMUServerPerformanceWindowInterval);
        _windowNextReport = now + TimeSpan.FromSeconds(float.IsFinite(interval) ? Math.Clamp(interval, 5, 60) : 5);
        _windowTimings.Clear();
        _windowInitialRequests = 0;
        _windowRecoveryRequests = 0;
        _windowProjectilesAdded = 0;
    }

    private void OnWindowPhase(CMUPerformancePhaseTracker.Phase phase, TimeSpan started)
    {
        // Enabling/resetting mid-tick must not mix a partial or earlier phase into this window.
        if (started >= _windowStarted)
            _windowTimings.Record(phase, 1000 / GetTargetTps());
    }

    private void OnWindowAck(ICommonSession session, GameTick tick)
    {
        if (tick == GameTick.Zero || tick >= _timing.CurTick ||
            _windowAcks.TryGetValue(session, out var previous) && tick <= previous.Tick)
            return;
        _windowAcks[session] = (tick, _timing.RealTime);
    }

    private void OnWindowRequest(ICommonSession session, GameTick tick, NetEntity? missing)
    {
        if (tick == GameTick.Zero)
            _windowInitialRequests++;
        else
            _windowRecoveryRequests++;
    }

    private void OnWindowPlayerStatus(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _windowAcks.Remove(args.Session);
    }

    private void ObserveWindowProjectile(AddedComponentEventArgs args)
    {
        if (_windowEnabled && args.BaseArgs.Component is ProjectileComponent)
            _windowProjectilesAdded++;
    }

    public bool CaptureWindowReport()
    {
        var now = _timing.RealTime;
        if (!_initialized || !_windowEnabled || now <= _windowStarted)
            return false;
        WritePerformanceWindow(now);
        return true;
    }

    private void WritePerformanceWindow(TimeSpan now)
    {
        var started = Stopwatch.GetTimestamp();
        _windowSequence++;
        var seconds = (now - _windowStarted).TotalSeconds;
        var transform = _entitySystemManager.GetEntitySystem<SharedTransformSystem>();
        _windowCells.Clear();
        _windowPings.Clear();
        _windowAckLags.Clear();
        var players = 0;
        var attached = 0;
        var moving = 0;
        var inputHeld = 0;
        var contacting = 0;
        long contacts = 0;
        var maxContacts = 0;
        var busiestCell = 0;
        double maxAckAge = 0;
        foreach (var session in _playerManager.NetworkedSessions)
        {
            players++;
            if (session.Ping >= 0)
                _windowPings.Add(session.Ping);
            if (_windowAcks.TryGetValue(session, out var ack) && ack.Tick <= _timing.CurTick)
            {
                _windowAckLags.Add(_timing.CurTick.Value - ack.Tick.Value);
                maxAckAge = Math.Max(maxAckAge, (now - ack.At).TotalMilliseconds);
            }
            if (session.AttachedEntity is not { } uid)
                continue;
            attached++;
            if (_entityManager.TryGetComponent<InputMoverComponent>(uid, out var mover) && mover.HasDirectionalMovement)
                inputHeld++;
            if (_entityManager.TryGetComponent<PhysicsComponent>(uid, out var body))
            {
                if (body.LinearVelocity.LengthSquared() > 0.01f)
                    moving++;
                if (body.ContactCount > 0)
                    contacting++;
                contacts += body.ContactCount;
                maxContacts = Math.Max(maxContacts, body.ContactCount);
            }
            if (_entityManager.TryGetComponent<TransformComponent>(uid, out var xform) && xform.MapUid is { } map)
            {
                var position = transform.GetWorldPosition(xform);
                if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                    continue;
                var cell = (map, (int) MathF.Floor(position.X / 8), (int) MathF.Floor(position.Y / 8));
                _windowCells.TryGetValue(cell, out var count);
                _windowCells[cell] = ++count;
                busiestCell = Math.Max(busiestCell, count);
            }
        }
        _windowPings.Sort();
        _windowAckLags.Sort();
        var network = _netManager.Statistics;
        var networkValid = network.SentBytes >= _windowNetworkBaseline.SentBytes &&
                           network.ReceivedBytes >= _windowNetworkBaseline.ReceivedBytes &&
                           network.SentPackets >= _windowNetworkBaseline.SentPackets &&
                           network.ReceivedPackets >= _windowNetworkBaseline.ReceivedPackets;
        var gunfireTotal = _entitySystemManager.GetEntitySystem<CMUCombatMetricsSystem>().GunfireEvents;
        var gunfire = gunfireTotal - _windowGunfireBaseline;
        var gcPause = GC.GetTotalPauseDuration();
        var pvs = _windowPvs!.Drain(now);
        _sawmill.Info(Invariant(
            $"[CMU-PERF] performance-window windowId={_windowSequence} incidentId={_activeIncidentId} round={_ticker?.RoundId ?? 0} phase={_ticker?.RunLevel.ToString() ?? "unknown"} paused={_timing.Paused} ",
            $"startTick={_windowStartTick} endTick={_timing.CurTick.Value} windowSeconds={seconds:F3} targetTps={GetTargetTps():F2} ",
            $"players={players} attachedPlayers={attached} movingPlayers={moving} inputHeldPlayers={inputHeld} ",
            $"contactingPlayers={contacting} playerContactEdges={contacts} maxPlayerContacts={maxContacts} busiest8x8CellPlayers={busiestCell} ",
            $"pingSamples={_windowPings.Count} pingP50Ms={Percentile(_windowPings, .5)} pingP95Ms={Percentile(_windowPings, .95)} ",
            $"ackSamples={_windowAckLags.Count} unknownAckPlayers={players - _windowAckLags.Count} ackLagP95Ticks={Percentile(_windowAckLags, .95)} ",
            $"maxAckProgressAgeMs={(_windowAckLags.Count > 0 ? FormatWindowMetric(maxAckAge) : "unavailable")} ",
            $"initialFullRequests={_windowInitialRequests} recoveryFullRequests={_windowRecoveryRequests} gunfireEvents={gunfire} projectileComponentsAdded={_windowProjectilesAdded} ",
            $"networkStatus={(networkValid ? "observed" : "counter-reset")} ",
            $"sendBytesPerSecond={FormatWindowMetric(networkValid ? (network.SentBytes - _windowNetworkBaseline.SentBytes) / seconds : null)} ",
            $"receiveBytesPerSecond={FormatWindowMetric(networkValid ? (network.ReceivedBytes - _windowNetworkBaseline.ReceivedBytes) / seconds : null)} ",
            $"sentPackets={FormatWindowMetric(networkValid ? network.SentPackets - _windowNetworkBaseline.SentPackets : null)} ",
            $"receivedPackets={FormatWindowMetric(networkValid ? network.ReceivedPackets - _windowNetworkBaseline.ReceivedPackets : null)} ",
            $"gcPauseMs={Math.Max(0, (gcPause - _windowGcPause).TotalMilliseconds):F3} bodyCohort=attached-session networkCohort=connected-session snapshotAt=window-end ") + DescribeMemory());
        LogWindowTiming("simulation", _windowTimings.Simulation, seconds);
        LogWindowTiming("post-tick-state-send", _windowTimings.StateSend, seconds);
        LogWindowTiming("combined-tick", _windowTimings.Combined, seconds);
        foreach (var stage in pvs.Stages)
        {
            _sawmill.Info(Invariant(
                $"[CMU-PERF] pvs-window windowId={_windowSequence} area={SanitizeName(stage.Name)} calls={stage.Calls} ",
                $"totalMs={FormatWindowMetric(stage.TotalMs)} avgMs={FormatWindowMetric(stage.AverageMs)} status={stage.Status} ",
                $"windowSeconds={seconds:F3} processWide=true inclusive=true source=engine-histogram"));
        }
        _sawmill.Info(Invariant($"[CMU-PERF] window-report-cost windowId={_windowSequence} elapsedMs={Stopwatch.GetElapsedTime(started).TotalMilliseconds:F3} ",
            $"sessionsScanned={players} fullWorldScan=false"));
        // Keep the observed endpoints: re-reading counters here would discard work that
        // networking/PVS workers completed while these log rows were being emitted.
        _windowNetworkBaseline = network;
        _windowGunfireBaseline = gunfireTotal;
        _windowGcPause = gcPause;
        BeginPerformanceWindow(now);
    }

    private void LogWindowTiming(string phase, CMUPerformanceTickWindow.Distribution row, double seconds)
    {
        _sawmill.Info(Invariant($"[CMU-PERF] tick-window windowId={_windowSequence} phase={phase} calls={row.Count} ",
            $"avgMs={FormatWindowMetric(row.Count > 0 ? row.TotalMs / row.Count : null)} maxMs={FormatWindowMetric(row.Count > 0 ? row.MaxMs : null)} ",
            $"p95UpperMs={FormatWindowMetric(row.PercentileUpperBound(.95))} p99UpperMs={FormatWindowMetric(row.PercentileUpperBound(.99))} ",
            $"overBudget={row.OverBudget} worstTick={row.WorstTick} windowSeconds={seconds:F3} ",
            $"status={(row.Count > 0 ? "observed" : "no-observations")} source=module-boundaries excludesIdle=true"));
    }

    private static string Percentile(List<double> sorted, double fraction) => sorted.Count == 0
        ? "unavailable"
        : FormatWindowMetric(sorted[Math.Max(0, (int) Math.Ceiling(sorted.Count * fraction) - 1)]);

    private static string FormatWindowMetric(double? value) => value is { } number
        ? double.IsPositiveInfinity(number) ? ">1000" : number.ToString("F3", CultureInfo.InvariantCulture)
        : "unavailable";
}
