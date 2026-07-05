using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Shared.Player;

namespace Content.Server._AU14.Insurgency;

/// <summary>
///     Single point of change for who may author Default factions and select Custom factions.
///     Today this is the Host/admin flag. If a dedicated HRP whitelist flag is added later, swap the
///     check in <see cref="IsAuthorized"/> and nowhere else.
///
///     Authorization is always enforced server-side. The client editor only hides options as a
///     convenience; every editor message re-checks here before touching the DB or applying a faction.
/// </summary>
public static class InsurgencyAuthorization
{
    // The admin flag that authorizes editing Default factions and selecting Custom factions.
    // Change this one constant to move the gate (for example to a future HRP whitelist manager).
    public const AdminFlags AuthorizedFlag = AdminFlags.Admin;

    // The Custom-faction editor (insforcustomeditor) is open to players job-whitelisted for this job
    // via the jobwhitelistadd command - a separate, wider group than the host flag. Change this one
    // constant to gate it on a different whitelist job.
    public const string CustomEditorWhitelistJob = "AU14JobCLFCellLeader";

    // Players job-whitelisted for this marker job (jobwhitelistadd <player> InsforEditor) may open
    // the INSFOR editor without being admins. The job exists only as a whitelist key.
    public const string EditorWhitelistJob = "InsforEditor";

    public static bool IsAuthorized(IAdminManager admin, ICommonSession player)
    {
        var data = admin.GetAdminData(player);
        if (data != null && data.HasFlag(AuthorizedFlag))
            return true;

        var jobWhitelist = IoCManager.Resolve<Players.JobWhitelist.JobWhitelistManager>();
        return jobWhitelist.IsWhitelisted(player.UserId, EditorWhitelistJob);
    }

    public static bool IsCustomAuthorized(IAdminManager admin, ICommonSession player)
    {
        // Hosts/admins always qualify, so the Default group never locks itself out of the Custom editor.
        if (IsAuthorized(admin, player))
            return true;

        var jobWhitelist = IoCManager.Resolve<Players.JobWhitelist.JobWhitelistManager>();
        return jobWhitelist.IsWhitelisted(player.UserId, CustomEditorWhitelistJob);
    }
}
