// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Walking into something you can vault over starts vaulting it, as if you'd dragged yourself onto it.
    /// </summary>
    public static readonly CVarDef<bool> CMUVaultOnBump =
        CVarDef.Create("cmu.vault_on_bump", false, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
}
