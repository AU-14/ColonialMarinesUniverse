using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._AU14.CCVar;

[CVarDefs]
public sealed partial class AU14CCVars : CVars
{
    /// <summary>
    /// TODO: Whether the AU14 entity fire spreading system is enabled.
    /// </summary>
    public static readonly CVarDef<bool> FireSpreading =
        CVarDef.Create("au14.fire_spreading", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> SellCargoRewards =
        CVarDef.Create("au14.sell_cargo_rewards", true, CVar.SERVERONLY);

    // master switch for the AU14 comms overhaul, off = stock radio behavior
    public static readonly CVarDef<bool> NewCommsSystem =
        CVarDef.Create("au14.new_comms_system", false, CVar.SERVERONLY);
        
    /// <summary>
    /// With the "Separated" HUD layout the chat panel sits to the right of the viewport, which pushes the
    /// game view left of the monitor centre. When on, the viewport pane is padded so the game view sits in
    /// the middle of the window (at the cost of a slightly narrower viewport).
    /// </summary>
    public static readonly CVarDef<bool> CenterSeparatedViewport =
        CVarDef.Create("au14.center_separated_viewport", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> SeparatedHudStatusSide =
        CVarDef.Create("au14.separated_hud_status_side", "right", CVar.CLIENTONLY | CVar.ARCHIVE);
}
