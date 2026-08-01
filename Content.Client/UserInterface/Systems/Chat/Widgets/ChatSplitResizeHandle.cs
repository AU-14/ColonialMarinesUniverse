using System;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed class ChatSplitResizeHandle : PanelContainer
{
    private bool _dragging;

    public event Action<GUIMouseMoveEventArgs>? OnDragged;
    public event Action? OnDragEnded;

    public ChatSplitResizeHandle()
    {
        MouseFilter = MouseFilterMode.Stop;
        DefaultCursorShape = CursorShape.VResize;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = true;
        args.Handle();
    }

    protected override void KeyBindUp(GUIBoundKeyEventArgs args)
    {
        base.KeyBindUp(args);
        if (args.Function != EngineKeyFunctions.UIClick || !_dragging)
            return;

        _dragging = false;
        OnDragEnded?.Invoke();
        args.Handle();
    }

    protected override void MouseMove(GUIMouseMoveEventArgs args)
    {
        base.MouseMove(args);
        if (_dragging)
            OnDragged?.Invoke(args);
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        if (!_dragging)
            DefaultCursorShape = CursorShape.VResize;
    }
}
