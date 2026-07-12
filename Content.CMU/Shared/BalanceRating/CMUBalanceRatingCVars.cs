using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._CMU14.BalanceRating;

[CVarDefs]
public sealed partial class CMUBalanceRatingCVars : CVars
{
    /// <summary>
    /// Whether balance-rating prompts are automatically opened between rounds.
    /// </summary>
    public static readonly CVarDef<bool> AutomaticEnabled =
        CVarDef.Create("cmu.balance_rating.automatic_enabled", true, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Minimum number of started rounds between automatic balance-rating prompts.
    /// </summary>
    public static readonly CVarDef<int> AutomaticMinimumRounds =
        CVarDef.Create("cmu.balance_rating.automatic_minimum_rounds", 2, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum number of started rounds between automatic balance-rating prompts.
    /// </summary>
    public static readonly CVarDef<int> AutomaticMaximumRounds =
        CVarDef.Create("cmu.balance_rating.automatic_maximum_rounds", 6, CVar.SERVERONLY | CVar.ARCHIVE);
}
