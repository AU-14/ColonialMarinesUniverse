using Content.Client.GameTicking.Managers;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RMC14.Lobby;

public sealed class RMCLobbyUIController : UIController, IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    [UISystemDependency] private readonly ClientGameTicker _gameTicker = default!;
    [UISystemDependency] private readonly JoinXenoSystem _joinXeno = default!;

    private JoinXenoWindow? _joinXenoWindow;
    private LobbyGui? _lobby;

    public override void Initialize()
    {
        SubscribeLocalEvent<BurrowedLarvaChangedEvent>(OnBurrowedLarvaChanged);
    }

    private void OnBurrowedLarvaChanged(ref BurrowedLarvaChangedEvent ev)
    {
        if (_joinXenoWindow is not { IsOpen: true })
            return;

        RefreshWindow(ev.Larva);
    }

    public void OnStateEntered(LobbyState state)
    {
        if (state.Lobby is not { } lobby)
            return;

        _lobby = lobby;
        _lobby.JoinXenoButton.OnPressed += OnJoinXenoPressed;
        _gameTicker.LobbyStatusUpdated += OnLobbyStatusUpdated;
        UpdateJoinXenoButton();
    }

    public void OnStateExited(LobbyState state)
    {
        _gameTicker.LobbyStatusUpdated -= OnLobbyStatusUpdated;

        if (_lobby != null)
        {
            _lobby.JoinXenoButton.OnPressed -= OnJoinXenoPressed;
            _lobby.JoinXenoButton.Visible = false;
            _lobby.ReadyButton.RemoveStyleClass("OpenLeft");
        }

        _joinXenoWindow?.Close();
        _joinXenoWindow = null;
        _lobby = null;
    }

    private void OnJoinXenoPressed(BaseButton.ButtonEventArgs args)
    {
        OpenJoinXenoWindow();
    }

    private void OnLobbyStatusUpdated()
    {
        UpdateJoinXenoButton();
    }

    private void UpdateJoinXenoButton()
    {
        if (_lobby == null)
            return;

        _lobby.JoinXenoButton.Visible = _gameTicker.IsGameStarted;

        if (_gameTicker.IsGameStarted)
            _lobby.ReadyButton.AddStyleClass("OpenLeft");
        else
        {
            _lobby.ReadyButton.RemoveStyleClass("OpenLeft");
            _joinXenoWindow?.Close();
        }
    }

    public void OpenJoinXenoWindow()
    {
        if (_lobby == null || !_gameTicker.IsGameStarted)
            return;

        RefreshWindow(_joinXeno.ClientBurrowedLarva);
        _joinXeno.RequestBurrowedLarvaStatus();
    }

    private void RefreshWindow(int larva)
    {
        if (_joinXenoWindow == null || _joinXenoWindow.Disposed)
        {
            _joinXenoWindow = new JoinXenoWindow();
            _joinXenoWindow.OnClose += () => _joinXenoWindow = null;
            _joinXenoWindow.LarvaButton.OnPressed += _ =>
            {
                _joinXeno.ClientJoinLarva();
                _joinXenoWindow.Close();
            };

            _joinXenoWindow.OpenCentered();
        }

        if (larva == 0)
        {
            _joinXenoWindow.Label.Text = Loc.GetString("rmc-lobby-no-burrowed-larva");
            _joinXenoWindow.Buttons.Visible = false;
        }
        else
        {
            _joinXenoWindow.Label.Text = Loc.GetString("rmc-lobby-burrowed-larva-available");
            _joinXenoWindow.Buttons.Visible = true;
        }
    }
}
