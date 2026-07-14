using System.Diagnostics.Metrics;
using System.Globalization;
using System.Linq;
using System.Text;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Robust.Server.DataMetrics;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Profiling;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Diagnostics.Performance;

public sealed partial class CMUServerPerformanceDiagnosticsManager : ICMUServerPerformanceDiagnostics, IPostInjectInit
{
    private const double BytesPerMiB = 1024d * 1024d;
    private const double ShortRateWindowSeconds = 5;
    private const double ChurnRateWindowSeconds = 60;
    private const double ProfilerProbeSeconds = 0.05;

    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private IEntitySystemManager _entitySystemManager = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IMeterFactory _meterFactory = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IServerNetManager _netManager = default!;
    [Dependency] private ProfManager _profiler = default!;

    private readonly CMUPerformanceIncidentDetector _detector = new();
    private readonly CMUPerformanceChurnTracker _churn = new();
    private readonly CMUPerformanceRollingWindow _shortRates = new(ShortRateWindowSeconds);
    private readonly CMUPerformanceRollingWindow _churnRates = new(ChurnRateWindowSeconds);

    private ISawmill _sawmill = default!;
    private GameTicker? _ticker;
    private Meter? _meter;
    private Counter<long>? _incidentCounter;
    private Counter<long>? _entityCreateCounter;
    private Counter<long>? _entityDeleteCounter;
    private Counter<long>? _componentAddCounter;
    private Counter<long>? _componentRemoveCounter;

    private CMUPerformanceChurnSnapshot? _churnBaseline;
    private Dictionary<Type, long> _lastMessageBandwidth = new();
    private NetworkStats _lastNetworkStats;
    private CMUServerPerformanceObservation? _lastObservation;

    private bool _initialized;
    private bool _enabled;
    private bool _eventsHooked;
    private bool _needsFullReset;
    private bool _warmupBaselinePending;
    private bool _haveNetworkStats;
    private bool _profilerEnabledByDiagnostics;
    private bool _changingProfilerCVar;
    private int _lastRoundId;
    private long _incidentSequence;
    private long _activeIncidentId;
    private int _suppressedDetailReports;
    private bool _detailPending;
    private string _incidentSeverity = "warning";

    private TimeSpan _lastSampleTime;
    private TimeSpan _nextSampleTime;
    private TimeSpan _nextProfilerProbeTime;
    private TimeSpan _warmupUntil;
    private TimeSpan _nextHeartbeatTime;
    private TimeSpan _nextIncidentUpdateTime;
    private TimeSpan _nextBaselineTime;
    private TimeSpan _nextDetailTime;
    private TimeSpan _incidentStartTime;

    private double _worstFrameMilliseconds;
    private double _worstTps = double.PositiveInfinity;
    private double _worstFps = double.PositiveInfinity;
    private long _worstAllocatedBytes;
    private long _profileIndexOffset;
    private CMUPerformanceProfileFrame? _maxAllocatedFrameSinceSample;
    private bool _allocationBreachPending;

    // Cached observable metric values. Callbacks never enumerate game state.
    private long _metricEnabled;
    private double _metricTps;
    private double _metricTargetTps;
    private double _metricFps;
    private double _metricFrameSeconds;
    private double _metricFrameAverageSeconds;
    private double _metricFrameStdDevSeconds;
    private double _metricBacklogSeconds;
    private long _metricEntities;
    private long _metricComponents;
    private double _metricEntityGrowthPerMinute;
    private double _metricEntityChurnPerMinute;
    private double _metricComponentGrowthPerMinute;
    private double _metricComponentChurnPerMinute;
    private double _metricEntityDirtiesPerSecond;
    private double _metricSendBytesPerSecond;
    private double _metricReceiveBytesPerSecond;
    private long _metricAllocatedBytes;
    private long _metricIncidentActive;
    private long _metricLastCompletedTick;
    private double _metricLastUpdateUnixSeconds;

    public void Initialize()
    {
        if (_initialized)
            return;

        _initialized = true;
        _enabled = _config.GetCVar(CCVars.CMUServerPerformanceDiagnosticsEnabled);
        _metricEnabled = _enabled ? 1 : 0;
        _ticker = _entitySystemManager.GetEntitySystem<GameTicker>();
        _lastRoundId = _ticker.RoundId;

        _config.OnValueChanged(CCVars.CMUServerPerformanceDiagnosticsEnabled, SetEnabled);
        _config.OnValueChanged(CVars.ProfEnabled, OnProfilerEnabledChanged);

        InitializeMetrics();

        if (_enabled)
        {
            HookEntityEvents();
            ResetTracking("startup");
            EnableProfilerIfConfigured();
        }

        LogStartupState();
    }

    public void Update()
    {
        if (!_initialized)
            return;

        if (!_enabled)
            return;

        TimeSpan now = _timing.RealTime;
        _metricLastCompletedTick = _timing.CurTick.Value;
        bool allocationBreach = ProbeProfiler(now);

        if (_needsFullReset)
        {
            _needsFullReset = false;
            ResetTracking("entity-flush");
        }

        if (_ticker != null && _ticker.RoundId != _lastRoundId)
        {
            _lastRoundId = _ticker.RoundId;
            ResetEpoch("round-change");
        }

        double stallThreshold = Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceStallMilliseconds));
        bool hardStall = stallThreshold > 0 &&
                         _timing.RealFrameTime.TotalMilliseconds >= stallThreshold;
        if (!hardStall && !allocationBreach && now < _nextSampleTime)
            return;

        Sample(now);
    }

    public void Shutdown()
    {
        if (!_initialized)
            return;

        _config.UnsubValueChanged(CCVars.CMUServerPerformanceDiagnosticsEnabled, SetEnabled);
        _config.UnsubValueChanged(CVars.ProfEnabled, OnProfilerEnabledChanged);
        UnhookEntityEvents();
        if (_profilerEnabledByDiagnostics && _profiler.IsEnabled)
            SetProfilerCVar(false);
        _profilerEnabledByDiagnostics = false;
        _meter?.Dispose();
        _initialized = false;
    }

    public string GetStatus()
    {
        if (!_initialized)
            return "CMU server performance diagnostics are not initialized.";
        if (!_enabled)
            return "CMU server performance diagnostics are disabled.";
        if (_lastObservation is not { } observation)
            return "CMU server performance diagnostics are enabled and waiting for the first sample.";

        string reasons = CMUPerformanceIncidentDetector.FormatReasons(_detector.ActiveReasons);
        return Invariant(
            $"enabled=true incident={_detector.Active} incidentId={_activeIncidentId} reasons={reasons} ",
            $"tick={observation.Tick} targetTps={observation.TargetTps:F2} achievedTps={observation.AchievedTps:F2} ",
            $"fps={observation.AverageFps:F2} frameMs={observation.FrameMilliseconds:F2} ",
            $"entities={observation.EntityCount} components={observation.ComponentCount} ",
            $"players={observation.Players} profiler={_profiler.IsEnabled} metrics={_config.GetCVar(CVars.MetricsEnabled)}");
    }

    public bool CaptureManualReport()
    {
        if (!_initialized || !_enabled || _lastObservation is not { } observation)
            return false;

        _sawmill.Warning(BuildObservationLine("manual-report", observation, _activeIncidentId,
            _detector.ActiveReasons, _detector.Active ? _incidentSeverity : "manual"));
        CaptureDetailedReport(observation, "manual", bypassCooldown: true);
        return true;
    }

    public bool ResetBaselines()
    {
        if (!_initialized || !_enabled)
            return false;

        ResetEpoch("manual-reset");
        return true;
    }

    void IPostInjectInit.PostInject()
    {
        _sawmill = _logManager.GetSawmill("cmu.server-performance");
    }

    private void Sample(TimeSpan now)
    {
        double interval = Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceSampleInterval), 0.1f, 60f);
        double elapsed = _lastSampleTime == default
            ? interval
            : Math.Max(0.001, (now - _lastSampleTime).TotalSeconds);
        _lastSampleTime = now;
        _nextSampleTime = now + TimeSpan.FromSeconds(interval);

        int entityCount = _entityManager.EntityCount;
        bool warmingUp = now < _warmupUntil;
        if (_warmupBaselinePending && !warmingUp)
            CompleteWarmupBaseline();

        var point = new CMUPerformanceCounterPoint(
            _timing.CurTick.Value,
            entityCount,
            _churn.EntitiesCreated,
            _churn.EntitiesDeleted,
            _churn.ComponentCount,
            _churn.ComponentsAdded,
            _churn.ComponentsRemoved,
            _churn.EntitiesDirtied);
        bool suppressRates = _timing.Paused || warmingUp;
        if (suppressRates)
        {
            _shortRates.Reset();
            _churnRates.Reset();
        }
        else
        {
            _shortRates.Add(now.TotalSeconds, point);
            _churnRates.Add(now.TotalSeconds, point);
        }

        bool tpsValid = _shortRates.TryGetRates(2, out CMUPerformanceRates shortRates);
        bool churnValid = _churnRates.TryGetRates(15, out CMUPerformanceRates churnRates);
        double targetTps = GetTargetTps();

        NetworkStats network = _netManager.Statistics;
        double sendBytesPerSecond = 0;
        double receiveBytesPerSecond = 0;
        if (_haveNetworkStats)
        {
            sendBytesPerSecond = NonNegativeDelta(network.SentBytes, _lastNetworkStats.SentBytes) / elapsed;
            receiveBytesPerSecond = NonNegativeDelta(network.ReceivedBytes, _lastNetworkStats.ReceivedBytes) / elapsed;
        }

        _lastNetworkStats = network;
        _haveNetworkStats = true;
        IReadOnlyList<CMUNetworkMessageRate> messageRates = CaptureMessageRates(elapsed);

        long allocatedBytes = Math.Max(0, _maxAllocatedFrameSinceSample?.AllocatedBytes ?? 0);
        long? profileFrame = _maxAllocatedFrameSinceSample?.Frame;
        _metricAllocatedBytes = allocatedBytes;
        _maxAllocatedFrameSinceSample = null;
        _allocationBreachPending = false;

        int attachedPlayers = _playerManager.NetworkedSessions.Count(session => session.AttachedEntity != null);
        double backlogSeconds = Math.Max(0, (now - _timing.LastTick).TotalSeconds);
        var observation = new CMUServerPerformanceObservation(
            now,
            _timing.CurTick.Value,
            targetTps,
            tpsValid ? Math.Max(0, shortRates.TicksPerSecond) : targetTps,
            tpsValid,
            _timing.FramesPerSecondAvg,
            _timing.RealFrameTime.TotalMilliseconds,
            _timing.RealFrameTimeAvg.TotalMilliseconds,
            _timing.RealFrameTimeStdDev.TotalMilliseconds,
            backlogSeconds * 1000,
            entityCount,
            _churn.ComponentCount,
            _playerManager.PlayerCount,
            attachedPlayers,
            _netManager.ChannelCount,
            churnValid ? churnRates.EntityGrowthPerSecond * 60 : 0,
            churnValid ? churnRates.EntityChurnPerSecond * 60 : 0,
            churnValid ? churnRates.ComponentGrowthPerSecond * 60 : 0,
            churnValid ? churnRates.ComponentChurnPerSecond * 60 : 0,
            churnValid,
            churnValid ? churnRates.EntityDirtiesPerSecond : 0,
            sendBytesPerSecond,
            receiveBytesPerSecond,
            network.SentPackets,
            network.ReceivedPackets,
            allocatedBytes,
            profileFrame,
            suppressRates,
            _timing.Paused,
            _ticker?.RoundId ?? 0,
            _ticker?.RunLevel.ToString() ?? "unknown",
            _ticker?.RoundDuration().TotalSeconds ?? 0,
            messageRates);
        _lastObservation = observation;

        CMUPerformanceIncidentThresholds thresholds = ReadThresholds();
        var sample = new CMUPerformanceIncidentSample(
            elapsed,
            observation.FrameMilliseconds,
            observation.AchievedTps,
            observation.TpsValid,
            observation.AverageFps,
            observation.TargetTps,
            observation.EntityGrowthPerMinute,
            observation.EntityChurnPerMinute,
            observation.ComponentGrowthPerMinute,
            observation.ComponentChurnPerMinute,
            observation.ChurnValid,
            observation.SendBytesPerSecond,
            observation.ReceiveBytesPerSecond,
            observation.AllocatedBytes,
            observation.SuppressRateTriggers);

        CMUPerformanceIncidentEvaluation evaluation = _detector.Evaluate(sample, thresholds);
        HandleEvaluation(observation, evaluation);
        UpdateMetrics(observation);
        HandleHeartbeatAndBaseline(observation);
    }

    private void HandleEvaluation(
        in CMUServerPerformanceObservation observation,
        in CMUPerformanceIncidentEvaluation evaluation)
    {
        switch (evaluation.Transition)
        {
            case CMUPerformanceIncidentTransition.Started:
                _activeIncidentId = ++_incidentSequence;
                _incidentStartTime = observation.RealTime;
                _incidentSeverity = GetSeverity(observation);
                _suppressedDetailReports = 0;
                _detailPending = false;
                ResetWorst(observation);
                _metricIncidentActive = 1;
                _incidentCounter?.Add(1);
                _nextIncidentUpdateTime = observation.RealTime + GetIncidentUpdateInterval();
                _sawmill.Warning(BuildObservationLine(
                    "incident-open",
                    observation,
                    _activeIncidentId,
                    evaluation.Reasons,
                    _incidentSeverity));

                if (observation.RealTime >= _nextDetailTime)
                    CaptureDetailedReport(observation, "incident-open", bypassCooldown: false);
                else
                {
                    _suppressedDetailReports++;
                    _detailPending = true;
                    _sawmill.Warning(Invariant(
                        $"[CMU-PERF] detail-suppressed incidentId={_activeIncidentId} ",
                        $"remainingSeconds={(_nextDetailTime - observation.RealTime).TotalSeconds:F1} reason=cooldown"));
                }
                break;

            case CMUPerformanceIncidentTransition.Updated:
                UpdateWorst(observation);
                string severity = GetSeverity(observation);
                if (severity == "critical")
                    _incidentSeverity = severity;
                _sawmill.Warning(BuildObservationLine(
                    "incident-reasons-changed",
                    observation,
                    _activeIncidentId,
                    evaluation.Reasons,
                    _incidentSeverity));
                CapturePendingDetailIfReady(observation);
                break;

            case CMUPerformanceIncidentTransition.Recovered:
                CapturePendingDetailIfReady(observation);
                UpdateWorst(observation);
                _sawmill.Warning(Invariant(
                    $"[CMU-PERF] incident-close incidentId={_activeIncidentId} severity={_incidentSeverity} ",
                    $"durationSeconds={(observation.RealTime - _incidentStartTime).TotalSeconds:F1} ",
                    $"previousReasons={CMUPerformanceIncidentDetector.FormatReasons(evaluation.PreviousReasons)} ",
                    $"worstFrameMs={_worstFrameMilliseconds:F2} worstTps={FiniteOrZero(_worstTps):F2} ",
                    $"worstFps={FiniteOrZero(_worstFps):F2} worstAllocatedBytes={_worstAllocatedBytes} ",
                    $"suppressedDetailReports={_suppressedDetailReports}"));
                _metricIncidentActive = 0;
                _activeIncidentId = 0;
                _incidentSeverity = "warning";
                _detailPending = false;
                break;

            case CMUPerformanceIncidentTransition.None:
                if (!_detector.Active)
                    break;

                UpdateWorst(observation);
                if (_incidentSeverity != "critical" && GetSeverity(observation) == "critical")
                {
                    _incidentSeverity = "critical";
                    _sawmill.Warning(BuildObservationLine(
                        "incident-escalated",
                        observation,
                        _activeIncidentId,
                        _detector.ActiveReasons,
                        _incidentSeverity));
                }

                CapturePendingDetailIfReady(observation);
                if (observation.RealTime >= _nextIncidentUpdateTime)
                {
                    _sawmill.Warning(BuildObservationLine(
                        "incident-update",
                        observation,
                        _activeIncidentId,
                        _detector.ActiveReasons,
                        _incidentSeverity));
                    _nextIncidentUpdateTime = observation.RealTime + GetIncidentUpdateInterval();
                }
                break;
        }
    }

    private void HandleHeartbeatAndBaseline(in CMUServerPerformanceObservation observation)
    {
        if (_detector.Active)
            return;

        double heartbeatSeconds = Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceHeartbeatInterval));
        if (heartbeatSeconds > 0 && observation.RealTime >= _nextHeartbeatTime)
        {
            _sawmill.Info(BuildObservationLine(
                "heartbeat",
                observation,
                0,
                CMUPerformanceIncidentReason.None,
                observation.SuppressRateTriggers ? "warmup" : "healthy"));
            _nextHeartbeatTime = observation.RealTime + TimeSpan.FromSeconds(heartbeatSeconds);
        }

        double baselineSeconds = Math.Max(5, _config.GetCVar(CCVars.CMUServerPerformanceBaselineInterval));
        if (observation.RealTime < _nextBaselineTime || observation.SuppressRateTriggers)
            return;

        _churnBaseline = _churn.Snapshot();
        _nextBaselineTime = observation.RealTime + TimeSpan.FromSeconds(baselineSeconds);
        _sawmill.Info(Invariant(
            $"[CMU-PERF] baseline-refresh tick={observation.Tick} round={observation.RoundId} ",
            $"entities={observation.EntityCount} components={observation.ComponentCount} ",
            $"nextSeconds={baselineSeconds:F1}"));
    }

    private void CaptureDetailedReport(
        in CMUServerPerformanceObservation observation,
        string source,
        bool bypassCooldown)
    {
        _detailPending = false;
        if (!bypassCooldown)
        {
            double cooldown = Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceDetailCooldown));
            _nextDetailTime = observation.RealTime + TimeSpan.FromSeconds(cooldown);
        }

        int top = Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceReportTop), 1, 25);
        CMUPerformanceChurnSnapshot current = _churn.Snapshot();
        CMUPerformanceChurnSnapshot baseline = _churnBaseline ?? current;
        var systemNames = _entitySystemManager.GetEntitySystemTypes()
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);
        int frames = Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceProfileFrames), 1, 64);
        int maxEvents = Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceProfileMaxEvents), 128, 100000);
        CMUPerformanceProfileReport profile = CMUPerformanceProfilerReader.Capture(
            _profiler,
            systemNames,
            frames,
            maxEvents);

        _sawmill.Warning(Invariant(
            $"[CMU-PERF] detail-begin incidentId={_activeIncidentId} source={source} profiler={_profiler.IsEnabled} ",
            $"profileFrames={profile.Frames.Count} profileEvents={profile.EventsRead} truncated={profile.Truncated} ",
            $"baselineEntitiesCreated={baseline.EntitiesCreated} currentEntitiesCreated={current.EntitiesCreated} ",
            $"baselineComponentsAdded={baseline.ComponentsAdded} currentComponentsAdded={current.ComponentsAdded}"));

        LogProfile(profile, top);
        LogChurnRows("prototype-churn", CMUPerformanceChurnTracker.GetPrototypeRows(baseline, current, top));
        LogChurnRows("component-churn", CMUPerformanceChurnTracker.GetComponentRows(baseline, current, top));
        LogChurnRows("map-creates", CMUPerformanceChurnTracker.GetMapCreationRows(baseline, current, top));

        foreach (CMUNetworkMessageRate message in observation.MessageRates.Take(top))
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] inbound-network-message incidentId={_activeIncidentId} type={message.Name} ",
                $"bytesPerSecond={message.BytesPerSecond:F0}"));
        }

        _sawmill.Warning(Invariant(
            $"[CMU-PERF] detail-end incidentId={_activeIncidentId} source={source}"));

        // The report itself allocates and runs inside the current profiler frame. Skip that frame so diagnostics do not
        // attribute their own bounded report generation to gameplay or open a follow-up allocation incident.
        _profileIndexOffset = _profiler.Buffer.IndexWriteOffset + 1;
        _maxAllocatedFrameSinceSample = null;
        _allocationBreachPending = false;
    }

    private void CapturePendingDetailIfReady(in CMUServerPerformanceObservation observation)
    {
        if (!_detailPending || observation.RealTime < _nextDetailTime)
            return;

        CaptureDetailedReport(observation, "cooldown-expired", bypassCooldown: false);
    }

    private void LogProfile(CMUPerformanceProfileReport profile, int top)
    {
        if (profile.Frames.Count == 0)
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] profile-unavailable incidentId={_activeIncidentId} enabled={_profiler.IsEnabled} ",
                $"hint=enable-profiler-before-incident"));
            return;
        }

        foreach (CMUPerformanceProfileFrame frame in profile.Frames
                     .OrderByDescending(row => row.TimeSeconds)
                     .Take(Math.Min(top, 5)))
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] profile-frame incidentId={_activeIncidentId} index={frame.IndexOffset} frame={frame.Frame?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} ",
                $"timeMs={frame.TimeSeconds * 1000:F3} allocatedBytes={frame.AllocatedBytes} ticks={frame.TickCount}"));
        }

        LogProfileRows("system-time", profile.Samples
            .Where(row => row.EntitySystem)
            .OrderByDescending(row => row.TotalSeconds)
            .Take(top));
        LogProfileRows("system-allocation", profile.Samples
            .Where(row => row.EntitySystem)
            .OrderByDescending(row => row.TotalAllocatedBytes)
            .Take(top));
        LogProfileRows("engine-time", profile.Samples
            .Where(row => !row.EntitySystem)
            .OrderByDescending(row => row.TotalSeconds)
            .Take(Math.Min(top, 5)));
        LogProfileRows("engine-allocation", profile.Samples
            .Where(row => !row.EntitySystem)
            .OrderByDescending(row => row.TotalAllocatedBytes)
            .Take(Math.Min(top, 5)));

        foreach (CMUPerformanceProfileCounter counter in profile.Counters
                     .OrderByDescending(row => row.Total)
                     .Take(Math.Min(top, 10)))
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] profile-counter incidentId={_activeIncidentId} name={SanitizeName(counter.Name)} ",
                $"samples={counter.Count} total={counter.Total} max={counter.Max} last={counter.Last}"));
        }
    }

    private void LogProfileRows(string category, IEnumerable<CMUPerformanceProfileSample> rows)
    {
        foreach (CMUPerformanceProfileSample row in rows)
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] profile-sample incidentId={_activeIncidentId} category={category} ",
                $"kind={row.Kind} name={SanitizeName(row.Name)} calls={row.Count} ",
                $"totalMs={row.TotalSeconds * 1000:F3} avgMs={row.AverageSeconds * 1000:F3} ",
                $"maxMs={row.MaxSeconds * 1000:F3} totalAllocatedBytes={row.TotalAllocatedBytes} ",
                $"maxAllocatedBytes={row.MaxAllocatedBytes}"));
        }
    }

    private void LogChurnRows(string category, IReadOnlyList<CMUPerformanceChurnRow> rows)
    {
        foreach (CMUPerformanceChurnRow row in rows)
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] ecs-churn incidentId={_activeIncidentId} category={category} name={SanitizeName(row.Name)} ",
                $"createdOrAdded={row.CreatedOrAdded} deletedOrRemoved={row.DeletedOrRemoved} ",
                $"net={row.Net} current={row.Current}"));
        }
    }

    private string BuildObservationLine(
        string kind,
        in CMUServerPerformanceObservation observation,
        long incidentId,
        CMUPerformanceIncidentReason reasons,
        string severity)
    {
        return Invariant(
            $"[CMU-PERF] {kind} incidentId={incidentId} severity={severity} ",
            $"reasons={CMUPerformanceIncidentDetector.FormatReasons(reasons)} tick={observation.Tick} ",
            $"round={observation.RoundId} roundState={observation.RoundState} roundSeconds={observation.RoundSeconds:F1} ",
            $"paused={observation.Paused} warmup={observation.SuppressRateTriggers} ",
            $"targetTps={observation.TargetTps:F2} achievedTps={observation.AchievedTps:F2} tpsValid={observation.TpsValid} ",
            $"fps={observation.AverageFps:F2} frameMs={observation.FrameMilliseconds:F2} ",
            $"frameAvgMs={observation.FrameAverageMilliseconds:F2} frameStdDevMs={observation.FrameStdDevMilliseconds:F2} ",
            $"backlogMs={observation.BacklogMilliseconds:F2} entities={observation.EntityCount} ",
            $"entityGrowthPerMinute={observation.EntityGrowthPerMinute:F1} entityChurnPerMinute={observation.EntityChurnPerMinute:F1} ",
            $"components={observation.ComponentCount} componentGrowthPerMinute={observation.ComponentGrowthPerMinute:F1} ",
            $"componentChurnPerMinute={observation.ComponentChurnPerMinute:F1} dirtyEntitiesPerSecond={observation.EntityDirtiesPerSecond:F1} ",
            $"players={observation.Players} attachedPlayers={observation.AttachedPlayers} channels={observation.Channels} ",
            $"sendBytesPerSecond={observation.SendBytesPerSecond:F0} receiveBytesPerSecond={observation.ReceiveBytesPerSecond:F0} ",
            $"sentPacketsTotal={observation.SentPacketsTotal} receivedPacketsTotal={observation.ReceivedPacketsTotal} ",
            $"profileFrame={observation.ProfileFrame?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} ",
            $"allocatedBytes={observation.AllocatedBytes} profiler={_profiler.IsEnabled} ",
            $"metrics={_config.GetCVar(CVars.MetricsEnabled)}");
    }

    private IReadOnlyList<CMUNetworkMessageRate> CaptureMessageRates(double elapsed)
    {
        var current = new Dictionary<Type, long>(_netManager.MessageBandwidthUsage);
        var rows = new List<CMUNetworkMessageRate>();
        foreach (var (type, bytes) in current)
        {
            _lastMessageBandwidth.TryGetValue(type, out long previous);
            long delta = bytes >= previous ? bytes - previous : 0;
            if (delta <= 0)
                continue;

            rows.Add(new(type.FullName ?? type.Name, delta / elapsed));
        }

        _lastMessageBandwidth = current;
        return rows
            .OrderByDescending(row => row.BytesPerSecond)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private bool ProbeProfiler(TimeSpan now)
    {
        if (now < _nextProfilerProbeTime)
            return _allocationBreachPending;

        _nextProfilerProbeTime = now + TimeSpan.FromSeconds(ProfilerProbeSeconds);
        if (!CMUPerformanceProfilerReader.TryGetFrameWindow(
                _profiler,
                _profileIndexOffset,
                out CMUPerformanceProfileFrame frame,
                out long nextIndexOffset))
        {
            _profileIndexOffset = nextIndexOffset;
            return _allocationBreachPending;
        }

        _profileIndexOffset = nextIndexOffset;
        if (_maxAllocatedFrameSinceSample is not { } current ||
            frame.AllocatedBytes >= current.AllocatedBytes)
        {
            _maxAllocatedFrameSinceSample = frame;
        }

        double threshold = Math.Max(0,
            _config.GetCVar(CCVars.CMUServerPerformanceAllocationMiBPerFrame)) * BytesPerMiB;
        _allocationBreachPending = threshold > 0 &&
                                   _maxAllocatedFrameSinceSample?.AllocatedBytes >= threshold;
        return _allocationBreachPending;
    }

    private void ResetTracking(string reason)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var prototypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (EntityUid uid in _entityManager.GetEntities())
        {
            if (!_entityManager.TryGetComponent(uid, out MetaDataComponent? metadata))
                continue;

            string prototype = metadata.EntityPrototype?.ID ?? "<none>";
            Increment(prototypeCounts, prototype);
        }

        var componentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        int componentCount = 0;
        foreach (ComponentRegistration registration in _entityManager.ComponentFactory.GetAllRegistrations())
        {
            int count = _entityManager.Count(registration.Type);
            if (count <= 0)
                continue;

            componentCounts[registration.Name] = count;
            componentCount += count;
        }

        _churn.Reset(componentCount, prototypeCounts, componentCounts);
        ResetWindowsAndBaselines();
        stopwatch.Stop();
        _sawmill.Info(Invariant(
            $"[CMU-PERF] tracking-reset reason={reason} entities={_entityManager.EntityCount} ",
            $"components={componentCount} prototypes={prototypeCounts.Count} durationMs={stopwatch.Elapsed.TotalMilliseconds:F2}"));
    }

    private void ResetEpoch(string reason)
    {
        CMUPerformanceChurnSnapshot current = _churn.Snapshot();
        _churn.Reset(current.ComponentCount, current.PrototypeCounts, current.ComponentCounts);
        ResetWindowsAndBaselines();
        _sawmill.Info(Invariant(
            $"[CMU-PERF] epoch-reset reason={reason} round={_ticker?.RoundId ?? 0} ",
            $"entities={_entityManager.EntityCount} components={current.ComponentCount}"));
    }

    private void ResetWindowsAndBaselines()
    {
        TimeSpan now = _timing.RealTime;
        _shortRates.Reset();
        _churnRates.Reset();
        _detector.Reset();
        _churnBaseline = _churn.Snapshot();
        _lastMessageBandwidth = new(_netManager.MessageBandwidthUsage);
        _haveNetworkStats = false;
        _lastSampleTime = default;
        _profileIndexOffset = _profiler.Buffer.IndexWriteOffset;
        _maxAllocatedFrameSinceSample = null;
        _allocationBreachPending = false;
        _nextProfilerProbeTime = now;
        double warmupSeconds = Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceWarmup));
        _warmupUntil = now + TimeSpan.FromSeconds(warmupSeconds);
        _warmupBaselinePending = warmupSeconds > 0;
        _nextSampleTime = now;
        _nextHeartbeatTime = now;
        _nextBaselineTime = now + TimeSpan.FromSeconds(
            Math.Max(5, _config.GetCVar(CCVars.CMUServerPerformanceBaselineInterval)));
        _activeIncidentId = 0;
        _metricIncidentActive = 0;
    }

    private void CompleteWarmupBaseline()
    {
        CMUPerformanceChurnSnapshot current = _churn.Snapshot();
        _churn.Reset(current.ComponentCount, current.PrototypeCounts, current.ComponentCounts);
        _churnBaseline = _churn.Snapshot();
        _shortRates.Reset();
        _churnRates.Reset();
        _warmupBaselinePending = false;
        _sawmill.Info(Invariant(
            $"[CMU-PERF] warmup-complete tick={_timing.CurTick.Value} round={_ticker?.RoundId ?? 0} ",
            $"entities={_entityManager.EntityCount} components={current.ComponentCount}"));
    }

    private void SetEnabled(bool enabled)
    {
        if (!_initialized || enabled == _enabled)
            return;

        _enabled = enabled;
        _metricEnabled = enabled ? 1 : 0;
        if (enabled)
        {
            HookEntityEvents();
            ResetTracking("enabled");
            EnableProfilerIfConfigured();
            _sawmill.Info("[CMU-PERF] diagnostics-enabled");
        }
        else
        {
            UnhookEntityEvents();
            _detector.Reset();
            _metricIncidentActive = 0;
            if (_profilerEnabledByDiagnostics && _profiler.IsEnabled)
                SetProfilerCVar(false);
            _profilerEnabledByDiagnostics = false;
            _sawmill.Info("[CMU-PERF] diagnostics-disabled");
        }
    }

    private void HookEntityEvents()
    {
        if (_eventsHooked)
            return;

        _entityManager.EntityInitialized += OnEntityInitialized;
        _entityManager.EntityDeleted += OnEntityDeleted;
        _entityManager.EntityDirtied += OnEntityDirtied;
        _entityManager.ComponentAdded += OnComponentAdded;
        _entityManager.ComponentRemoved += OnComponentRemoved;
        _entityManager.AfterEntityFlush += OnAfterEntityFlush;
        _eventsHooked = true;
    }

    private void UnhookEntityEvents()
    {
        if (!_eventsHooked)
            return;

        _entityManager.EntityInitialized -= OnEntityInitialized;
        _entityManager.EntityDeleted -= OnEntityDeleted;
        _entityManager.EntityDirtied -= OnEntityDirtied;
        _entityManager.ComponentAdded -= OnComponentAdded;
        _entityManager.ComponentRemoved -= OnComponentRemoved;
        _entityManager.AfterEntityFlush -= OnAfterEntityFlush;
        _eventsHooked = false;
    }

    private void EnableProfilerIfConfigured()
    {
        if (!_config.GetCVar(CCVars.CMUServerPerformanceEnableProfiler) || _profiler.IsEnabled)
            return;

        SetProfilerCVar(true);
        _profilerEnabledByDiagnostics = true;
        _sawmill.Info("[CMU-PERF] profiler-enabled source=automatic-diagnostics");
    }

    private void SetProfilerCVar(bool enabled)
    {
        _changingProfilerCVar = true;
        try
        {
            _config.SetCVar(CVars.ProfEnabled, enabled);
        }
        finally
        {
            _changingProfilerCVar = false;
        }
    }

    private void OnProfilerEnabledChanged(bool enabled)
    {
        if (!_changingProfilerCVar)
            _profilerEnabledByDiagnostics = false;
    }

    private void LogStartupState()
    {
        bool metrics = _config.GetCVar(CVars.MetricsEnabled);
        _sawmill.Info(Invariant(
            $"[CMU-PERF] startup enabled={_enabled} profiler={_profiler.IsEnabled} ",
            $"profilerEnabledByDiagnostics={_profilerEnabledByDiagnostics} metrics={metrics} ",
            $"sampleSeconds={Math.Max(0.1, _config.GetCVar(CCVars.CMUServerPerformanceSampleInterval)):F2} ",
            $"stallMs={Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceStallMilliseconds)):F1} ",
            $"criticalStallMs={Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceCriticalStallMilliseconds)):F1}"));

        bool runtimeMetrics = _config.GetCVar(CVars.MetricsRuntime);
        if (metrics && runtimeMetrics)
        {
            _sawmill.Info(Invariant(
                $"[CMU-PERF] runtime-metrics endpoint=http://{_config.GetCVar(CVars.MetricsHost)}:",
                $"{_config.GetCVar(CVars.MetricsPort)}/metrics runtime={runtimeMetrics}"));
        }
        else
        {
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] runtime-metrics-disabled metrics={metrics} runtime={runtimeMetrics} ",
                $"retainedHeapRssGcThreadPoolUnavailable=true action=enable-and-scrape-runtime-metrics"));
        }
    }

    private CMUPerformanceIncidentThresholds ReadThresholds()
    {
        return new(
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceStallMilliseconds)),
            Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceLowTpsRatio), 0, 1),
            Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceLowFpsRatio), 0, 1),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceBreachDuration)),
            Math.Clamp(_config.GetCVar(CCVars.CMUServerPerformanceRecoveryRatio), 0, 1),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceRecoveryDuration)),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceEntityGrowthPerMinute)),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceEntityChurnPerMinute)),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceComponentGrowthPerMinute)),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceComponentChurnPerMinute)),
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceSendMiBPerSecond)) * BytesPerMiB,
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceReceiveMiBPerSecond)) * BytesPerMiB,
            Math.Max(0, _config.GetCVar(CCVars.CMUServerPerformanceAllocationMiBPerFrame)) * BytesPerMiB);
    }

    private TimeSpan GetIncidentUpdateInterval()
    {
        return TimeSpan.FromSeconds(Math.Max(1,
            _config.GetCVar(CCVars.CMUServerPerformanceIncidentUpdateInterval)));
    }

    private double GetTargetTps()
    {
        double seconds = _timing.CalcAdjustedTickPeriod().TotalSeconds;
        return seconds > 0 ? 1 / seconds : _timing.TickRate;
    }

    private string GetSeverity(in CMUServerPerformanceObservation observation)
    {
        double criticalStall = Math.Max(0,
            _config.GetCVar(CCVars.CMUServerPerformanceCriticalStallMilliseconds));
        double allocationThreshold = Math.Max(0,
            _config.GetCVar(CCVars.CMUServerPerformanceAllocationMiBPerFrame)) * BytesPerMiB;

        if ((criticalStall > 0 && observation.FrameMilliseconds >= criticalStall) ||
            (observation.TpsValid && observation.TargetTps > 0 &&
             observation.AchievedTps / observation.TargetTps < 0.5) ||
            (allocationThreshold > 0 && observation.AllocatedBytes >= allocationThreshold * 4))
            return "critical";

        return "warning";
    }

    private void ResetWorst(in CMUServerPerformanceObservation observation)
    {
        _worstFrameMilliseconds = observation.FrameMilliseconds;
        _worstTps = observation.TpsValid ? observation.AchievedTps : double.PositiveInfinity;
        _worstFps = observation.AverageFps;
        _worstAllocatedBytes = observation.AllocatedBytes;
    }

    private void UpdateWorst(in CMUServerPerformanceObservation observation)
    {
        _worstFrameMilliseconds = Math.Max(_worstFrameMilliseconds, observation.FrameMilliseconds);
        if (observation.TpsValid)
            _worstTps = Math.Min(_worstTps, observation.AchievedTps);
        _worstFps = Math.Min(_worstFps, observation.AverageFps);
        _worstAllocatedBytes = Math.Max(_worstAllocatedBytes, observation.AllocatedBytes);
    }

    private void InitializeMetrics()
    {
        _meter = _meterFactory.Create("CMU.ServerPerformance");
        _meter.CreateObservableGauge("cmu_server_performance_enabled", () => _metricEnabled);
        _meter.CreateObservableGauge("cmu_server_performance_tps", () => _metricTps, "ticks/s");
        _meter.CreateObservableGauge("cmu_server_performance_target_tps", () => _metricTargetTps, "ticks/s");
        _meter.CreateObservableGauge("cmu_server_performance_fps", () => _metricFps, "frames/s");
        _meter.CreateObservableGauge("cmu_server_performance_frame_seconds", () => _metricFrameSeconds, "s");
        _meter.CreateObservableGauge("cmu_server_performance_frame_average_seconds", () => _metricFrameAverageSeconds, "s");
        _meter.CreateObservableGauge("cmu_server_performance_frame_stddev_seconds", () => _metricFrameStdDevSeconds, "s");
        _meter.CreateObservableGauge("cmu_server_performance_tick_backlog_seconds", () => _metricBacklogSeconds, "s");
        _meter.CreateObservableGauge("cmu_server_performance_entities", () => _metricEntities);
        _meter.CreateObservableGauge("cmu_server_performance_components", () => _metricComponents);
        _meter.CreateObservableGauge("cmu_server_performance_entity_growth_per_minute", () => _metricEntityGrowthPerMinute);
        _meter.CreateObservableGauge("cmu_server_performance_entity_churn_per_minute", () => _metricEntityChurnPerMinute);
        _meter.CreateObservableGauge("cmu_server_performance_component_growth_per_minute", () => _metricComponentGrowthPerMinute);
        _meter.CreateObservableGauge("cmu_server_performance_component_churn_per_minute", () => _metricComponentChurnPerMinute);
        _meter.CreateObservableGauge("cmu_server_performance_entity_dirties_per_second", () => _metricEntityDirtiesPerSecond);
        _meter.CreateObservableGauge("cmu_server_performance_send_bytes_per_second", () => _metricSendBytesPerSecond, "By/s");
        _meter.CreateObservableGauge("cmu_server_performance_receive_bytes_per_second", () => _metricReceiveBytesPerSecond, "By/s");
        _meter.CreateObservableGauge("cmu_server_performance_profile_max_frame_allocated_bytes", () => _metricAllocatedBytes, "By");
        _meter.CreateObservableGauge("cmu_server_performance_incident_active", () => _metricIncidentActive);
        _meter.CreateObservableGauge("cmu_server_performance_last_completed_tick", () => _metricLastCompletedTick);
        _meter.CreateObservableGauge("cmu_server_performance_last_update_unix_seconds", () => _metricLastUpdateUnixSeconds, "s");

        _incidentCounter = _meter.CreateCounter<long>("cmu_server_performance_incidents_total");
        _entityCreateCounter = _meter.CreateCounter<long>("cmu_server_performance_entity_creates_total");
        _entityDeleteCounter = _meter.CreateCounter<long>("cmu_server_performance_entity_deletes_total");
        _componentAddCounter = _meter.CreateCounter<long>("cmu_server_performance_component_adds_total");
        _componentRemoveCounter = _meter.CreateCounter<long>("cmu_server_performance_component_removes_total");
    }

    private void UpdateMetrics(in CMUServerPerformanceObservation observation)
    {
        _metricTps = observation.AchievedTps;
        _metricTargetTps = observation.TargetTps;
        _metricFps = observation.AverageFps;
        _metricFrameSeconds = observation.FrameMilliseconds / 1000;
        _metricFrameAverageSeconds = observation.FrameAverageMilliseconds / 1000;
        _metricFrameStdDevSeconds = observation.FrameStdDevMilliseconds / 1000;
        _metricBacklogSeconds = observation.BacklogMilliseconds / 1000;
        _metricEntities = observation.EntityCount;
        _metricComponents = observation.ComponentCount;
        _metricEntityGrowthPerMinute = observation.EntityGrowthPerMinute;
        _metricEntityChurnPerMinute = observation.EntityChurnPerMinute;
        _metricComponentGrowthPerMinute = observation.ComponentGrowthPerMinute;
        _metricComponentChurnPerMinute = observation.ComponentChurnPerMinute;
        _metricEntityDirtiesPerSecond = observation.EntityDirtiesPerSecond;
        _metricSendBytesPerSecond = observation.SendBytesPerSecond;
        _metricReceiveBytesPerSecond = observation.ReceiveBytesPerSecond;
        _metricIncidentActive = _detector.Active ? 1 : 0;
        _metricLastCompletedTick = observation.Tick;
        _metricLastUpdateUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000d;
    }

    private void OnEntityInitialized(Entity<MetaDataComponent> entity)
    {
        if (!_enabled)
            return;

        string prototype = entity.Comp.EntityPrototype?.ID ?? "<none>";
        string map = _entityManager.TryGetComponent(entity.Owner, out TransformComponent? transform)
            ? transform.MapID.ToString()
            : "<none>";
        _churn.EntityCreated(prototype, map);
        _entityCreateCounter?.Add(1);
    }

    private void OnEntityDeleted(Entity<MetaDataComponent> entity)
    {
        if (!_enabled)
            return;

        _churn.EntityDeleted(entity.Comp.EntityPrototype?.ID ?? "<none>");
        _entityDeleteCounter?.Add(1);
    }

    private void OnEntityDirtied(Entity<MetaDataComponent> entity)
    {
        if (!_enabled)
            return;

        _churn.EntityDirtied();
    }

    private void OnComponentAdded(AddedComponentEventArgs args)
    {
        if (!_enabled)
            return;

        _churn.ComponentAdded(args.ComponentType.Name);
        _componentAddCounter?.Add(1);
    }

    private void OnComponentRemoved(RemovedComponentEventArgs args)
    {
        if (!_enabled)
            return;

        string name = _entityManager.ComponentFactory.GetRegistration(args.Idx).Name;
        _churn.ComponentRemoved(name);
        _componentRemoveCounter?.Add(1);
    }

    private void OnAfterEntityFlush()
    {
        if (_enabled)
            _needsFullReset = true;
    }

    private static void Increment(Dictionary<string, int> values, string key)
    {
        values.TryGetValue(key, out int count);
        values[key] = count + 1;
    }

    private static long NonNegativeDelta(long current, long previous)
    {
        return current >= previous ? current - previous : 0;
    }

    private static double FiniteOrZero(double value)
    {
        return double.IsFinite(value) ? value : 0;
    }

    private static string SanitizeName(string name)
    {
        return name.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_');
    }

    internal static string Invariant(params FormattableString[] values)
    {
        var builder = new StringBuilder();
        foreach (FormattableString value in values)
        {
            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private readonly record struct CMUNetworkMessageRate(string Name, double BytesPerSecond);

    private sealed record CMUServerPerformanceObservation(
        TimeSpan RealTime,
        uint Tick,
        double TargetTps,
        double AchievedTps,
        bool TpsValid,
        double AverageFps,
        double FrameMilliseconds,
        double FrameAverageMilliseconds,
        double FrameStdDevMilliseconds,
        double BacklogMilliseconds,
        int EntityCount,
        int ComponentCount,
        int Players,
        int AttachedPlayers,
        int Channels,
        double EntityGrowthPerMinute,
        double EntityChurnPerMinute,
        double ComponentGrowthPerMinute,
        double ComponentChurnPerMinute,
        bool ChurnValid,
        double EntityDirtiesPerSecond,
        double SendBytesPerSecond,
        double ReceiveBytesPerSecond,
        long SentPacketsTotal,
        long ReceivedPacketsTotal,
        long AllocatedBytes,
        long? ProfileFrame,
        bool SuppressRateTriggers,
        bool Paused,
        int RoundId,
        string RoundState,
        double RoundSeconds,
        IReadOnlyList<CMUNetworkMessageRate> MessageRates);
}
