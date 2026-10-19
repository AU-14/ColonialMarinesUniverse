using System.Linq;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Administration.Events;
using Robust.Shared.Player;

// ReSharper disable once CheckNamespace
namespace Content.Server.Administration.Systems;

// CMU14 class: player list for in-game admins. They are inactive, so the normal admin broadcasts skip them, but
// the AHelp window lists its channels from this list. They get a copy with antag and role details removed.
public sealed partial class AdminSystem
{
    /// <summary>
    /// Sends <paramref name="session"/> the full player list without antag details, if it is an in-game admin.
    /// </summary>
    public void CMUSendIngamePlayerList(ICommonSession session)
    {
        if (CMUIsIngameAhelpAdmin(session))
            RaiseNetworkEvent(new FullPlayerListEvent { PlayersInfo = _playerList.Values.Select(CMUStrip).ToList() }, session.Channel);
    }

    /// <summary>
    /// Mirrors a full player list broadcast to in-game admins, without antag details.
    /// </summary>
    private void CMURelayToIngameAdmins(FullPlayerListEvent ev)
    {
        FullPlayerListEvent? stripped = null;
        foreach (var session in CMUIngameAhelpAdmins())
        {
            stripped ??= new FullPlayerListEvent { PlayersInfo = ev.PlayersInfo.Select(CMUStrip).ToList() };
            RaiseNetworkEvent(stripped, session.Channel);
        }
    }

    /// <summary>
    /// Mirrors a single player update to in-game admins, without antag details.
    /// </summary>
    private void CMURelayToIngameAdmins(PlayerInfoChangedEvent ev)
    {
        if (ev.PlayerInfo is not { } info)
            return;

        PlayerInfoChangedEvent? stripped = null;
        foreach (var session in CMUIngameAhelpAdmins())
        {
            stripped ??= new PlayerInfoChangedEvent { PlayerInfo = CMUStrip(info) };
            RaiseNetworkEvent(stripped, session.Channel);
        }
    }

    private IEnumerable<ICommonSession> CMUIngameAhelpAdmins()
    {
        return ((AdminManager) _adminManager).CMUAhelpAdmins.Where(CMUIsIngameAhelpAdmin);
    }

    private bool CMUIsIngameAhelpAdmin(ICommonSession session)
    {
        var manager = (AdminManager) _adminManager;
        return manager.CMUIsIngameAdmin(session) && manager.GetAdminData(session) == null && manager.CMUHasAhelp(session);
    }

    /// <summary>
    /// The player entry an in-game admin may see: no antag flag, role type, subtype, icon, or role sort weight.
    /// </summary>
    public static PlayerInfo CMUStrip(PlayerInfo info)
    {
        return info with { Antag = false, RoleProto = null, Subtype = null, AntagIcon = null, SortWeight = 0 };
    }
}
