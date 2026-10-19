using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Require an installed battery to arm its supported weapons, and a matching loaded mission round to fire.
    /// </summary>
    public static readonly CVarDef<bool> ShipWeaponsRequireGuns =
        CVarDef.Create("cmu.ship_weapons.require_guns", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
