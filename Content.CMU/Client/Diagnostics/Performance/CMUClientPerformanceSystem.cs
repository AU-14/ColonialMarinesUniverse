using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Content.Shared.CMU14.ZLevels;
using Robust.Client.Console;
using Robust.Client.GameStates;
using Robust.Client.Graphics;
using Robust.Client.Timing;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Network;
using Robust.Shared.Profiling;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Diagnostics.Performance;

/// <summary>Opt-in, bounded client FPS capture, driven by cmu_client_perf.</summary>
public sealed partial class CMUClientPerformanceSystem : EntitySystem
{
    private const double ReportSeconds = 5;
    private const double InventorySeconds = 15;
    // Retain a completed busy frame while the next frame is still being written. The default
    // engine ring can overwrite even one detailed frame before the reader gets to it.
    internal const int MinimumProfileLogSize = 262144;
    internal static readonly ResPath OutputDirectory = new("/client-performance");

    [Dependency] private IClyde _clyde = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private IClientGameStateManager _gameStates = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IClientNetManager _network = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private ProfManager _profiler = default!;
    [Dependency] private IResourceManager _resources = default!;
    [Dependency] private IClientGameTiming _timing = default!;

    private ISawmill _sawmill = default!;
    private TextWriter? _writer;
    private CMUClientProfileReader? _reader;
    private readonly List<double> _wallFrames = new(4096);
    private NetworkStats _lastNetwork;
    private TimeSpan _started;
    private TimeSpan _ends;
    private TimeSpan _lastReport;
    private TimeSpan _nextInventory;
    private long _excludeThroughFrame;
    private double _spikeMs;
    private double _wallTotal;
    private double _wallMax;
    private long _wallMaxFrame;
    private int _wallCount;
    private int _spikes;
    private int _unfocusedFrames;
    private int _sessionFrames;
    private int _sessionSpikes;
    private double _sessionWorstMs;
    private double _readerMs;
    private double _readerMaxMs;
    private long _readerBytes;
    private long _created;
    private long _deleted;
    private long _addedComponents;
    private long _removedComponents;
    private string _worstContext = "";
    private string? _lastPath;
    private bool _profilerOwned;
    private bool _zDiagnosticsOwned;
    private bool _profileBufferOwned;
    private int _previousProfileBufferSize;
    private long _lastLostFrames;
    private int _lastInvalidFrames;
    private bool _changingSettings;

    public bool Capturing => _reader != null;

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = _log.GetSawmill("cmu.client-performance");
        Subs.CVar(_config, CVars.ProfEnabled, OnProfilerChanged);
        Subs.CVar(_config, CVars.ProfBufferSize, OnProfileBufferChanged);
        Subs.CVar(_config, CMUZLevelsCVars.ClientDiagnosticsEnabled, OnZDiagnosticsChanged);
    }

    public override void Shutdown()
    {
        if (Capturing)
            StopCapture("shutdown");
        base.Shutdown();
    }

    public string StartCapture(int seconds, double spikeMs)
    {
        if (Capturing)
            return "A capture is already running. Use cmu_client_perf stop or status.";
        if (seconds is < 5 or > 1800 || !double.IsFinite(spikeMs) || spikeMs is < 1 or > 10000)
            return "Duration must be 5–1800 seconds and spike threshold 1–10000 ms.";

        _resources.UserData.CreateDir(OutputDirectory);
        var path = OutputDirectory / $"client-perf-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.log";
        // Open before changing any engine settings, so a file error cannot leave profiling enabled.
        _writer = new StreamWriter(_resources.UserData.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        _lastPath = path.ToString();
        _profilerOwned = !_config.GetCVar(CVars.ProfEnabled);
        _zDiagnosticsOwned = !_config.GetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled);
        _previousProfileBufferSize = _config.GetCVar(CVars.ProfBufferSize);
        _profileBufferOwned = _previousProfileBufferSize < MinimumProfileLogSize;
        try
        {
            _changingSettings = true;
            if (_profileBufferOwned)
                _config.SetCVar(CVars.ProfBufferSize, MinimumProfileLogSize);
            _config.SetCVar(CVars.ProfEnabled, true);
            _config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, true);
            _changingSettings = false;
            _reader = new CMUClientProfileReader(_profiler.Buffer.IndexWriteOffset);
            _started = _lastReport = _timing.RealTime;
            _ends = _started + TimeSpan.FromSeconds(seconds);
            _nextInventory = _started + TimeSpan.FromSeconds(InventorySeconds);
            _spikeMs = spikeMs;
            _sessionFrames = _sessionSpikes = 0;
            _sessionWorstMs = 0;
            _inventoryBaseline = null;
            ResetWindow();
            _lastNetwork = _network.Statistics;
            EntityManager.EntityInitialized += OnEntityCreated;
            EntityManager.EntityDeleted += OnEntityDeleted;
            EntityManager.ComponentAdded += OnComponentAdded;
            EntityManager.ComponentRemoved += OnComponentRemoved;

            var probe = ProfSampler.StartNew();
            var text = new StringBuilder();
            text.AppendLine($"CMU CLIENT PERFORMANCE CAPTURE v1 utc={DateTime.UtcNow:O} durationSeconds={seconds} spikeMs={F(spikeMs)}");
            text.AppendLine("All times are milliseconds; allocations are bytes on the sampled main thread. Timing scopes are inclusive and overlap: do not sum parent and child costs.");
            text.AppendLine("When allocationRatePartial=True, bytesPerSecond is a lower bound from retained frames; missing frames must not be interpreted as zero allocation.");
            text.AppendLine("Frame work excludes the engine sleep/FPS limiter but may include GPU/driver waits. Wall frame time includes waiting. GC counters are collection counts, not GC pause durations.");
            text.AppendLine("GPU utilization/VRAM, process heap/RSS, background-thread allocations and call stacks are unavailable through this content capture. It identifies instrumented scopes, not individual methods inside them.");
            text.AppendLine("Reports every 5s; inventory every 15s. Worst work and allocation frames are preserved per window. Report/start/stop frames are excluded; per-frame reader overhead is reported separately. Profiling itself still has overhead.");
            text.AppendLine($"limits: eventsPerFrame={CMUClientProfileReader.MaxEventsPerFrame} scopePaths={CMUClientProfileReader.MaxScopes} wallSamples=4096 profileLogEntries={_profiler.Buffer.LogBuffer.Length}; data loss/truncation is reported explicitly.");
            AppendSettings(text);
            AppendContext(text, "start");
            AppendInventory(text);
            Write(text.ToString());
            Write($"startDiagnosticMs={F(probe.Elapsed.TotalMilliseconds)} startDiagnosticBytes={probe.ElapsedAlloc}");
            _excludeThroughFrame = _timing.CurFrame;
        }
        catch
        {
            FinishCapture();
            throw;
        }
        return $"Capturing client performance for {seconds}s (spikes >= {F(spikeMs)} ms). Close the console and reproduce the FPS drop. File: {_lastPath}. Use cmu_client_perf stop to finish early, then cmu_client_perf open.";
    }

    public string Status()
    {
        return Capturing
            ? $"Capture running; {F(Math.Max(0, (_ends - _timing.RealTime).TotalSeconds))}s remaining, {_sessionFrames + _wallCount} measured frames, {_sessionSpikes + _spikes} spikes. File: {_lastPath}"
            : $"No capture running. Last file: {_lastPath ?? "none"}. Use cmu_client_perf to start.";
    }

    public string StopCapture(string reason = "manual")
    {
        if (!Capturing)
            return Status();
        try
        {
            CaptureReport(reason, inventory: true);
            Write($"capture-end reason={reason} elapsedSeconds={F((_timing.RealTime - _started).TotalSeconds)} measuredFrames={_sessionFrames} spikes={_sessionSpikes} worstWallMs={F(_sessionWorstMs)}");
        }
        finally
        {
            FinishCapture();
        }
        return $"Client performance capture saved: {_lastPath}. Run cmu_client_perf open and send the .log file.";
    }

    public string ManualReport()
    {
        if (!Capturing)
            return "Start a capture with cmu_client_perf first.";
        try
        {
            CaptureReport("manual-report", inventory: true);
        }
        catch
        {
            FinishCapture();
            throw;
        }
        return $"Detailed checkpoint written to {_lastPath}. Capture continues.";
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_reader == null)
            return;
        try
        {
            var probe = ProfSampler.StartNew();
            var worst = _reader.WorstWork;
            var completedFrame = (long) _timing.CurFrame - 1;
            var wallMs = _timing.RealFrameTime.TotalMilliseconds;
            _reader.Read(_profiler, _excludeThroughFrame,
                completedFrame > _excludeThroughFrame && wallMs > _wallMax ? completedFrame : -1);
            if (_reader.WorstWork != worst)
            {
                var context = new StringBuilder();
                AppendContext(context, "worst-work-observation");
                _worstContext = context.ToString();
            }
            _readerMs += probe.Elapsed.TotalMilliseconds;
            _readerMaxMs = Math.Max(_readerMaxMs, probe.Elapsed.TotalMilliseconds);
            _readerBytes += probe.ElapsedAlloc;

            // CurFrame increments in Render, after FrameUpdate. RealFrameTime belongs to the previous loop.
            if (completedFrame > _excludeThroughFrame)
            {
                var ms = wallMs;
                _wallCount++;
                _wallTotal += ms;
                if (_wallFrames.Count < 4096)
                    _wallFrames.Add(ms);
                if (ms > _wallMax)
                {
                    _wallMax = ms;
                    _wallMaxFrame = completedFrame;
                }
                if (ms >= _spikeMs)
                    _spikes++;
                if (!_clyde.IsFocused)
                    _unfocusedFrames++;
            }

            var now = _timing.RealTime;
            if (now >= _ends)
                _console.WriteLine(null, StopCapture("duration"));
            else if (now - _lastReport >= TimeSpan.FromSeconds(ReportSeconds))
                CaptureReport("periodic", now >= _nextInventory);
        }
        catch (Exception e)
        {
            FinishCapture();
            _sawmill.Error($"[CMU-CLIENT-PERF] Capture stopped after a diagnostic error: {e}");
            _console.WriteError(null, $"Client performance capture stopped: {e.Message}. Partial file: {_lastPath}");
        }
    }

    private void CaptureReport(string reason, bool inventory)
    {
        if (_reader == null)
            return;
        var probe = ProfSampler.StartNew();
        _reader.Read(_profiler, _excludeThroughFrame);
        var now = _timing.RealTime;
        var elapsed = Math.Max(0.001, (now - _lastReport).TotalSeconds);
        var text = new StringBuilder(32768);
        _wallFrames.Sort();
        text.AppendLine($"REPORT reason={reason} elapsed={F((now - _started).TotalSeconds)} windowSeconds={F(elapsed)} utc={DateTime.UtcNow:O}");
        text.AppendLine($"wall: frames={_wallCount} fps={F(_wallTotal > 0 ? _wallCount * 1000 / _wallTotal : 0)} meanMs={F(_wallCount > 0 ? _wallTotal / _wallCount : 0)} p50Ms={F(Percentile(0.50))} p95Ms={F(Percentile(0.95))} p99Ms={F(Percentile(0.99))} maxMs={F(_wallMax)} maxFrame={_wallMaxFrame} spikes={_spikes} thresholdMs={F(_spikeMs)} unfocusedFrames={_unfocusedFrames} percentileSamples={_wallFrames.Count} omittedPercentileSamples={_wallCount - _wallFrames.Count}");
        var lostFrames = _reader.LostFrames - _lastLostFrames;
        var partialAllocations = lostFrames > 0 || _reader.InvalidFrames > _lastInvalidFrames;
        text.AppendLine($"profile: enabled={_profiler.IsEnabled} frames={_reader.Frames} meanWorkMs={F(_reader.Frames > 0 ? _reader.TotalWorkMs / _reader.Frames : 0)} allocatedBytes={_reader.TotalAllocatedBytes} bytesPerSecond={F(_reader.TotalAllocatedBytes / elapsed)} allocationRatePartial={partialAllocations} lostFramesWindow={lostFrames} lostFramesTotal={_reader.LostFrames} oversizedFramesTotal={_reader.OversizedFrames} invalidFramesTotal={_reader.InvalidFrames} droppedScopeEventsTotal={_reader.DroppedScopeEvents} excludedDiagnosticFramesTotal={_reader.ExcludedFrames} profileLogEntries={_profiler.Buffer.LogBuffer.Length}");
        text.AppendLine($"reader-overhead: totalMs={F(_readerMs)} maxMs={F(_readerMaxMs)} bytes={_readerBytes}; included in sampled CMUClientPerformanceSystem time");
        var net = _network.Statistics;
        text.AppendLine($"network-window: rxBytesPerSecond={F(Math.Max(0, net.ReceivedBytes - _lastNetwork.ReceivedBytes) / elapsed)} txBytesPerSecond={F(Math.Max(0, net.SentBytes - _lastNetwork.SentBytes) / elapsed)} rxPackets={Math.Max(0, net.ReceivedPackets - _lastNetwork.ReceivedPackets)} txPackets={Math.Max(0, net.SentPackets - _lastNetwork.SentPackets)} countersReset={net.ReceivedBytes < _lastNetwork.ReceivedBytes || net.SentBytes < _lastNetwork.SentBytes}");
        text.AppendLine($"churn-window: entitiesCreated={_created} entitiesDeleted={_deleted} componentsAdded={_addedComponents} componentsRemoved={_removedComponents}");
        AppendContext(text, "report-time");
        AppendProfile(text, _reader, _wallMaxFrame);
        text.Append(_worstContext);
        if (inventory)
        {
            AppendSettings(text);
            AppendInventory(text);
            _nextInventory = now + TimeSpan.FromSeconds(InventorySeconds);
        }
        Write(text.ToString());
        Write($"reportDiagnosticMs={F(probe.Elapsed.TotalMilliseconds)} reportDiagnosticBytes={probe.ElapsedAlloc} excludedFrame={_timing.CurFrame}");
        _excludeThroughFrame = _timing.CurFrame;
        _sessionFrames += _wallCount;
        _sessionSpikes += _spikes;
        _sessionWorstMs = Math.Max(_sessionWorstMs, _wallMax);
        _lastReport = now;
        _lastNetwork = net;
        ResetWindow();
    }

    private void ResetWindow()
    {
        _lastLostFrames = _reader?.LostFrames ?? 0;
        _lastInvalidFrames = _reader?.InvalidFrames ?? 0;
        _reader?.ResetWindow();
        _wallFrames.Clear();
        _wallCount = _spikes = _unfocusedFrames = 0;
        _wallTotal = _wallMax = _readerMs = _readerMaxMs = 0;
        _wallMaxFrame = _readerBytes = _created = _deleted = _addedComponents = _removedComponents = 0;
        _worstContext = "";
    }

    private double Percentile(double percentile)
    {
        return _wallFrames.Count == 0 ? 0 : _wallFrames[Math.Clamp((int) Math.Ceiling(_wallFrames.Count * percentile) - 1, 0, _wallFrames.Count - 1)];
    }

    private void Write(string message)
    {
        _writer?.WriteLine(message);
        _writer?.Flush();
        _sawmill.Info($"[CMU-CLIENT-PERF] {message}");
    }

    private void FinishCapture()
    {
        EntityManager.EntityInitialized -= OnEntityCreated;
        EntityManager.EntityDeleted -= OnEntityDeleted;
        EntityManager.ComponentAdded -= OnComponentAdded;
        EntityManager.ComponentRemoved -= OnComponentRemoved;
        _reader = null;
        _inventoryBaseline = null;
        try
        {
            _changingSettings = true;
            if (_profilerOwned)
                _config.SetCVar(CVars.ProfEnabled, false);
            if (_zDiagnosticsOwned)
                _config.SetCVar(CMUZLevelsCVars.ClientDiagnosticsEnabled, false);
            if (_profileBufferOwned)
                _config.SetCVar(CVars.ProfBufferSize, _previousProfileBufferSize);
        }
        finally
        {
            _changingSettings = _profilerOwned = _zDiagnosticsOwned = _profileBufferOwned = false;
            var writer = _writer;
            _writer = null;
            writer?.Dispose();
        }
    }

    private void OnProfilerChanged(bool enabled)
    {
        if (!_changingSettings)
            _profilerOwned = false;
    }

    private void OnZDiagnosticsChanged(bool enabled)
    {
        if (!_changingSettings)
            _zDiagnosticsOwned = false;
    }

    private void OnProfileBufferChanged(int size)
    {
        if (!_changingSettings)
            _profileBufferOwned = false;
    }

    private void OnEntityCreated(Entity<MetaDataComponent> entity) => _created++;
    private void OnEntityDeleted(Entity<MetaDataComponent> entity) => _deleted++;
    private void OnComponentAdded(AddedComponentEventArgs args) => _addedComponents++;
    private void OnComponentRemoved(RemovedComponentEventArgs args) => _removedComponents++;
    private static string F(double value) => value.ToString("F3", CultureInfo.InvariantCulture);
}
