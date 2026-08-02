using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Part of the CLF intel console claim feature, when Govfor seizes it the game sends a fax
    ///     of the remaining CLF to the Marshal/Military faxes and can send a faction wide announcement
    ///     to the marines as fallback. Default: false, the announcement only goes automatically when zero
    ///     matching fax machines (groups) are found. On true, this will always notify marines instantly.
    /// </summary>
    public static readonly CVarDef<bool> CMUIntelClaimForceGovforAnnouncement =
        CVarDef.Create("cmu.intel_claim.force_govfor_announcement", false, CVar.SERVERONLY | CVar.ARCHIVE);
}
