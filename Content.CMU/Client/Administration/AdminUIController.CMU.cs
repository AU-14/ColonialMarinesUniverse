using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

// ReSharper disable once CheckNamespace
namespace Content.Client.UserInterface.Systems.Admin;

// CMU14 class: lobby Admin button, so admins can open the admin menu without joining the round.
public sealed partial class AdminUIController : IOnStateExited<LobbyState>
{
    private Button? LobbyAdminButton => (UIManager.ActiveScreen as LobbyGui)?.AdminButton;

    // The screen is unloaded before OnStateExited runs, so keep the wired button to unwire it there.
    private Button? _cmuWiredLobbyAdminButton;

    /// <summary>
    /// Called from <see cref="OnStateEntered(LobbyState)"/>. The lobby screen is loaded before the
    /// state starts up, so the button exists by now (same timing AHelpUIController relies on).
    /// </summary>
    private void CMULobbyEntered()
    {
        CMUUnwireLobbyAdminButton();

        if (LobbyAdminButton is not { } button)
            return;

        button.OnPressed += AdminButtonPressed;
        button.Pressed = _window?.IsOpen ?? false;
        _cmuWiredLobbyAdminButton = button;
    }

    public void OnStateExited(LobbyState state)
    {
        CMUUnwireLobbyAdminButton();
    }

    private void CMUUnwireLobbyAdminButton()
    {
        if (_cmuWiredLobbyAdminButton == null)
            return;

        _cmuWiredLobbyAdminButton.OnPressed -= AdminButtonPressed;
        _cmuWiredLobbyAdminButton = null;
    }

    /// <summary>
    /// Called from <see cref="AdminStatusUpdated"/>.
    /// </summary>
    private void CMUUpdateLobbyAdminButton()
    {
        if (LobbyAdminButton is { } button)
            button.Visible = _conGroups.CanAdminMenu();
    }

    /// <summary>
    /// Called when the admin menu opens, closes or is disposed.
    /// </summary>
    private void CMUSetLobbyAdminButtonPressed(bool pressed)
    {
        if (LobbyAdminButton is { } button)
            button.Pressed = pressed;
    }
}
