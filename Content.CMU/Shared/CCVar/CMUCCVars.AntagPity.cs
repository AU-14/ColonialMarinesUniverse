using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Whether antag pity weighting is active. When disabled, round-start antag
    /// selection is a flat lottery again and nothing is accrued or stored.
    /// </summary>
    public static readonly CVarDef<bool> AntagPityEnabled =
        CVarDef.Create("cmu.antag.pity.enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Draw weight added per consecutive round an opted-in player goes unselected.
    /// </summary>
    public static readonly CVarDef<float> AntagPityStep =
        CVarDef.Create("cmu.antag.pity.step", 0.25f, CVar.SERVERONLY);

    /// <summary>
    /// Maximum total draw weight a player can reach through pity.
    /// Values below 1 are treated as 1.
    /// </summary>
    public static readonly CVarDef<float> AntagPityCap =
        CVarDef.Create("cmu.antag.pity.cap", 3f, CVar.SERVERONLY);
}
