using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>Send a small client-reported state-application sample every five seconds.</summary>
    public static readonly CVarDef<bool> CMUClientStateHealthEnabled =
        CVarDef.Create("cmu.diagnostics.client_state_health_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);

    /// <summary>
    /// Records bounded server-side context when clients request replacement game states.
    /// Does not collect client logs or change state delivery.
    /// </summary>
    public static readonly CVarDef<bool> CMUClientStateDiagnosticsEnabled =
        CVarDef.Create("cmu.diagnostics.client_state_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
