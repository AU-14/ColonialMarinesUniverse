using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>Enables the xeno passive regeneration charge system.</summary>
    [CVarControl(AdminFlags.VarEdit)]
    public static readonly CVarDef<bool> CMUXenoRegenPoolEnabled =
        CVarDef.Create("cmu.xeno.regen_pool_enabled", true, CVar.SERVER | CVar.REPLICATED | CVar.ARCHIVE);
}
