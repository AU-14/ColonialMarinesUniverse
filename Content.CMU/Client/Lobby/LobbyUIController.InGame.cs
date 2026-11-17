using Content.Client.Gameplay;
using Content.Shared.Ghost.Components;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Lobby;

/// <summary>
///     Lets observers open the lobby character setup in a window mid-round, so they can edit
///     their characters while waiting for the next round.
/// </summary>
public sealed partial class LobbyUIController : IOnStateExited<GameplayState>
{
    private DefaultWindow? _inGameSetupWindow;

    /// <summary>
    ///     Character setup is offered in game only to ghosts, or to players with no body at all.
    /// </summary>
    public bool CanOpenInGameCharacterSetup()
    {
        if (_stateManager.CurrentState is not GameplayState)
            return false;

        var attached = _playerManager.LocalEntity;
        return attached == null || EntityManager.HasComponent<GhostComponent>(attached.Value);
    }

    public void OpenInGameCharacterSetup()
    {
        if (!CanOpenInGameCharacterSetup())
            return;

        if (_inGameSetupWindow is { IsOpen: true })
        {
            _inGameSetupWindow.MoveToFront();
            return;
        }

        // Drop any editor left over from an earlier window so EnsureGui builds a fresh one.
        TearDownCharacterSetup();

        _inGameSetupWindow = new DefaultWindow
        {
            Title = Loc.GetString("cmu-ui-escape-character-setup"),
            MinSize = new(1100, 750),
            SetSize = new(1400, 900),
            Resizable = true,
        };

        var (characterGui, _) = EnsureGui();
        _inGameSetupWindow.Contents.AddChild(characterGui);
        _inGameSetupWindow.OnClose += OnInGameSetupWindowClosed;

        ReloadCharacterSetup();
        _inGameSetupWindow.OpenCentered();
    }

    /// <summary>
    ///     Called when the editor's own close button finishes (after any save prompt).
    /// </summary>
    private void CloseInGameCharacterSetup()
    {
        _inGameSetupWindow?.Close();
    }

    private void OnInGameSetupWindowClosed()
    {
        if (_inGameSetupWindow != null)
            _inGameSetupWindow.OnClose -= OnInGameSetupWindowClosed;

        _inGameSetupWindow = null;
        _savePanel?.Close();
        TearDownCharacterSetup();
    }

    public void OnStateExited(GameplayState state)
    {
        // Round ended or disconnected: the lobby rebuilds its own editor when it opens.
        CloseInGameCharacterSetup();
    }

    private void TearDownCharacterSetup()
    {
        _characterSetup?.Shutdown();
        _profileEditor?.Orphan();
        _characterSetup?.Orphan();
        _characterSetup = null;
        _profileEditor = null;
    }
}
