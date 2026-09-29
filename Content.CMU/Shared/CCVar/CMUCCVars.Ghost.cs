using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// The player's chosen ghost color as a hex string. Empty uses the default ghost color.
    /// </summary>
    public static readonly CVarDef<string> CMUGhostColor =
        CVarDef.Create("cmu.ghost_color", "", CVar.CLIENTONLY | CVar.ARCHIVE);
}
