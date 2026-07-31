using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    private const CVar CmuServerPerformanceFlags = CVar.SERVERONLY | CVar.ARCHIVE;

    public static readonly CVarDef<bool> CMUServerPerformanceDiagnosticsEnabled =
        CVarDef.Create("cmu.server_performance.enabled", true, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceSampleInterval =
        CVarDef.Create("cmu.server_performance.sample_interval", 1f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceWarmup =
        CVarDef.Create("cmu.server_performance.warmup", 30f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceHeartbeatInterval =
        CVarDef.Create("cmu.server_performance.heartbeat_interval", 60f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceIncidentUpdateInterval =
        CVarDef.Create("cmu.server_performance.incident_update_interval", 30f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceBaselineInterval =
        CVarDef.Create("cmu.server_performance.baseline_interval", 300f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceStallMilliseconds =
        CVarDef.Create("cmu.server_performance.stall_ms", 250f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceCriticalStallMilliseconds =
        CVarDef.Create("cmu.server_performance.critical_stall_ms", 1000f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceLowTpsRatio =
        CVarDef.Create("cmu.server_performance.low_tps_ratio", 0.8f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceLowFpsRatio =
        CVarDef.Create("cmu.server_performance.low_fps_ratio", 0.8f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceBreachDuration =
        CVarDef.Create("cmu.server_performance.breach_duration", 3f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceRecoveryRatio =
        CVarDef.Create("cmu.server_performance.recovery_ratio", 0.95f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceRecoveryDuration =
        CVarDef.Create("cmu.server_performance.recovery_duration", 10f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceEntityGrowthPerMinute =
        CVarDef.Create("cmu.server_performance.entity_growth_per_minute", 1000f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceEntityChurnPerMinute =
        CVarDef.Create("cmu.server_performance.entity_churn_per_minute", 5000f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceComponentGrowthPerMinute =
        CVarDef.Create("cmu.server_performance.component_growth_per_minute", 10000f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceComponentChurnPerMinute =
        CVarDef.Create("cmu.server_performance.component_churn_per_minute", 50000f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceSendMiBPerSecond =
        CVarDef.Create("cmu.server_performance.send_mib_per_second", 25f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceReceiveMiBPerSecond =
        CVarDef.Create("cmu.server_performance.receive_mib_per_second", 10f, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceAllocationMiBPerFrame =
        CVarDef.Create("cmu.server_performance.allocation_mib_per_frame", 32f, CmuServerPerformanceFlags);
    public static readonly CVarDef<bool> CMUServerPerformanceEnableProfiler =
        CVarDef.Create("cmu.server_performance.enable_profiler", true, CmuServerPerformanceFlags);
    public static readonly CVarDef<int> CMUServerPerformanceProfileFrames =
        CVarDef.Create("cmu.server_performance.profile_frames", 8, CmuServerPerformanceFlags);
    public static readonly CVarDef<int> CMUServerPerformanceProfileMaxEvents =
        CVarDef.Create("cmu.server_performance.profile_max_events", 20000, CmuServerPerformanceFlags);
    public static readonly CVarDef<int> CMUServerPerformanceReportTop =
        CVarDef.Create("cmu.server_performance.report_top", 10, CmuServerPerformanceFlags);
    public static readonly CVarDef<float> CMUServerPerformanceDetailCooldown =
        CVarDef.Create("cmu.server_performance.detail_cooldown", 120f, CmuServerPerformanceFlags);
}
