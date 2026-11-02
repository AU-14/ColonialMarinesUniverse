using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Enables the FOB reserve for the next round and each validated Distress Signal map's
    /// equipment, legacy-defense replacement, contested evacuation, player information and trial reporting.
    /// </summary>
    public static readonly CVarDef<bool> CMUFobReserveEnabled =
        CVarDef.Create("cmu.fob.reserve_enabled", false, CVar.SERVERONLY);
}
