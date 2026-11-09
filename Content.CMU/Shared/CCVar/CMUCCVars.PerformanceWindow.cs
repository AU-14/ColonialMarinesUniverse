using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>Opt-in aggregate server telemetry. Requires the server performance monitor.</summary>
    public static readonly CVarDef<bool> CMUServerPerformanceWindowEnabled =
        CVarDef.Create("cmu.server_performance.window_enabled", false, CVar.SERVERONLY);

    /// <summary>Seconds between continuous windows, clamped to 5–60. Does not change gameplay sampling.</summary>
    public static readonly CVarDef<float> CMUServerPerformanceWindowInterval =
        CVarDef.Create("cmu.server_performance.window_interval", 5f, CVar.SERVERONLY);
}
