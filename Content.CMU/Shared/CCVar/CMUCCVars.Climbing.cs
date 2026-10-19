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

    /// <summary>
    /// While lying down on purpose, others can't shake you back up.
    /// </summary>
    public static readonly CVarDef<bool> CMUNoShakeWhenResting =
        CVarDef.Create("cmu.no_shake_when_resting", false, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
}
