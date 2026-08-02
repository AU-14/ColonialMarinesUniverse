using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Enables automatic CMU server performance incident detection and reporting.
    /// </summary>
    public static readonly CVarDef<bool> CMUServerPerformanceDiagnosticsEnabled =
        CVarDef.Create("cmu.server_performance.enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Enable the [CMU-PERF] log output. Doesn't influence normal metrics/collection, profiler or incident trackers.
    /// </summary>
    public static readonly CVarDef<bool> CMUServerPerformanceLogEnabled =
        CVarDef.Create("cmu.server_performance.log_enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds between full performance observations. Hard frame stalls are checked every server frame.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceSampleInterval =
        CVarDef.Create("cmu.server_performance.sample_interval", 1f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds after startup or a new round during which rate-based triggers are suppressed.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceWarmup =
        CVarDef.Create("cmu.server_performance.warmup", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds between healthy performance heartbeat log entries. Zero disables healthy heartbeats.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceHeartbeatInterval =
        CVarDef.Create("cmu.server_performance.heartbeat_interval", 60f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds between updates while a performance incident remains active.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceIncidentUpdateInterval =
        CVarDef.Create("cmu.server_performance.incident_update_interval", 30f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds between healthy churn baselines used by detailed incident reports.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceBaselineInterval =
        CVarDef.Create("cmu.server_performance.baseline_interval", 300f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     A single real frame at or above this duration immediately opens an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceStallMilliseconds =
        CVarDef.Create("cmu.server_performance.stall_ms", 250f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     A single real frame at or above this duration is classified as critical.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceCriticalStallMilliseconds =
        CVarDef.Create("cmu.server_performance.critical_stall_ms", 1000f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Opens an incident when achieved ticks per second remain below this fraction of target.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceLowTpsRatio =
        CVarDef.Create("cmu.server_performance.low_tps_ratio", 0.8f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Opens an incident when average server FPS remains below this fraction of target.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceLowFpsRatio =
        CVarDef.Create("cmu.server_performance.low_fps_ratio", 0.8f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds that the TPS or FPS threshold must remain breached before it opens an incident.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceBreachDuration =
        CVarDef.Create("cmu.server_performance.breach_duration", 3f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Low-TPS/FPS incidents remain active until the signal reaches this fraction of target.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceRecoveryRatio =
        CVarDef.Create("cmu.server_performance.recovery_ratio", 0.95f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds all triggers must remain healthy before an incident is closed.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceRecoveryDuration =
        CVarDef.Create("cmu.server_performance.recovery_duration", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Net entity growth per minute that opens an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceEntityGrowthPerMinute =
        CVarDef.Create("cmu.server_performance.entity_growth_per_minute", 1000f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Entity creations plus deletions per minute that open an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceEntityChurnPerMinute =
        CVarDef.Create("cmu.server_performance.entity_churn_per_minute", 5000f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Net component growth per minute that opens an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceComponentGrowthPerMinute =
        CVarDef.Create("cmu.server_performance.component_growth_per_minute", 10000f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Component additions plus removals per minute that open an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceComponentChurnPerMinute =
        CVarDef.Create("cmu.server_performance.component_churn_per_minute", 50000f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Outbound network MiB/s that opens an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceSendMiBPerSecond =
        CVarDef.Create("cmu.server_performance.send_mib_per_second", 25f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Inbound network MiB/s that opens an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceReceiveMiBPerSecond =
        CVarDef.Create("cmu.server_performance.receive_mib_per_second", 10f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Main-thread allocation in one profiled frame, in MiB, that opens an incident. Zero disables the trigger.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceAllocationMiBPerFrame =
        CVarDef.Create("cmu.server_performance.allocation_mib_per_frame", 32f, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Enables the engine flight-recorder profiler at server startup so the frames before a trigger are available.
    /// </summary>
    public static readonly CVarDef<bool> CMUServerPerformanceEnableProfiler =
        CVarDef.Create("cmu.server_performance.enable_profiler", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Number of selected profiler frames included in a detailed incident report. Selection mixes the slowest,
    ///     highest-allocation, and recent tick-bearing frames.
    /// </summary>
    public static readonly CVarDef<int> CMUServerPerformanceProfileFrames =
        CVarDef.Create("cmu.server_performance.profile_frames", 8, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Maximum profiler events parsed for one detailed incident report.
    /// </summary>
    public static readonly CVarDef<int> CMUServerPerformanceProfileMaxEvents =
        CVarDef.Create("cmu.server_performance.profile_max_events", 20000, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Maximum entries emitted per category in detailed performance reports.
    /// </summary>
    public static readonly CVarDef<int> CMUServerPerformanceReportTop =
        CVarDef.Create("cmu.server_performance.report_top", 10, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Minimum seconds between expensive profiler/churn detail reports. Incident summaries are not suppressed.
    /// </summary>
    public static readonly CVarDef<float> CMUServerPerformanceDetailCooldown =
        CVarDef.Create("cmu.server_performance.detail_cooldown", 120f, CVar.SERVERONLY | CVar.ARCHIVE);
}
