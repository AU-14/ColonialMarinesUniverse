using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether this client's character gets the scream emote action pinned to their hotbar automatically.
    ///     Replicated so the server can honor the controlling player's preference.
    /// </summary>
    public static readonly CVarDef<bool> CMUScreamOnHotbarEnabled =
        CVarDef.Create("cmu.vocal.scream_on_hotbar", false, CVar.REPLICATED | CVar.CLIENT | CVar.ARCHIVE);
}
