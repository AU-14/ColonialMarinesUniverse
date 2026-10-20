using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client._CMU14.Interface;
using Content.Client._CMU14.Lobby;
using Content.Client.Stylesheets;
using Content.Shared.CMU14.Round;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client.CMU14.Round;

public sealed partial class FoFBalanceConfirmUIController : UIController,
    IOnStateEntered<LobbyState>,
    IOnStateExited<LobbyState>
{
    [Dependency] private IClientNetManager _net = default!;

    private FoFBalanceConfirmWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FoFBalanceConfirmEvent>(OnConfirmRequested);
        _net.RegisterNetMessage<FoFBalanceConfirmMessage>();
    }

    public void OnStateEntered(LobbyState state) { }
    public void OnStateExited(LobbyState state)
        => CloseWindow();

    private void OnConfirmRequested(FoFBalanceConfirmEvent ev, EntitySessionEventArgs args)
    {
        CloseWindow();

        _window = new FoFBalanceConfirmWindow(ev);
        _window.Confirmed += OnConfirmed;
        _window.OpenCentered();
    }

    private void OnConfirmed()
    {
        _net.ClientSendMessage(new FoFBalanceConfirmMessage());
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

public sealed class FoFBalanceConfirmWindow : DefaultWindow
{
    [Dependency] private readonly IStylesheetManager _stylesheetManager = default!;

    public event Action? Confirmed;

    private readonly CmuChoiceCard _join;
    private readonly CmuChoiceCard _stay;

    public FoFBalanceConfirmWindow(FoFBalanceConfirmEvent ev)
    {
        IoCManager.InjectDependencies(this);

        Title = Loc.GetString("fof-balance-confirm-title");
        MinWidth = CmuPanelMetrics.ChoiceWindowWidth;

        var target = ev.Govfor < ev.Opfor ? "GOVFOR" : "OPFOR";

        // The join card wears the faction it is sending you to; the buttons alternate sides like
        // the join-round window's cards so the pair reads in the same language.
        _join = new CmuChoiceCard(
            Loc.GetString("fof-balance-confirm-join", ("target", target)),
            Loc.GetString("fof-balance-confirm-join-desc", ("target", target), ("max", ev.MaxGap)),
            target == "GOVFOR" ? JoinRoundWindow.GovforPalette : JoinRoundWindow.OpforPalette,
            buttonOnLeft: true);

        _stay = new CmuChoiceCard(
            Loc.GetString("fof-balance-confirm-stay"),
            Loc.GetString("fof-balance-confirm-stay-desc"),
            CmuChoiceCard.Terminal,
            buttonOnLeft: false);

        _join.Button.OnPressed += _ => Confirmed?.Invoke();
        _stay.Button.OnPressed += _ => Close();

        var prompt = new Label
        {
            Text = Loc.GetString("fof-balance-confirm-prompt", ("govfor", ev.Govfor), ("opfor", ev.Opfor)),
            StyleClasses = { "CrtDimText" },
        };

        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = CmuPanelMetrics.PanelPadding,
            SeparationOverride = CmuPanelMetrics.RowSeparation,
            Children = { prompt, _join, _stay },
        };

        ContentsContainer.AddChild(new PanelContainer
        {
            StyleClasses = { "CrtPanelFill" },
            HorizontalExpand = true,
            VerticalExpand = true,
            Children = { stack },
        });

        ApplyCrtPalette();
    }

    // The CRT theme walk hands every control the shared boxes, so the cards' own palettes go on
    // after it or they get overwritten, same as the join-round window.
    private void ApplyCrtPalette()
    {
        Stylesheet = _stylesheetManager.SheetNano;
        CrtLobbyTheme.ApplyWindow(this, useCrtTypography: true);
        _join.ApplyPalette();
        _stay.ApplyPalette();
    }
}
