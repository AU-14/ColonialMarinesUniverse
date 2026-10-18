using Content.Server.Administration.Managers;
using Content.Server.Administration.Systems;
using Content.Shared.CMU14.Administration;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Administration;

/// <summary>
/// Keeps an admin's client in step with in-game admin mode: whether its AHelp window stays the admin one, and the
/// player list (without antag details) that window lists its channels from.
/// </summary>
public sealed class CMUIngameAdminSystem : EntitySystem
{
    [Dependency] private IAdminManager _adminManager = default!;
    [Dependency] private AdminSystem _admin = default!;

    // Users whose client was last told it is an in-game admin, so it is told when that ends even if the
    // session is no longer an admin at all.
    private readonly HashSet<NetUserId> _toldIngame = new();

    private AdminManager Manager => (AdminManager) _adminManager;

    public override void Initialize()
    {
        base.Initialize();
        Manager.CMUAdminStatusSent += OnAdminStatusSent;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Manager.CMUAdminStatusSent -= OnAdminStatusSent;
    }

    private void OnAdminStatusSent(ICommonSession session)
    {
        var ingame = Manager.CMUIsIngameAdmin(session) && Manager.CMUHasAhelp(session);
        var wasTold = _toldIngame.Contains(session.UserId);

        // Only admins, or a client that still believes it is one, need to hear about the mode.
        if (!ingame && !wasTold && _adminManager.GetAdminData(session, includeDeAdmin: true) == null)
            return;

        if (ingame)
            _toldIngame.Add(session.UserId);
        else
            _toldIngame.Remove(session.UserId);

        RaiseNetworkEvent(new CMUIngameAdminStateEvent(ingame), session.Channel);

        // Entering the mode cleared the list (the admin is no longer active); give back the stripped one.
        if (ingame)
            _admin.CMUSendIngamePlayerList(session);
    }
}
