using System.Linq;
using Content.Shared.Administration;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Managers;

// CMU14 class: in-game admin mode, toggled by CMUIngameAdminCommand. The admin is made inactive like a deadmin,
// so every admin tool, privilege and admin-only check switches off at once, and only the AHelp is given back:
// AHelp code asks CMUGetAhelpAdminData instead of GetAdminData. Allowing one function back is safer than
// trying to strip every tool, because many checks only ask "is this an active admin".
public sealed partial class AdminManager
{
    // Kept by user id so the mode survives a relog or a rank reload until the admin leaves it.
    private readonly HashSet<NetUserId> _cmuIngameAdmins = new();

    /// <summary>
    /// Raised after a session's admin status was sent to its client, which happens on every change of
    /// in-game admin mode, login, rank reload, deadmin and readmin.
    /// </summary>
    public event Action<ICommonSession>? CMUAdminStatusSent;

    public bool CMUIsIngameAdmin(ICommonSession session)
    {
        return _cmuIngameAdmins.Contains(session.UserId);
    }

    /// <summary>
    /// Enters or leaves in-game admin mode. Entering needs an active admin. Returns false when nothing changed.
    /// </summary>
    public bool CMUSetIngameAdmin(ICommonSession session, bool enabled)
    {
        if (!_admins.TryGetValue(session, out var reg))
            throw new ArgumentException($"Player {session} is not an admin");

        if (enabled == _cmuIngameAdmins.Contains(session.UserId))
            return false;

        if (enabled && !reg.Data.Active)
            return false;

        if (enabled)
            _cmuIngameAdmins.Add(session.UserId);
        else
            _cmuIngameAdmins.Remove(session.UserId);

        reg.Data.Active = !enabled;

        var announcement = Loc.GetString(enabled ? "cmu-ingameadmin-admin-announce-on" : "cmu-ingameadmin-admin-announce-off",
            ("name", session.Name));
        if (reg.Data.Stealth)
            _chat.SendAdminAnnouncement(announcement, flagWhitelist: AdminFlags.Stealth);
        else
            _chat.SendAdminAnnouncement(announcement);

        _chat.DispatchServerMessage(session,
            Loc.GetString(enabled ? "cmu-ingameadmin-self-on" : "cmu-ingameadmin-self-off"));

        SendPermsChangedEvent(session);
        UpdateAdminStatus(session);
        return true;
    }

    /// <summary>
    /// The admin data AHelp code should use for <paramref name="session"/>: the normal active data, or for an
    /// in-game admin who holds <see cref="AdminFlags.Adminhelp"/> an active copy only AHelp code ever sees.
    /// </summary>
    public AdminData? CMUGetAhelpAdminData(ICommonSession session)
    {
        if (GetAdminData(session) is { } active)
            return active;

        if (!_cmuIngameAdmins.Contains(session.UserId) ||
            !_admins.TryGetValue(session, out var reg) ||
            !reg.Data.HasFlag(AdminFlags.Adminhelp, includeDeAdmin: true))
        {
            return null;
        }

        return new AdminData
        {
            Active = true,
            Stealth = reg.Data.Stealth,
            Title = reg.Data.Title,
            Flags = reg.Data.Flags,
        };
    }

    /// <summary>
    /// True when <paramref name="session"/> may handle AHelps, in-game admins included.
    /// </summary>
    public bool CMUHasAhelp(ICommonSession session)
    {
        return CMUGetAhelpAdminData(session)?.HasFlag(AdminFlags.Adminhelp) ?? false;
    }

    /// <summary>
    /// Every session that may handle AHelps: active Adminhelp admins and in-game admins.
    /// </summary>
    public IEnumerable<ICommonSession> CMUAhelpAdmins => _admins.Keys.Where(CMUHasAhelp);

    /// <summary>
    /// Called wherever a session's admin data is (re)loaded, before permission changes are sent, so a rank
    /// reload or a relog keeps an in-game admin inactive.
    /// </summary>
    private void CMUApplyIngameAdmin(ICommonSession session)
    {
        if (_cmuIngameAdmins.Contains(session.UserId) && _admins.TryGetValue(session, out var reg))
            reg.Data.Active = false;
    }

    /// <summary>
    /// Called at the end of <see cref="UpdateAdminStatus"/>. An in-game admin who is active again was readminned,
    /// which ends the mode.
    /// </summary>
    private void CMUOnAdminStatusSent(ICommonSession session)
    {
        if (_admins.TryGetValue(session, out var reg))
        {
            if (reg.Data.Active)
                _cmuIngameAdmins.Remove(session.UserId);
        }
        else if (session.Status == SessionStatus.InGame)
        {
            // Lost admin entirely (rank removed). The status sent while connecting, before the admin data has
            // loaded, also has no entry; that one must keep the mode for the relog.
            _cmuIngameAdmins.Remove(session.UserId);
        }

        CMUAdminStatusSent?.Invoke(session);
    }
}
