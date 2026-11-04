using Content.Client.CMU14.Interface;
using Content.Client.CMU14.Lobby;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Squads.Foxtrot;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client.CMU14.Squads.Foxtrot;

/// <summary>
/// Asks a late joiner whether to take a slot only the Foxtrot squad has left.
/// </summary>
public sealed partial class CMUFoxtrotSquadConfirmUIController : UIController,
    IOnStateEntered<LobbyState>,
    IOnStateExited<LobbyState>
{
    [Dependency] private IClientNetManager _net = default!;

    private CMUFoxtrotSquadConfirmWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CMUFoxtrotSquadConfirmEvent>(OnConfirmRequested);
        _net.RegisterNetMessage<CMUFoxtrotSquadConfirmMessage>();
    }

    public void OnStateEntered(LobbyState state) { }

    public void OnStateExited(LobbyState state)
        => CloseWindow();

    private void OnConfirmRequested(CMUFoxtrotSquadConfirmEvent ev, EntitySessionEventArgs args)
    {
        CloseWindow();

        _window = new CMUFoxtrotSquadConfirmWindow(ev);
        _window.Confirmed += OnConfirmed;
        _window.OpenCentered();
    }

    private void OnConfirmed()
    {
        _net.ClientSendMessage(new CMUFoxtrotSquadConfirmMessage());
        CloseWindow();
    }

    private void CloseWindow()
    {
        if (_window == null)
            return;

        _window.Confirmed -= OnConfirmed;
        _window.Close();
        _window = null;
    }
}

public sealed partial class CMUFoxtrotSquadConfirmWindow : DefaultWindow
{
    [Dependency] private IStylesheetManager _stylesheetManager = default!;

    public event Action? Confirmed;

    private readonly CmuChoiceCard _join;
    private readonly CmuChoiceCard _cancel;

    public CMUFoxtrotSquadConfirmWindow(CMUFoxtrotSquadConfirmEvent ev)
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("cmu-foxtrot-squad-confirm-title", ("squad", ev.SquadName));
        MinWidth = CmuPanelMetrics.ChoiceWindowWidth;

        _join = new CmuChoiceCard(
            Loc.GetString("cmu-foxtrot-squad-confirm-join", ("squad", ev.SquadName)),
            Loc.GetString("cmu-foxtrot-squad-confirm-join-desc", ("squad", ev.SquadName), ("job", ev.JobName)),
            CmuChoiceCard.Terminal,
            buttonOnLeft: true);

        _cancel = new CmuChoiceCard(
            Loc.GetString("cmu-foxtrot-squad-confirm-cancel"),
            Loc.GetString("cmu-foxtrot-squad-confirm-cancel-desc"),
            CmuChoiceCard.Terminal,
            buttonOnLeft: false);

        _join.Button.OnPressed += _ => Confirmed?.Invoke();
        _cancel.Button.OnPressed += _ => Close();

        var prompt = new Label
        {
            Text = Loc.GetString("cmu-foxtrot-squad-confirm-prompt", ("squad", ev.SquadName), ("job", ev.JobName)),
            StyleClasses = { "CrtDimText" },
        };

        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = CmuPanelMetrics.PanelPadding,
            SeparationOverride = CmuPanelMetrics.RowSeparation,
            Children = { prompt, _join, _cancel },
        };

        ContentsContainer.AddChild(new PanelContainer
        {
            StyleClasses = { "CrtPanelFill" },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { stack },
        });

        Stylesheet = _stylesheetManager.SheetNano;
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
        _join.ApplyPalette();
        _cancel.ApplyPalette();
    }
}
