using System.Globalization;
using Robust.Shared;

namespace Content.Server.CMU14.Diagnostics.Performance;

public sealed partial class CMUServerPerformanceDiagnosticsManager
{
    private CMUPerformancePvsMetrics _pvsMetrics = default!;

    /// <summary>Captures stage counters without traversing the entity census or profiler history.</summary>
    public bool CapturePvsReport()
    {
        if (!_initialized || !_enabled)
            return false;
        _sawmill.Warning($"[CMU-PERF] manual-pvs {GetCorrelationContext()}");
        LogPvsStages();
        return true;
    }

    private void LogPvsStages()
    {
        var window = _pvsMetrics.Drain(_timing.RealTime);
        var isAsync = _config.GetCVar(CVars.NetPvsAsync);
        foreach (var stage in window.Stages)
        {
            var total = stage.TotalMs?.ToString("F3", CultureInfo.InvariantCulture) ?? "unavailable";
            var average = stage.AverageMs?.ToString("F3", CultureInfo.InvariantCulture) ?? "unavailable";
            _sawmill.Warning(Invariant(
                $"[CMU-PERF] pvs-stage-window incidentId={_activeIncidentId} area={SanitizeName(stage.Name)} ",
                $"calls={stage.Calls} totalMs={total} avgMs={average} status={stage.Status} ",
                $"windowSeconds={window.Seconds:F3} pvsAsync={isAsync} processWide=true source=engine-histogram"));
        }
    }
}
