using System.Text;
using System.Linq;
using Content.Server.CMU14.Diagnostics.Performance;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.CMU14.Diagnostics;
using Content.Shared.GameTicking;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Diagnostics;

/// <summary>
/// Correlates the state-request/ACK stream with bounded client-reported application progress.
/// Diagnostic evidence never changes PVS, and receipt ACKs do not prove successful application/rendering.
/// </summary>
public sealed class CMUClientStateDiagnosticsSystem : EntitySystem
{
    public const string SawmillName = "cmu.client_state";
    private const int MaxDetailsPerWindow = 8;
    private const int MaxSummarySamples = 8;
    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(30);

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IServerGameStateManager _gameStates = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ICMUServerPerformanceDiagnostics _performance = default!;

    private readonly Dictionary<ICommonSession, ClientTrace> _clients = new();
    private readonly CMURecentServerErrors _errors = new();
    private ISawmill _sawmill = default!;
    private bool _enabled;
    private TimeSpan _windowStart;
    private TimeSpan _nextSummary;
    private TimeSpan _nextDetailsWindow;
    private TimeSpan _nextErrorReport;
    private TimeSpan? _lastCleanup;
    private GameTick? _cleanupTick;
    private int _details;
    private long _requests;
    private long _suppressed;
    private int _disconnected;
    private long _lastErrorId;
    private long _initialRequests;
    private long _recoveryRequests;
    private long _syncSequence;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill(SawmillName);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
        SubscribeNetworkEvent<CMUClientStateHealthEvent>(OnHealthReport);
        Subs.CVar(_cfg, CCVars.CMUClientStateDiagnosticsEnabled, SetEnabled, true);
    }

    public override void Shutdown()
    {
        SetEnabled(false);
        base.Shutdown();
    }

    private void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        if (enabled)
        {
            _windowStart = _timing.RealTime;
            _nextSummary = _windowStart + ReportInterval;
            _nextDetailsWindow = _windowStart + ReportInterval;
            _nextErrorReport = TimeSpan.Zero;
            _gameStates.ClientRequestFull += OnClientRequestFull;
            _gameStates.ClientAck += OnClientAck;
            _players.PlayerStatusChanged += OnPlayerStatusChanged;
            _logManager.RootSawmill.AddHandler(_errors);
        }
        else
        {
            _gameStates.ClientRequestFull -= OnClientRequestFull;
            _gameStates.ClientAck -= OnClientAck;
            _players.PlayerStatusChanged -= OnPlayerStatusChanged;
            _logManager.RootSawmill.RemoveHandler(_errors);
            _clients.Clear();
            _errors.Clear();
            _details = 0;
            _requests = 0;
            _suppressed = 0;
            _disconnected = 0;
            _initialRequests = 0;
            _recoveryRequests = 0;
        }
    }

    public override void Update(float frameTime)
    {
        // One bounded pass over connected sessions every 30 seconds; never enumerate entities or components.
        if (_enabled && _timing.RealTime >= _nextSummary)
            WriteSummary(_timing.RealTime, "interval");
    }

    private ClientTrace GetTrace(ICommonSession session)
    {
        if (_clients.TryGetValue(session, out var trace))
            return trace;

        trace = new ClientTrace();
        _clients.Add(session, trace);
        return trace;
    }

    private void OnClientAck(ICommonSession session, GameTick tick)
    {
        // Ignore duplicate/out-of-order/future ACKs when measuring observed receipt progress.
        if (tick == GameTick.Zero || tick >= _timing.CurTick)
            return;

        var trace = GetTrace(session);
        if (trace.LastAck is { } last && tick <= last)
            return;

        trace.LastAck = tick;
        trace.LastAckAt = _timing.RealTime;
    }

    private void OnClientRequestFull(ICommonSession session, GameTick tick, NetEntity? missingEntity)
    {
        var now = _timing.RealTime;
        if (now >= _nextSummary)
            WriteSummary(now, "interval");

        var trace = GetTrace(session);
        if (trace.Requests == 0)
        {
            trace.FirstRequestAt = now;
            trace.FirstRequestedTick = tick;
        }

        trace.SameTickRequests = trace.Requests > 0 && trace.RequestedTick == tick ? trace.SameTickRequests + 1 : 1;
        trace.Requests++;
        trace.WindowRequests++;
        trace.LastRequestAt = now;
        trace.LastMissingEntity = missingEntity;
        if (tick == GameTick.Zero)
        {
            _initialRequests++;
            trace.WindowInitialRequests++;
        }
        else
        {
            _recoveryRequests++;
            trace.WindowRecoveryRequests++;
        }
        trace.RequestedTick = tick;
        trace.AckAtRequest = trace.LastAck;
        _requests++;
        if (trace.WindowRecoveryRequests >= 3)
            StartSyncIncident(session, trace, "repeated-full-state-requests", now);

        if (now < trace.NextDetail || !TryTakeDetail(now))
        {
            trace.Suppressed++;
            _suppressed++;
            return;
        }

        trace.NextDetail = now + ReportInterval;
        _sawmill.Warning(
            $"full-state-request user={session.UserId} name=\"{SafeName(session.Name)}\" status={session.Status} " +
            $"round={_ticker.RoundId} phase={_ticker.RunLevel} serverTick={_timing.CurTick} requestedTick={tick} " +
            $"requestKind={(tick == GameTick.Zero ? "initial-or-manual" : "recovery")} syncIncidentId={trace.SyncIncidentId} " +
            $"firstRequestedTick={trace.FirstRequestedTick} requests={trace.Requests} sameTickRequests={trace.SameTickRequests} " +
            $"suppressedDetails={trace.Suppressed} sinceFirstRequestSeconds={Age(now, trace.FirstRequestAt)} " +
            $"lastReceivedAck={trace.LastAck?.ToString() ?? "unknown"} ackAgeSeconds={Age(now, trace.LastAckAt)} " +
            $"pingMs={session.Ping} attached=[{DescribeEntity(session.AttachedEntity)}] " +
            $"missingNetEntity={missingEntity?.ToString() ?? "none"} missing=[{DescribeEntity(GetEntity(missingEntity))}] " +
            $"cleanupTick={_cleanupTick?.ToString() ?? "none"} cleanupAgeSeconds={Age(now, _lastCleanup)} " +
            $"{DescribeHealth(trace, now)} {_performance.GetCorrelationContext()}");
        trace.Suppressed = 0;
        WriteRecentErrors(now);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected || !_clients.Remove(args.Session, out var trace))
            return;

        if ((trace.Requests == 0 && trace.SyncIncidentId == 0) ||
            trace.SyncIncidentId == 0 && _timing.RealTime - trace.LastRequestAt > TimeSpan.FromMinutes(1))
            return;

        _disconnected++;
        // The normal disconnect log already identifies the player. Keep this detail in the same global budget.
        if (TryTakeDetail(_timing.RealTime))
        {
            _sawmill.Warning(
                $"disconnect-after-state-request user={args.Session.UserId} round={_ticker.RoundId} " +
                $"syncIncidentId={trace.SyncIncidentId} secondsSinceLastRequest={Age(_timing.RealTime, trace.LastRequestAt)} " +
                $"serverTick={_timing.CurTick} requests={trace.Requests} requestedTick={trace.RequestedTick} " +
                $"lastReceivedAck={trace.LastAck?.ToString() ?? "unknown"} {DescribeHealth(trace, _timing.RealTime)} " +
                _performance.GetCorrelationContext());
        }
    }

    private bool TryTakeDetail(TimeSpan now)
    {
        if (now >= _nextDetailsWindow)
        {
            _details = 0;
            _nextDetailsWindow = now + ReportInterval;
        }

        if (_details >= MaxDetailsPerWindow)
            return false;

        _details++;
        return true;
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent ev)
    {
        _lastCleanup = _timing.RealTime;
        _cleanupTick = _timing.CurTick;
        if (!_enabled)
            return;

        WriteSummary(_timing.RealTime, "round-cleanup");
        _sawmill.Info($"round-cleanup round={_ticker.RoundId} serverTick={_cleanupTick}");
        // Keep connection traces: the failure we are investigating can persist across cleanup.
    }

    private void WriteSummary(TimeSpan now, string reason)
    {
        var affected = 0;
        var repeated = 0;
        var ackAdvanced = 0;
        var activeSync = 0;
        var samples = new StringBuilder();
        foreach (var (session, trace) in _clients.OrderByDescending(pair => pair.Value.WindowRecoveryRequests))
        {
            TryCloseSyncIncident(session, trace, now);
            if (trace.SyncIncidentId != 0)
                activeSync++;
            if (trace.WindowRequests == 0 && trace.SyncIncidentId == 0)
                continue;

            affected++;
            if (trace.WindowRequests > 1)
                repeated++;
            if (trace.LastAck is { } ack && (trace.AckAtRequest == null || ack > trace.AckAtRequest.Value))
                ackAdvanced++;
            if (affected <= MaxSummarySamples)
            {
                samples.Append($" user={session.UserId}/tick={trace.RequestedTick}/requests={trace.WindowRequests}" +
                               $"/recoveryRequests={trace.WindowRecoveryRequests}/syncIncidentId={trace.SyncIncidentId}" +
                               $"/missingNetEntity={trace.LastMissingEntity?.ToString() ?? "none"}" +
                               $"/ack={trace.LastAck?.ToString() ?? "unknown"}/ackLagTicks={AckLag(trace)}" +
                               $"/ackAgeSeconds={Age(now, trace.LastAckAt)} [{DescribeHealth(trace, now)}]");
            }

            trace.WindowRequests = 0;
            trace.WindowInitialRequests = 0;
            trace.WindowRecoveryRequests = 0;
        }

        if (_requests > 0 || _disconnected > 0 || activeSync > 0)
        {
            _sawmill.Warning(
                $"state-request-summary reason={reason} round={_ticker.RoundId} phase={_ticker.RunLevel} " +
                $"serverTick={_timing.CurTick} windowSeconds={Age(now, _windowStart)} " +
                $"requests={_requests} affectedConnectedClients={affected} repeatedClients={repeated} " +
                $"initialOrManualRequests={_initialRequests} recoveryRequests={_recoveryRequests} activeSyncClients={activeSync} " +
                $"ackAdvancedAfterLastRequestClients={ackAdvanced} disconnectedAfterRequest={_disconnected} " +
                $"suppressedDetails={_suppressed} cleanupTick={_cleanupTick?.ToString() ?? "none"} " +
                $"cleanupAgeSeconds={Age(now, _lastCleanup)} samples=[{samples}] " +
                _performance.GetCorrelationContext());
            WriteRecentErrors(now);
        }

        _requests = 0;
        _suppressed = 0;
        _disconnected = 0;
        _initialRequests = 0;
        _recoveryRequests = 0;
        _windowStart = now;
        _nextSummary = now + ReportInterval;
    }

    private void OnHealthReport(CMUClientStateHealthEvent msg, EntitySessionEventArgs args) =>
        ObserveHealth(args.SenderSession, msg);

    internal void ObserveHealth(ICommonSession session, CMUClientStateHealthEvent msg)
    {
        if (!_enabled || !_cfg.GetCVar(CCVars.CMUClientStateHealthEnabled) ||
            msg.AppliedTick > _timing.CurTick || !double.IsFinite(msg.AppliedAgeSeconds) ||
            !double.IsFinite(msg.AverageFps) || msg.AppliedAgeSeconds < -1 || msg.AppliedAgeSeconds > 86400 ||
            msg.AppliedTick != GameTick.Zero && msg.AppliedAgeSeconds < 0 ||
            msg.AverageFps < -1 || msg.AverageFps > 10000 ||
            msg.BufferedStates < 0 || msg.BufferedStates > 65536 || msg.TargetBuffer < 0 || msg.TargetBuffer > 65536)
            return;

        var now = _timing.RealTime;
        var trace = GetTrace(session);
        if (trace.HealthAt is { } last && now - last < TimeSpan.FromSeconds(2))
            return;
        trace.HealthAt = now;
        // Retain scalar values rather than a mutable message object supplied by the caller.
        trace.Health = new CMUClientStateHealthEvent
        {
            AppliedTick = msg.AppliedTick, AppliedAgeSeconds = msg.AppliedAgeSeconds,
            BufferedStates = msg.BufferedStates, TargetBuffer = msg.TargetBuffer, AverageFps = msg.AverageFps,
        };
        if (_ticker.RunLevel == GameRunLevel.InRound && !_timing.Paused &&
            msg.AppliedTick != GameTick.Zero && msg.AppliedAgeSeconds >= 10 &&
            _timing.CurTick.Value - msg.AppliedTick.Value >= _timing.TickRate * 5)
        {
            StartSyncIncident(session, trace, "client-reported-application-stall", now);
        }
    }

    private void StartSyncIncident(ICommonSession session, ClientTrace trace, string reason, TimeSpan now)
    {
        if (trace.SyncIncidentId != 0)
            return;
        trace.SyncIncidentId = ++_syncSequence;
        trace.SyncStartedAt = now;
        // A sync report gets its own bounded capture opportunity even when server TPS is healthy.
        _performance.RequestSyncReport(trace.SyncIncidentId);
        if (!TryTakeDetail(now))
            return;
        _sawmill.Warning(
            $"sync-incident-open syncIncidentId={trace.SyncIncidentId} reason={reason} user={session.UserId} " +
            $"round={_ticker.RoundId} phase={_ticker.RunLevel} serverTick={_timing.CurTick} " +
            $"requestedTick={trace.RequestedTick} windowRecoveryRequests={trace.WindowRecoveryRequests} " +
            $"sameTickRequests={trace.SameTickRequests} missingNetEntity={trace.LastMissingEntity?.ToString() ?? "none"} " +
            $"missing=[{DescribeEntity(GetEntity(trace.LastMissingEntity))}] " +
            $"lastReceivedAck={trace.LastAck?.ToString() ?? "unknown"} ackLagTicks={AckLag(trace)} " +
            $"{DescribeHealth(trace, now)} {_performance.GetCorrelationContext()}");
        WriteRecentErrors(now);
    }

    private void TryCloseSyncIncident(ICommonSession session, ClientTrace trace, TimeSpan now)
    {
        if (trace.SyncIncidentId == 0 || now - trace.LastRequestAt < ReportInterval)
            return;
        bool applied = trace.Health is { } health && trace.HealthAt is { } healthAt &&
                       now - healthAt < TimeSpan.FromSeconds(10) && health.AppliedTick > trace.RequestedTick &&
                       health.AppliedAgeSeconds is >= 0 and < 5;
        bool received = trace.LastAck is { } ack && ack > trace.RequestedTick && trace.LastAckAt is { } ackAt &&
                        now - ackAt < TimeSpan.FromSeconds(5) && now - trace.LastRequestAt >= TimeSpan.FromMinutes(1);
        // Fresh stalled application telemetry takes precedence over receipt ACKs.
        if (!applied && (!received || trace.HealthAt is { } at && now - at < TimeSpan.FromSeconds(10)))
            return;
        if (TryTakeDetail(now))
        {
            _sawmill.Warning(
                $"sync-incident-close syncIncidentId={trace.SyncIncidentId} user={session.UserId} " +
                $"outcome={(applied ? "client-reported-application-progress" : "requests-stopped-receipt-progress-only")} " +
                $"durationSeconds={Age(now, trace.SyncStartedAt)} {DescribeHealth(trace, now)} " +
                _performance.GetCorrelationContext());
        }
        trace.SyncIncidentId = 0;
    }

    private long AckLag(ClientTrace trace) => trace.LastAck is { } ack
        ? Math.Max(0, (long)_timing.CurTick.Value - ack.Value) : -1;

    private string DescribeHealth(ClientTrace trace, TimeSpan now)
    {
        if (trace.Health is not { } health)
            return "clientAppliedState=unknown";
        return CMUServerPerformanceDiagnosticsManager.Invariant(
            $"clientAppliedState=client-reported clientAppliedTick={health.AppliedTick} ",
            $"clientAppliedAgeSeconds={health.AppliedAgeSeconds:F2} clientAppliedLagTicks={Math.Max(0, (long)_timing.CurTick.Value - health.AppliedTick.Value)} ",
            $"healthReportAgeSeconds={Age(now, trace.HealthAt)} bufferedStates={health.BufferedStates} ",
            $"targetBuffer={health.TargetBuffer} clientAvgFps={health.AverageFps:F2}");
    }

    private void WriteRecentErrors(TimeSpan now)
    {
        if (now < _nextErrorReport)
            return;

        _nextErrorReport = now + ReportInterval;
        foreach (var error in _errors.Snapshot(DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1), _lastErrorId))
        {
            _lastErrorId = error.Id;
            _sawmill.Warning(
                $"recent-server-error id={error.Id} at={error.Message.Timestamp:O} source={error.Sawmill} " +
                $"round={_ticker.RoundId} observedAtServerTick={_timing.CurTick} " +
                $"correlationOnly=true\n{CMURecentServerErrors.Format(error)}");
        }
    }

    private string DescribeEntity(EntityUid? uid)
    {
        if (uid == null || !TryComp<MetaDataComponent>(uid, out var meta))
            return $"uid={uid?.ToString() ?? "none"} unavailable";

        var result = $"uid={uid} net={meta.NetEntity} prototype={meta.EntityPrototype?.ID ?? "none"} lifeStage={meta.EntityLifeStage}";
        if (TryComp<TransformComponent>(uid, out var xform))
            result += $" parent={xform.ParentUid} map={xform.MapID} grid={xform.GridUid}";
        return result;
    }

    private static string Age(TimeSpan now, TimeSpan? time) => time is { } value
        ? (now - value).TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)
        : "unknown";

    private static string SafeName(string name)
    {
        if (name.Length > 64)
            name = name[..64];
        return name.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    private sealed class ClientTrace
    {
        public GameTick? LastAck;
        public TimeSpan? LastAckAt;
        public GameTick? AckAtRequest;
        public GameTick FirstRequestedTick;
        public GameTick RequestedTick;
        public TimeSpan FirstRequestAt;
        public TimeSpan NextDetail;
        public long Requests;
        public long SameTickRequests;
        public long WindowRequests;
        public long Suppressed;
        public TimeSpan LastRequestAt;
        public long WindowInitialRequests;
        public long WindowRecoveryRequests;
        public long SyncIncidentId;
        public TimeSpan SyncStartedAt;
        public TimeSpan? HealthAt;
        public CMUClientStateHealthEvent? Health;
        public NetEntity? LastMissingEntity;
    }
}
