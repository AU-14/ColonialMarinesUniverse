using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Extra tips dataset merged into the periodic tip draw alongside tips.dataset.
    /// Empty disables the merge and restores the stock single-dataset draw.
    /// </summary>
    public static readonly CVarDef<string> CmuTipsDataset =
        CVarDef.Create("cmu.tips.dataset", "CMU_Tips", CVar.SERVERONLY);
}
