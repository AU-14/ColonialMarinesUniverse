using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.ZLevels;

public sealed partial class CMUZLevelsCVars
{
    /// <summary>Collect detailed client render and projected-light diagnostics for cmu_zrender_debug.</summary>
    public static readonly CVarDef<bool> ClientDiagnosticsEnabled =
        CVarDef.Create("cmu.zlevels.client_diagnostics", false, CVar.CLIENTONLY);
}
