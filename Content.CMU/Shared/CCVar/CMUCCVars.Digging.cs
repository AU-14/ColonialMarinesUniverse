using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>Allow tools and the dig-down driver to create new downward excavations in any game mode.</summary>
    public static readonly CVarDef<bool> AllowDiggingDown =
        CVarDef.Create("cmu.game.allow_digging_down", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
