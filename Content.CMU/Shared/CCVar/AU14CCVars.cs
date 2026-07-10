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
}
