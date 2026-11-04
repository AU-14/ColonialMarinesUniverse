using System.Linq;
using Content.Shared.Administration;
using Robust.Shared.Player;

namespace Content.Server.Administration.Managers;

public static class CMUAdminFlagExtensions
{
    /// <summary>
    ///     Online, active admins holding every one of these flags. Use this rather than ActiveAdmins when sending
    ///     something only some ranks should get.
    /// </summary>
    public static IEnumerable<ICommonSession> ActiveAdminsWithFlag(this IAdminManager admins, AdminFlags flags)
    {
        return admins.AllAdmins.Where(session => admins.HasAdminFlag(session, flags));
    }
}
