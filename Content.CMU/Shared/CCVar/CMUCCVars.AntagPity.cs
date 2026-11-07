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

    /// <summary>
    /// Whether colony antag rules get their round-start fire chance lifted by how many
    /// ready players enabled that antag. See ColonyAntagsRuleSystem.
    /// </summary>
    public static readonly CVarDef<bool> AntagDemandEnabled =
        CVarDef.Create("cmu.antag.demand.enabled", true, CVar.SERVERONLY);

    /// <summary>
    /// Fire chance added to a colony antag rule per point of demand.
    /// One point is one max pity weight player whose antag preferences all match that rule.
    /// </summary>
    public static readonly CVarDef<float> AntagDemandBoost =
        CVarDef.Create("cmu.antag.demand.boost", 0.1f, CVar.SERVERONLY);

    /// <summary>
    /// Highest fire chance demand can push a colony antag rule to.
    /// A cap below a rule's base chance never lowers the base.
    /// </summary>
    public static readonly CVarDef<float> AntagDemandCap =
        CVarDef.Create("cmu.antag.demand.cap", 0.85f, CVar.SERVERONLY);
}
