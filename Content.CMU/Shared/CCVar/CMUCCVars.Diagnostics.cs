using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Records bounded server-side context when clients request replacement game states.
    /// Does not collect client logs or change state delivery.
    /// </summary>
    public static readonly CVarDef<bool> CMUClientStateDiagnosticsEnabled =
        CVarDef.Create("cmu.diagnostics.client_state_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
