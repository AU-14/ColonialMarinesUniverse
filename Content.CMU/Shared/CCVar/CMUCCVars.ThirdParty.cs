using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether ghosts can call in a random minor third party from the ghost role menu.
    /// </summary>
    public static readonly CVarDef<bool> GhostThirdPartyCallEnabled =
        CVarDef.Create("cmu.thirdparty.ghost_call_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Dead or ghosted players must be at least this fraction of living players before a ghost call is allowed.
    /// </summary>
    public static readonly CVarDef<float> GhostThirdPartyCallDeadRatio =
        CVarDef.Create("cmu.thirdparty.ghost_call_dead_ratio", 0.3f, CVar.SERVERONLY);

    /// <summary>
    /// Minimum round age in minutes before ghosts can call in a third party.
    /// </summary>
    public static readonly CVarDef<int> GhostThirdPartyCallMinRoundMinutes =
        CVarDef.Create("cmu.thirdparty.ghost_call_min_round_minutes", 20, CVar.SERVERONLY);
}
