using Content.Client.Gameplay;
using Content.Client.Lobby;
using Content.Client.Lobby.UI;
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.VoiceChat;

/// <summary>
/// Keeps the speaker list on the active screen: over the game viewport in a round, beside the lobby panel in the lobby.
/// </summary>
public sealed partial class CMUVoiceSpeakerListUIController : UIController,
    IOnStateEntered<GameplayState>, IOnStateExited<GameplayState>,
    IOnStateEntered<LobbyState>, IOnStateExited<LobbyState>
{
    private const float Margin = 10f;
    private const float GameplayBottom = 110f;

    [Dependency] private IConfigurationManager _cfg = default!;
    [UISystemDependency] private readonly CMUVoiceChatSystem? _voice = default;

    private CMUVoiceSpeakerList? _list;
    private UIScreen? _attachedScreen;
    private bool _active;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();
        _cfg.OnValueChanged(CCVars.CMUVoiceChatSpeakerList, OnEnabledChanged, true);
    }

    public void OnStateEntered(GameplayState state)
    {
        _active = true;
    }

    public void OnStateExited(GameplayState state)
    {
        _active = false;
        Detach();
    }

    public void OnStateEntered(LobbyState state)
    {
        _active = true;
    }

    public void OnStateExited(LobbyState state)
    {
        _active = false;
        Detach();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_active || !_enabled || _voice == null || !_cfg.GetCVar(CCVars.CMUVoiceChatEnabled))
            return;

        if (UIManager.ActiveScreen != _attachedScreen || _list?.Parent == null)
            Attach(UIManager.ActiveScreen);

        if (_list == null)
            return;

        if (_attachedScreen is LobbyGui lobby)
            _list.SetInset(lobby.RightSide.Width + Margin, Margin);
        else
            _list.SetInset(Margin, GameplayBottom);

        _list.Update(args.DeltaSeconds, _voice);
    }

    private void OnEnabledChanged(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
            Detach();
    }

    private void Attach(UIScreen? screen)
    {
        Detach();
        if (screen == null)
            return;

        LayoutContainer container = screen;
        if (screen.GetWidget<MainViewport>() is { Parent: LayoutContainer viewportContainer })
            container = viewportContainer;

        _list ??= new CMUVoiceSpeakerList();
        container.AddChild(_list);
        LayoutContainer.SetAnchorPreset(_list, LayoutContainer.LayoutPreset.Wide);
        _attachedScreen = screen;
    }

    private void Detach()
    {
        _list?.Clear();
        _list?.Orphan();
        _attachedScreen = null;
    }
}
