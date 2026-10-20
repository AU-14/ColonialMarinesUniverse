using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Require an installed battery to arm its supported weapons, and a matching authorized disk loaded in one to fire.
    /// </summary>
    public static readonly CVarDef<bool> ShipWeaponsRequireGuns =
        CVarDef.Create("cmu.ship_weapons.require_guns", true, CVar.SERVERONLY | CVar.ARCHIVE);
}
