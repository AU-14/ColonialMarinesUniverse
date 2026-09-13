using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether eating and drinking automatically continue until the item is empty.
    ///     Replicated so the server can honor the consuming player's preference.
    /// </summary>
    public static readonly CVarDef<bool> CMUAutoIngestEnabled =
        CVarDef.Create("cmu.accessibility.auto_eat_and_drink", true, CVar.REPLICATED | CVar.CLIENT | CVar.ARCHIVE);
}
