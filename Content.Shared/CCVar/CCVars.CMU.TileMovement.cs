using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    // cmu edit start: doc updated for the tile movement setting
    /// <summary>
    /// When enabled, forces tile-based movement (see CMUTileMovementComponent) onto every mob with an
    /// InputMoverComponent, regardless of their own tile movement setting. Toggled via the
    /// "cmuforcetilemovement" admin command.
    /// </summary>
    // cmu edit end
    [CVarControl(AdminFlags.VarEdit)]
    public static readonly CVarDef<bool> CMUTileMovementForcedForAll =
        CVarDef.Create("cmu.tile_movement.forced_for_all", false, CVar.SERVERONLY);

    // cmu edit start
    /// <summary>
    /// A player's own choice to move from tile to tile, set in the CMU options tab.
    /// </summary>
    public static readonly CVarDef<bool> CMUTileMovementPreferred =
        CVarDef.Create("cmu.tile_movement.preferred", false, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);

    /// <summary>
    /// Move from tile to tile only while walking; running uses normal movement.
    /// </summary>
    public static readonly CVarDef<bool> CMUTileMovementWalkOnly =
        CVarDef.Create("cmu.tile_movement.walk_only", false, CVar.CLIENT | CVar.ARCHIVE | CVar.REPLICATED);
    // cmu edit end
}
