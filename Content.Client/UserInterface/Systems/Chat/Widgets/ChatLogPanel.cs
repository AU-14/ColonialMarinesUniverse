using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed class ChatLogPanel : PanelContainer
{
    public const int MaxEntries = 2500;
    private const float BottomTolerance = 12f;
    private const float ScrollDirectionTolerance = 1f;

    private readonly ChatScrollContainer _scroll;
    private readonly BoxContainer _rows;
    private readonly Button _scrollToLatest;
    private bool _isAtBottom = true;
    private bool _followingBottom = true;
    private int _pendingScrollToBottomFrames;
    private int _pendingLayoutRefreshFrames;
    private float _lastLayoutWidth = -1f;
    private float _lastScrollTarget;

    public int EntryCount => _rows.ChildCount;

    public ChatLogPanel()
    {
        HorizontalExpand = true;
        VerticalExpand = true;
        PanelOverride = new StyleBoxEmpty();

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        AddChild(root);

        _scroll = new ChatScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
            ReserveScrollbarSpace = true
        };
        _scroll.OnUserMouseWheel += OnUserMouseWheel;
        _scroll.OnScrolled += UpdateScrollState;
        root.AddChild(_scroll);

        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
            HorizontalExpand = true,
            VerticalExpand = false
        };
        _scroll.AddChild(_rows);

        _scrollToLatest = new Button
        {
            Text = "Scroll to latest",
            Visible = false,
            HorizontalAlignment = HAlignment.Center,
            MinWidth = 150,
            StyleClasses = { OutputPanel.StyleClassOutputPanelScrollDownButton }
        };
        _scrollToLatest.OnPressed += _ => ScrollToBottom();
        root.AddChild(_scrollToLatest);
    }

    public ChatMessageRow AddMessage(ChatMessage message, FormattedMessage formatted, Color color, Color? accentOverride = null, int? fontSize = null)
    {
        var row = new ChatMessageRow(message, formatted, color, accentOverride, fontSize);
        _rows.AddChild(row);

        while (_rows.ChildCount > MaxEntries)
        {
            _rows.RemoveChild(0);
        }

        if (_followingBottom || _isAtBottom)
            QueueScrollToBottom();
        else
            _scrollToLatest.Visible = true;

        return row;
    }

    public void Clear()
    {
        while (_rows.ChildCount > 0)
        {
            _rows.RemoveChild(0);
        }

        _isAtBottom = true;
        _scrollToLatest.Visible = false;
        QueueScrollToBottom();
        QueueLayoutRefresh();
    }

    public void ScrollToBottom()
    {
        _isAtBottom = true;
        _followingBottom = true;
        _scrollToLatest.Visible = false;
        QueueScrollToBottom();
    }

    public void RefreshLayout(bool forceScrollToBottom = false)
    {
        foreach (var child in _rows.Children)
        {
            if (child is ChatMessageRow row)
                row.RefreshLayout();
            else
                child.InvalidateMeasure();
        }

        _rows.InvalidateMeasure();
        _scroll.InvalidateMeasure();
        InvalidateMeasure();

        if (forceScrollToBottom || _followingBottom || _isAtBottom)
            ScrollToBottom();
        else
            UpdateScrollState();
    }

    protected override void Resized()
    {
        base.Resized();
        QueueLayoutRefresh();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Width > 0 && MathF.Abs(Width - _lastLayoutWidth) > 0.5f)
        {
            _lastLayoutWidth = Width;
            QueueLayoutRefresh();
        }

        if (_pendingLayoutRefreshFrames > 0)
        {
            RefreshLayout();
            _pendingLayoutRefreshFrames--;
        }

        if (_pendingScrollToBottomFrames <= 0)
            return;

        _scroll.VScrollTarget = float.MaxValue;
        _lastScrollTarget = _scroll.VScrollTarget;
        _scrollToLatest.Visible = false;
        _pendingScrollToBottomFrames--;
    }

    private void OnUserMouseWheel(float deltaY, float previousTarget, float currentTarget)
    {
        if (deltaY <= 0 || currentTarget >= previousTarget - ScrollDirectionTolerance)
            return;

        StopFollowingBottom();
    }

    private void QueueScrollToBottom()
    {
        _isAtBottom = true;
        _followingBottom = true;
        _scroll.VScrollTarget = float.MaxValue;
        _lastScrollTarget = _scroll.VScrollTarget;
        _scrollToLatest.Visible = false;

        // Rebuilt tab contents can take multiple layout passes before ScrollContainer
        // knows its final max value, so keep snapping for a few frames.
        _pendingScrollToBottomFrames = 4;
    }

    private void QueueLayoutRefresh()
    {
        // RichTextLabel caches line breaks during measure. On startup, chat rows
        // can be created before the separated chat panel reaches its final width,
        // so keep refreshing briefly until the real width has settled.
        _pendingLayoutRefreshFrames = 8;
    }

    private void StopFollowingBottom()
    {
        _isAtBottom = false;
        _followingBottom = false;
        _pendingScrollToBottomFrames = 0;
        _lastScrollTarget = _scroll.VScrollTarget;
        _scrollToLatest.Visible = true;
    }

    private void UpdateScrollState()
    {
        var scrollTarget = _scroll.VScrollTarget;
        var scrolledUp = scrollTarget < _lastScrollTarget - ScrollDirectionTolerance;
        _lastScrollTarget = scrollTarget;

        var scrollBottom = scrollTarget + _scroll.Height + BottomTolerance;
        var contentHeight = _rows.DesiredSize.Y;
        _isAtBottom = scrollBottom >= contentHeight;

        if (scrolledUp && !_isAtBottom)
        {
            StopFollowingBottom();
            return;
        }

        if (_isAtBottom)
        {
            _followingBottom = true;
            _scrollToLatest.Visible = false;
            return;
        }

        if (_followingBottom)
        {
            if (_pendingScrollToBottomFrames <= 0)
                QueueScrollToBottom();

            _scrollToLatest.Visible = false;
            return;
        }

        _pendingScrollToBottomFrames = 0;
        _scrollToLatest.Visible = true;
    }

    private sealed class ChatScrollContainer : ScrollContainer
    {
        public event Action<float, float, float>? OnUserMouseWheel;

        protected override void MouseWheel(GUIMouseWheelEventArgs args)
        {
            var previousTarget = VScrollTarget;
            base.MouseWheel(args);
            OnUserMouseWheel?.Invoke(args.Delta.Y, previousTarget, VScrollTarget);
        }
    }
}
