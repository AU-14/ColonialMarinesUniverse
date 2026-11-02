using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Shared.CCVar;
using Robust.Client.Console;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Configuration;
using Robust.Shared.Network;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// Shows the voice chat explainer once per server join, on the first lobby or game screen,
/// while voice chat is enabled on the server and the player hasn't turned the explainer off.
/// </summary>
public sealed partial class CMUVoiceChatIntroUIController : UIController,
    IOnStateEntered<LobbyState>,
    IOnStateEntered<GameplayState>
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IClientConsoleHost _console = default!;

    private CMUVoiceChatIntroWindow? _window;
    private bool _shownThisConnection;

    public override void Initialize()
    {
        base.Initialize();
        _net.Disconnect += (_, _) =>
        {
            _shownThisConnection = false;
            CloseWindow();
        };
    }

    public void OnStateEntered(LobbyState state)
    {
        TryShow();
    }

    public void OnStateEntered(GameplayState state)
    {
        TryShow();
    }

    /// <summary>
    /// Opens the explainer if this connection hasn't shown it yet and it is wanted.
    /// </summary>
    public void TryShow()
    {
        if (_shownThisConnection ||
            !_cfg.GetCVar(CCVars.CMUVoiceChatEnabled) ||
            !_cfg.GetCVar(CCVars.CMUVoiceChatShowIntro))
        {
            return;
        }

        _shownThisConnection = true;
        _window = new CMUVoiceChatIntroWindow();
        _window.OpenPageRequested += OnOpenPage;
        _window.OnClose += OnWindowClosed;
        _window.OpenCentered();
    }

    private void OnOpenPage()
    {
        _console.ExecuteCommand("voicechat");
        CloseWindow();
    }

    private void OnWindowClosed()
    {
        if (_window == null)
            return;

        if (_window.DontShowAgain)
        {
            _cfg.SetCVar(CCVars.CMUVoiceChatShowIntro, false);
            _cfg.SaveToFile();
        }

        _window.OpenPageRequested -= OnOpenPage;
        _window.OnClose -= OnWindowClosed;
        _window = null;
    }

    private void CloseWindow()
    {
        _window?.Close();
    }
}
