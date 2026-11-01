using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Soft wall-clock budget for expedition decisions per tick. One decision always
    /// completes; weapon execution and safety checks continue for every active agent.
    /// </summary>
    public static readonly CVarDef<float> CMUExpeditionThinkBudgetMs =
        CVarDef.Create("cmu.expedition_ai.think_budget_ms", 3f, CVar.SERVERONLY);

    /// <summary>Additional work cap for large batches of inexpensive decisions.</summary>
    public static readonly CVarDef<int> CMUExpeditionThinksPerTick =
        CVarDef.Create("cmu.expedition_ai.thinks_per_tick", 64, CVar.SERVERONLY);
}
