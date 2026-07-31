using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Enables client-side CMU rubberband diagnostics for the local attached entity.
    /// </summary>
    public static readonly CVarDef<bool> CMURubberbandDiagnosticsEnabled =
        CVarDef.Create("cmu.rubberband_diagnostics.enabled", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Minimum same-map local player movement, in tiles/metres, that is logged as a suspected snap.
    /// </summary>
    public static readonly CVarDef<float> CMURubberbandDiagnosticsSnapThreshold =
        CVarDef.Create("cmu.rubberband_diagnostics.snap_threshold", 0.75f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Number of server states applied in one client frame before logging a catch-up diagnostic.
    /// </summary>
    public static readonly CVarDef<int> CMURubberbandDiagnosticsCatchupApplyThreshold =
        CVarDef.Create("cmu.rubberband_diagnostics.catchup_apply_threshold", 3, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Minimum seconds between repeated CMU rubberband diagnostic log lines of the same kind.
    /// </summary>
    public static readonly CVarDef<float> CMURubberbandDiagnosticsLogCooldown =
        CVarDef.Create("cmu.rubberband_diagnostics.log_cooldown", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
