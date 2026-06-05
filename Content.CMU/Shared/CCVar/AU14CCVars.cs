using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared._AU14.CCVar;

[CVarDefs]
public sealed partial class AU14CCVars : CVars
{
    /// <summary>
    ///     Whether the AU14 entity fire spreading system is enabled.
    /// </summary>
    public static readonly CVarDef<bool> FireSpreading =
        CVarDef.Create("au14.fire_spreading", false, CVar.SERVERONLY);
}
