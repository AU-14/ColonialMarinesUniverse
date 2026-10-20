using Content.Client.Lobby.UI;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Bwoink;
using Content.Shared.CMU14.BugHelp;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using Robust.Shared.Network;

namespace Content.Client.CMU14.BugHelp;

/// <summary>
/// Owns the bug report window: player-bound, so a plain UI controller over raw net messages —
/// the same shape as the mentor help controller, no BoundUserInterface involved.
/// </summary>
public sealed class BugHelpUIController : UIController
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly AHelpUIController _aHelp = default!;

    private BugHelpWindow? _window;

    public override void Initialize()
    {
        _net.RegisterNetMessage<BugHelpOpenMsg>(OnOpen);
        _net.RegisterNetMessage<BugHelpSubmitMsg>();
    }

    private void OnOpen(BugHelpOpenMsg message)
    {
        OpenWindow();
    }

    public void OpenWindow()
    {
        if (_window is { IsOpen: true })
            return;

        _window = new BugHelpWindow();
        _window.Stylesheet = IoCManager.Resolve<IStylesheetManager>().SheetNano;
        CrtLobbyTheme.ApplyWindow(_window, useCrtTypography: false);
        _window.OnClose += () => _window = null;
        _window.TriageOpenButton.OnPressed += _ =>
        {
            _aHelp.Open();
            _window.Close();
        };
        _window.OnSubmit += window =>
        {
            var submission = window.Collect();
            _net.ClientSendMessage(new BugHelpSubmitMsg
            {
                WhatHappened = submission.WhatHappened,
                Expected = submission.Expected,
                Steps = submission.Steps,
                ItemQuery = submission.ItemQuery,
                ExamineText = submission.ExamineText,
            });
            window.Close();
        };
        _window.OpenCentered();
    }
}
