using Robust.Shared.ContentPack;

namespace Content.Server.CMU14.Diagnostics.Performance;

public interface ICMUServerPerformanceDiagnostics
{
    void Initialize();
    void Update();
    void ObservePhase(ModUpdateLevel level);
    void EndFrameCallbacks();
    void RequestSyncReport(long syncIncidentId);
    string GetCorrelationContext();
    CMUPerformanceOperationScope MeasureOperation(string name, string? prototype = null);
    void Shutdown();
    string GetStatus();
    bool CaptureManualReport();
    bool ResetBaselines();
}
