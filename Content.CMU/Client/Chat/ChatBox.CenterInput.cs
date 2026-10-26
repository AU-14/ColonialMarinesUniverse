using System.Numerics;
using Content.Client.CMU14.Interface;
using Content.Client.UserInterface.Controls;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

/// <summary>
/// CM13-style centered chat input: when the chat keybind focuses the main chat box, its input row
/// moves to the middle of the screen, and goes back once the message is sent or cancelled.
/// </summary>
public partial class ChatBox
{
    private PanelContainer? _cmuCenterHolder;
    private Control? _cmuInputHome;
    private Control? _cmuInputPlaceholder;
    private int _cmuInputHomeIndex;

    private bool CMUInputCentered => _cmuInputHome != null;

    private void CMUCenterInput()
    {
        if (!Main || CMUInputCentered || !_config.GetCVar(CCVars.ChatCenterInput))
            return;

        if (ChatInput.Parent is not { } home || ChatInput.Input.HasKeyboardFocus())
            return;

        // Only in game: the lobby and menus have no game view and keep the chat box as it is.
        if (UserInterfaceManager.ActiveScreen?.GetWidget<MainViewport>() is not { } viewport)
            return;

        var root = UserInterfaceManager.WindowRoot;
        _cmuCenterHolder ??= new PanelContainer
        {
            MinWidth = 600,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = CrtTerminalPalette.Surface0.WithAlpha(0.95f),
                BorderColor = CrtTerminalPalette.Line,
                BorderThickness = new Thickness(1),
            },
        };

        _cmuInputHome = home;
        _cmuInputHomeIndex = ChatInput.GetPositionInParent();
        // Hold the input's spot so the chat log doesn't grow into it while it's away.
        _cmuInputPlaceholder = new Control
        {
            MinHeight = ChatInput.Size.Y,
            HorizontalExpand = ChatInput.HorizontalExpand,
            Margin = ChatInput.Margin,
        };
        home.RemoveChild(ChatInput);
        home.AddChild(_cmuInputPlaceholder);
        _cmuInputPlaceholder.SetPositionInParent(_cmuInputHomeIndex);
        _cmuCenterHolder.AddChild(ChatInput);

        root.AddChild(_cmuCenterHolder);

        // Centre on the game view (not the whole window, which also holds the chat and HUD).
        _cmuCenterHolder.Measure(new Vector2(float.PositiveInfinity, float.PositiveInfinity));
        var size = _cmuCenterHolder.DesiredSize;
        var center = viewport.GlobalPosition + viewport.Size / 2f;
        LayoutContainer.SetPosition(_cmuCenterHolder, (center - size / 2f).Floored());
    }

    private void CMURestoreInput()
    {
        if (_cmuInputHome is not { } home)
            return;

        _cmuInputHome = null;
        _cmuCenterHolder?.RemoveChild(ChatInput);
        _cmuCenterHolder?.Orphan();
        _cmuInputPlaceholder?.Orphan();
        _cmuInputPlaceholder = null;

        home.AddChild(ChatInput);
        if (_cmuInputHomeIndex < home.ChildCount)
            ChatInput.SetPositionInParent(_cmuInputHomeIndex);
    }
}
