using Robust.Shared.Configuration;

namespace Content.Shared.CMU14.ZLevels;

public sealed partial class CMUZLevelsCVars
{
    /// <summary>Maximum live projected entities across all receiving maps, including visibility fades.</summary>
    public static readonly CVarDef<int> MaxProjectedLightsGlobal =
        CVarDef.Create("cmu.zlevels.projected_lighting_max_global", 128, CVar.CLIENTONLY | CVar.ARCHIVE);
}
