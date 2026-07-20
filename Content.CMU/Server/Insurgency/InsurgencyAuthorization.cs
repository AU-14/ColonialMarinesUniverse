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
    // Host-gated to match the other Improved Construction Menu tools (Construction Item / Lathe / Tiles
    // editors all require AdminFlags.Host), so a regular admin cannot open the INSFOR editor.
    // Change this one constant to move the gate (for example to a future HRP whitelist manager).
    public const AdminFlags AuthorizedFlag = AdminFlags.Host;

    // The Custom-faction editor (insforcustomeditor) is open to players job-whitelisted for this job
    // via the jobwhitelistadd command - a separate, wider group than the host flag. Change this one
    // constant to gate it on a different whitelist job.
    public const string CustomEditorWhitelistJob = "AU14JobCLFCellLeader";

    // AU14: the old InsforEditor job-whitelist gate was replaced by per-tool ckey grants (see
    // AU14ToolPermissionSystem) because jobwhitelistadd was reachable by lower admin ranks. Trusted
    // non-admins are now granted the "insfor" tool through the Tool Permissions window / toolperm command.

    public static bool IsAuthorized(IAdminManager admin, ICommonSession player)
    {
        var data = admin.GetAdminData(player);
        if (data != null && data.HasFlag(AuthorizedFlag))
            return true;

        var perms = IoCManager.Resolve<IEntityManager>()
            .System<_AU14.Administration.AU14ToolPermissionSystem>();
        return perms.HasGrant(player, Content.Shared._AU14.Administration.AU14ToolPermissions.Insfor);
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
