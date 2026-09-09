using System.Text;
using Content.Server.GameTicking;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Robust.Server.GameStates;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.CMU14.Diagnostics;

/// <summary>
/// Observes the existing state-request/ACK stream without changing PVS or asking clients for more data.
/// ACKs mean a state was received, not that applying it (or rendering) succeeded.
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

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _logManager.GetSawmill(SawmillName);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
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
        trace.RequestedTick = tick;
        trace.AckAtRequest = trace.LastAck;
        _requests++;

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
            $"firstRequestedTick={trace.FirstRequestedTick} requests={trace.Requests} sameTickRequests={trace.SameTickRequests} " +
            $"suppressedDetails={trace.Suppressed} sinceFirstRequestSeconds={(now - trace.FirstRequestAt).TotalSeconds:F1} " +
            $"lastReceivedAck={trace.LastAck?.ToString() ?? "unknown"} ackAgeSeconds={Age(now, trace.LastAckAt)} " +
            $"pingMs={session.Ping} attached=[{DescribeEntity(session.AttachedEntity)}] " +
            $"missingNetEntity={missingEntity?.ToString() ?? "none"} missing=[{DescribeEntity(GetEntity(missingEntity))}] " +
            $"cleanupTick={_cleanupTick?.ToString() ?? "none"} cleanupAgeSeconds={Age(now, _lastCleanup)} " +
            "clientAppliedState=unknown");
        trace.Suppressed = 0;
        WriteRecentErrors(now);
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus != SessionStatus.Disconnected || !_clients.Remove(args.Session, out var trace))
            return;

        if (trace.Requests == 0)
            return;

        _disconnected++;
        // The normal disconnect log already identifies the player. Keep this detail in the same global budget.
        if (TryTakeDetail(_timing.RealTime))
        {
            _sawmill.Warning(
                $"disconnect-after-state-request user={args.Session.UserId} round={_ticker.RoundId} " +
                $"serverTick={_timing.CurTick} requests={trace.Requests} requestedTick={trace.RequestedTick} " +
                $"lastReceivedAck={trace.LastAck?.ToString() ?? "unknown"} clientAppliedState=unknown");
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
        var samples = new StringBuilder();
        foreach (var (session, trace) in _clients)
        {
            if (trace.WindowRequests == 0)
                continue;

            affected++;
            if (trace.WindowRequests > 1)
                repeated++;
            if (trace.LastAck is { } ack && (trace.AckAtRequest == null || ack > trace.AckAtRequest.Value))
                ackAdvanced++;
            if (affected <= MaxSummarySamples)
            {
                samples.Append($" user={session.UserId}/tick={trace.RequestedTick}/requests={trace.WindowRequests}" +
                               $"/ack={trace.LastAck?.ToString() ?? "unknown"}");
            }

            trace.WindowRequests = 0;
        }

        if (_requests > 0 || _disconnected > 0)
        {
            _sawmill.Warning(
                $"state-request-summary reason={reason} round={_ticker.RoundId} phase={_ticker.RunLevel} " +
                $"serverTick={_timing.CurTick} windowSeconds={(now - _windowStart).TotalSeconds:F1} " +
                $"requests={_requests} affectedConnectedClients={affected} repeatedClients={repeated} " +
                $"ackAdvancedAfterLastRequestClients={ackAdvanced} disconnectedAfterRequest={_disconnected} " +
                $"suppressedDetails={_suppressed} cleanupTick={_cleanupTick?.ToString() ?? "none"} " +
                $"cleanupAgeSeconds={Age(now, _lastCleanup)} samples=[{samples}] " +
                "clientAppliedState=unknown");
            WriteRecentErrors(now);
        }

        _requests = 0;
        _suppressed = 0;
        _disconnected = 0;
        _windowStart = now;
        _nextSummary = now + ReportInterval;
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
    }
}
