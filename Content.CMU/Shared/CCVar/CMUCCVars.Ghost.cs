using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// The player's chosen ghost color as a hex string. Empty uses the default ghost color.
    /// </summary>
    public static readonly CVarDef<string> CMUGhostColor =
        CVarDef.Create("cmu.ghost_color", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Whether ghosts can call in a random minor third party from the ghost role menu.
    /// </summary>
    public static readonly CVarDef<bool> GhostThirdPartyCallEnabled =
        CVarDef.Create("cmu.ghost.call_party_enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Dead or ghosted players must be at least this fraction of living players before a ghost call is allowed.
    /// </summary>
    public static readonly CVarDef<float> GhostThirdPartyCallDeadRatio =
        CVarDef.Create("cmu.ghost.call_party_dead_ratio", 0.3f, CVar.SERVERONLY);

    /// <summary>
    /// Minimum round age in minutes before ghosts can call in a third party.
    /// </summary>
    public static readonly CVarDef<int> GhostThirdPartyCallMinRoundMinutes =
        CVarDef.Create("cmu.ghost.call_party_round_minutes", 20, CVar.SERVERONLY);
}
