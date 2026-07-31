using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// When enabled, forces tile-based movement (see CMUTileMovementComponent) onto every mob with an
    /// InputMoverComponent, regardless of whether they have the trait. Toggled via the
    /// "cmuforcetilemovement" admin command.
    /// </summary>
    [CVarControl(AdminFlags.VarEdit)]
    public static readonly CVarDef<bool> CMUTileMovementForcedForAll =
        CVarDef.Create("cmu.tile_movement.forced_for_all", false, CVar.SERVERONLY);
}
