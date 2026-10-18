// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Shows the held gun's ammo counter on screen while a squad optic is down.
    /// </summary>
    public static readonly CVarDef<bool> CMUAmmoHud =
        CVarDef.Create("cmu.ammo_hud", true, CVar.CLIENT | CVar.ARCHIVE);
}
