using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Ghost.Widgets;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client.UserInterface.Systems.Ghost;

// TODO hud refactor BEFORE MERGE fix ghost gui being too far up
public sealed partial class GhostUIController : UIController, IOnSystemChanged<GhostSystem>
{
    [Dependency] private IEntityNetworkManager _net = default!;

    [UISystemDependency] private readonly GhostSystem? _system = default;

    private string? _serverTab; // CMU14: tab the server last scoped preview overrides to

    private GhostGui? Gui => UIManager.GetActiveUIWidgetOrNull<GhostGui>();

    public override void Initialize()
    {
        base.Initialize();

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        LoadGui();
    }

    private void OnScreenUnload()
    {
        UnloadGui();
    }

    public void OnSystemLoaded(GhostSystem system)
    {
        system.PlayerRemoved += OnPlayerRemoved;
        system.PlayerUpdated += OnPlayerUpdated;
        system.PlayerAttached += OnPlayerAttached;
        system.PlayerDetached += OnPlayerDetached;
        system.GhostWarpsResponse += OnWarpsResponse;
        system.GhostWarpsReset += OnWarpsReset;
        system.GhostRoleCountUpdated += OnRoleCountUpdated;
    }

    public void OnSystemUnloaded(GhostSystem system)
    {
        system.PlayerRemoved -= OnPlayerRemoved;
        system.PlayerUpdated -= OnPlayerUpdated;
        system.PlayerAttached -= OnPlayerAttached;
        system.PlayerDetached -= OnPlayerDetached;
        system.GhostWarpsResponse -= OnWarpsResponse;
        system.GhostWarpsReset -= OnWarpsReset;
        system.GhostRoleCountUpdated -= OnRoleCountUpdated;
    }

    public void UpdateGui()
    {
        if (Gui == null)
        {
            return;
        }

        Gui.Visible = _system?.IsGhost ?? false;
        Gui.Update(_system?.AvailableGhostRoleCount, _system?.Player?.CanReturnToBody);
    }

    private void OnPlayerRemoved(GhostComponent component)
    {
        Gui?.Hide();
    }

    private void OnPlayerUpdated(GhostComponent component)
    {
        UpdateGui();
    }

    private void OnPlayerAttached(GhostComponent component)
    {
        if (Gui == null)
            return;

        Gui.Visible = true;
        UpdateGui();
    }

    private void OnPlayerDetached()
    {
        Gui?.Hide();
    }

    private void OnWarpsResponse(GhostWarpsResponseEvent msg)
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        _serverTab = msg.Tab; // CMU14
        window.UpdateWarps(msg.Warps);
        window.Populate();
    }

    private void OnWarpsReset()
    {
        Gui?.TargetWindow.ClearWarps(clearSearch: true);
    }

    private void OnRoleCountUpdated(GhostUpdateGhostRoleCountEvent msg)
    {
        UpdateGui();
    }

    private void OnWarpClicked(NetEntity player)
    {
        var msg = new GhostWarpToTargetRequestEvent(player);
        _net.SendSystemNetworkMessage(msg);
    }

    // CMU14 method: only one tab's entities are force-sent at a time; re-request when another tab is opened
    private void OnActiveTabChanged(string? tab)
    {
        if (tab == null || tab == _serverTab)
            return;

        _serverTab = tab;
        _system?.RequestWarps(tab);
    }

    private void OnGhostnadoClicked()
    {
        var msg = new GhostnadoRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    private void OnWarpToRandomClicked()
    {
        var msg = new WarpToRandomRequestEvent();
        _net.SendSystemNetworkMessage(msg);
    }

    public void LoadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed += RequestWarps;
        Gui.ReturnToBodyPressed += ReturnToBody;
        Gui.GhostRolesPressed += GhostRolesPressed;
        Gui.TargetWindow.WarpClicked += OnWarpClicked;
        Gui.TargetWindow.OnClose += OnWarpsClosed;
        Gui.TargetWindow.ActiveTabChanged += OnActiveTabChanged; // CMU14
        Gui.TargetWindow.OnGhostnadoClicked += OnGhostnadoClicked;
        Gui.LateJoinPressed += LateJoinPressed;
        Gui.TargetWindow.OnWarpToRandomClicked += OnWarpToRandomClicked;

        UpdateGui();
    }

    public void UnloadGui()
    {
        if (Gui == null)
            return;

        Gui.RequestWarpsPressed -= RequestWarps;
        Gui.ReturnToBodyPressed -= ReturnToBody;
        Gui.GhostRolesPressed -= GhostRolesPressed;
        Gui.TargetWindow.WarpClicked -= OnWarpClicked;
        Gui.TargetWindow.OnClose -= OnWarpsClosed;
        Gui.TargetWindow.ActiveTabChanged -= OnActiveTabChanged; // CMU14
        Gui.TargetWindow.OnGhostnadoClicked -= OnGhostnadoClicked;
        Gui.TargetWindow.OnWarpToRandomClicked -= OnWarpToRandomClicked;
        Gui.LateJoinPressed -= LateJoinPressed;

        Gui.Hide();
    }

    private void ReturnToBody()
    {
        _system?.ReturnToBody();
    }

    private void RequestWarps()
    {
        if (Gui?.TargetWindow is not { } window)
            return;

        window.ClearWarps();
        window.OpenCentered();
        _serverTab = null; // CMU14: fresh open, the server picks the default tab
        _system?.RequestWarps();
    }

    private void OnWarpsClosed()
    {
        _net.SendSystemNetworkMessage(new GhostWarpsCloseEvent());
        Gui?.TargetWindow.ClearWarps();
    }

    private void GhostRolesPressed()
    {
        _system?.OpenGhostRoles();
    }

    private void LateJoinPressed()
    {
        // Send a network event to request joining the lobby (works for all players)
        _net.SendSystemNetworkMessage(new Content.Shared.GameTicking.TickerJoinLobbyEvent());
    }
}
