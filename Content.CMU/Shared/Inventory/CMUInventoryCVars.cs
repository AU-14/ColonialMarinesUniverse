using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.Inventory;

[CVarDefs]
public sealed partial class CMUInventoryCVars : CVars
{
    /// <summary>
    /// Clicking a worn storage item (belt, backpack, pouch) with an empty hand opens it instead of taking it off.
    /// </summary>
    public static readonly CVarDef<bool> ClickOpensWornStorage =
        CVarDef.Create("cmu.inventory.click_opens_worn_storage", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
