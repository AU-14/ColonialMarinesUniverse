namespace Content.Server.CMU14.Diagnostics.Performance;

public interface ICMUServerPerformanceDiagnostics
{
    void Initialize();
    void Update();
    void Shutdown();
    string GetStatus();
    bool CaptureManualReport();
    bool ResetBaselines();
}
