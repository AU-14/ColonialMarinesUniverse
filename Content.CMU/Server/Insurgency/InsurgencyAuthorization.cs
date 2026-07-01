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

    public static bool IsAuthorized(IAdminManager admin, ICommonSession player)
    {
        var data = admin.GetAdminData(player);
        return data != null && data.HasFlag(AuthorizedFlag);
    }
}
