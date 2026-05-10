using System;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChatTabButton : Button
{
    private const float DragStartDistanceSquared = 16f;

    public readonly string TabId;

    private bool _canDrag = true;
    private bool _clickHeld;
    private bool _dragActive;
    private Vector2 _dragStartPosition;

    public bool CanDrag
    {
        get => _canDrag;
        set
        {
            _canDrag = value;
            DefaultCursorShape = value ? CursorShape.Hand : CursorShape.Arrow;
        }
    }

    public event Action<string>? DragStarted;
    public event Action<string>? DragEntered;
    public event Action<string>? DragExited;
    public event Action<string>? DragEnded;

    public ChatTabButton(string tabId)
    {
        TabId = tabId;
        DefaultCursorShape = CursorShape.Hand;
    }

    public void SetDragVisualState(bool dragging, bool dropTarget)
    {
        StyleBoxOverride = (dragging, dropTarget) switch
        {
            (true, _) => CreateDragStyle(Color.FromHex("#173523"), Color.FromHex("#7fd89a"), new Thickness(1)),
            (_, true) => CreateDragStyle(Color.FromHex("#1b2638"), Color.FromHex("#82b7ff"), new Thickness(2)),
            _ => null
        };
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);

        if (args.Function == EngineKeyFunctions.UIClick && CanDrag)
        {
            _clickHeld = true;
            _dragActive = false;
            _dragStartPosition = args.PointerLocation.Position / UIScale;
        }
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);

        if (args.Function == EngineKeyFunctions.UIClick && CanDrag && _dragActive)
            DragEnded?.Invoke(TabId);

        if (args.Function == EngineKeyFunctions.UIClick)
        {
            _clickHeld = false;
            _dragActive = false;
        }
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);

        if (!CanDrag || !_clickHeld || _dragActive)
            return;

        if ((args.GlobalPosition - _dragStartPosition).LengthSquared() < DragStartDistanceSquared)
            return;

        _dragActive = true;
        DragStarted?.Invoke(TabId);
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();

        DragEntered?.Invoke(TabId);
    }

    protected override void MouseExited()
    {
        base.MouseExited();

        DragExited?.Invoke(TabId);
    }

    protected override void ControlFocusExited()
    {
        base.ControlFocusExited();

        if (CanDrag && _dragActive)
            DragEnded?.Invoke(TabId);

        _clickHeld = false;
        _dragActive = false;
    }

    private static StyleBoxFlat CreateDragStyle(Color background, Color border, Thickness borderThickness)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = borderThickness,
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3
        };
    }
}
