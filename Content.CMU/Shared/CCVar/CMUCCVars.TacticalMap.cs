using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> CMUTacMapClassic =
        CVarDef.Create("cmu.tacmap.classic", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> CMUTacMapCenterOnOpen =
        CVarDef.Create("cmu.tacmap.center_on_open", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    // Only explicit selections made aboard ship change this preference.
    public static readonly CVarDef<bool> CMUTacMapPlanetOnShip =
        CVarDef.Create("cmu.tacmap.planet_on_ship", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
