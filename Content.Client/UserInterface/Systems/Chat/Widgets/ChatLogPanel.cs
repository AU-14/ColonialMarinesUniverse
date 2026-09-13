using System.Numerics;
using Content.Client._CMU14.Interface;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
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
    private readonly VScrollBar _scrollBar;
    private readonly ChatLogList _rows;
    private readonly Button _scrollToLatest;
    private bool _syncingScrollBar;
    private float _lastSyncedBarValue;
    private bool _isAtBottom = true;
    private bool _followingBottom = true;
    private int _pendingScrollToBottomFrames;
    private int _pendingLayoutRefreshFrames;
    private float _lastLayoutWidth = -1f;


    public int EntryCount => _rows.EntryCount;

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

        // The scrollbar is a sibling of the scroll area, not the one ScrollContainer draws inside
        // itself. Two reasons the built-in one doesn't work here: ScrollContainer adds it before any
        // content, so it draws *underneath* the message rows, and it overlays the right-hand edge of
        // those rows, which is exactly where ChatMessageRow puts its channel accent triangle.
        _scroll = new ChatScrollContainer(this)
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
            // Hidden *and* not reserving: ReservesSpace=false is what drops its DesiredSize to zero,
            // which is what stops ScrollContainer insetting the rows for a bar that isn't drawn.
            // Hiding it does cost two things the engine would otherwise do for us, both handled in
            // FrameUpdate: applying VScrollTarget to VScroll, and resetting the offset to zero when
            // everything fits.
            ReserveScrollbarSpace = false,
            VScrollBarHidden = true
        };
        _scroll.OnUserMouseWheel += OnUserMouseWheel;
        _scroll.OnScrolled += UpdateScrollState;

        _scrollBar = new VScrollBar
        {
            VerticalExpand = true,
            // Matches crtChatScrollGrabber's own computed minimum (its content margins sum to 10) -
            // this is a floor, not the thing actually setting the width, but it should say the same
            // number as the stylebox or the two silently drift apart on the next retune.
            MinWidth = 10
        };
        _scrollBar.OnValueChanged += OnScrollBarValueChanged;
        // Chat is skipped by CrtLobbyTheme (Apply returns early on a ChatBox), so this is set here
        // rather than picked up by the tree walk.
        if (StyleNano.CrtUiEnabled)
            _scrollBar.AddStyleClass(StyleNano.StyleClassCrtChatScrollBar);

        root.AddChild(new ChatScrollLayout(_scroll, _scrollBar));

        _rows = new ChatLogList
        {
            HorizontalExpand = true,
            VerticalExpand = false
        };

        _scroll.AddChild(_rows);

        _scrollToLatest = new Button
        {
            Text = "Scroll to latest",
            Visible = false,
            HorizontalAlignment = HAlignment.Center,
            MinSize = new Vector2(200, 30),
            // Text only, no plate: a filled button here reads as a lump sitting on the log. The box
            // has to be overridden rather than removed, or the whole rect stops being clickable -
            // StyleBoxOverride also beats any stylesheet rule, including the CRT one added below.
            StyleBoxOverride = new StyleBoxFlat { BackgroundColor = Color.Transparent }
        };

        if (StyleNano.CrtUiEnabled)
        {
            // Sized to the log, not above it. This was 12 against a chat that is 8 everywhere else,
            // so the one control that appears over the messages was also the largest text in the
            // panel. Accent rather than body text because it is the only thing here that is an
            // action - and with the fill deliberately removed, colour is all it has left to say so.
            _scrollToLatest.AddStyleClass(StyleNano.StyleClassCrtButton);
            _scrollToLatest.Label.FontOverride =
                StyleNano.GetChatFont(IoCManager.Resolve<IResourceCache>());
            _scrollToLatest.Label.FontColorOverride = CrtTerminalPalette.Accent;
            _scrollToLatest.MinSize = new Vector2(160, 20);
            // The button is 160 wide and the label is not, so without this the text sits at its left
            // edge rather than under the middle of the log. See CrtLobbyTheme.ApplyControl - chat is
            // skipped by that walk, so it has to be repeated here.
            _scrollToLatest.Label.HorizontalExpand = true;
        }

        _scrollToLatest.OnPressed += _ => ScrollToBottom();
        root.AddChild(_scrollToLatest);
    }

    /// <summary>
    ///     Re-read the chat font onto the scroll-to-latest button: its FontOverride is baked once and
    ///     outlives a stylesheet rebuild, and it is not a row, so a repopulate never rebuilds it.
    /// </summary>
    public void RefreshChatFont()
    {
        _scrollToLatest.Label.FontOverride = StyleNano.CrtUiEnabled
            ? StyleNano.GetChatFont(IoCManager.Resolve<IResourceCache>())
            : null;
    }

    public ChatLogEntry AddMessage(ChatMessage message, FormattedMessage formatted, Color color, Color? accentOverride = null, int? fontSize = null)
    {
        return AddMessage(message, () => formatted, color, accentOverride, fontSize);
    }

    public ChatLogEntry AddMessage(ChatMessage message, Func<FormattedMessage> format, Color color, Color? accentOverride = null, int? fontSize = null)
    {
        var entry = new ChatLogEntry(message, format, color, accentOverride, fontSize);
        var removedHeight = _rows.Add(entry);
        if (!_followingBottom && removedHeight > 0)
            _scroll.VScroll = MathF.Max(0, _scroll.VScroll - removedHeight);

        if (_followingBottom || _isAtBottom)
            QueueScrollToBottom();
        else
            _scrollToLatest.Visible = true;

        return entry;
    }

    public void Clear()
    {
        _rows.Clear();

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

        if (_pendingScrollToBottomFrames > 0)
        {
            _scroll.VScroll = float.MaxValue;
            _scrollToLatest.Visible = false;
            _pendingScrollToBottomFrames--;
        }

        // Apply the scroll target by hand. ScrollContainer positions its content from the bar's
        // Value, but the mouse wheel (and VScrollTarget) only write ValueTarget - ScrollBar.FrameUpdate
        // is what normally eases one into the other, and that never runs for us because
        // Control.DoFrameUpdateRecursive returns early on an invisible control and we hide the
        // built-in bar. Without this the log does not move at all.
        if (_scroll.VScroll != _scroll.VScrollTarget)
            _scroll.VScroll = _scroll.VScrollTarget;

        // Re-derive from geometry every frame rather than trusting ScrollContainer.OnScrolled.
        // That event only fires when the scroll value itself changes, so anything that moves the
        // bottom without scrolling - a message arriving, a rewrap, the scroll-to-latest button
        // appearing and taking height off the log - used to leave the state stale, which is how the
        // button got stuck on screen after scrolling back down.
        UpdateScrollState();
        SyncScrollBar();
    }

    /// <summary>
    ///     Mirrors the scroll area's state onto the gutter's bar. Driven from FrameUpdate because
    ///     the content height changes on message add, tab switch, resize and rewrap, and there is no
    ///     single event covering all of those.
    /// </summary>
    private void SyncScrollBar()
    {
        var content = _rows.DesiredSize.Y;
        var page = _scroll.Height;

        _syncingScrollBar = true;

        if (page <= 0 || content <= page)
        {
            // Nothing to scroll. A full-length grabber keeps the gutter reading as a scrollbar
            // rather than an empty channel.
            _scrollBar.MaxValue = 1;
            _scrollBar.Page = 1;
            _scrollBar.Value = 0;

            // ScrollContainer.GetScrollValue zeroes the offset when everything fits - but only if
            // the bar was hidden by *it*. We hide it deliberately (VScrollBarHidden), and the engine
            // explicitly leaves the value alone in that case. ArrangeOverride also doesn't reset the
            // hidden bar's stale Page/MaxValue, so without this the log could still be dragged
            // around when there was nothing to scroll.
            // Writing VScroll also resets ValueTarget, so the apply step in FrameUpdate won't drag
            // it straight back.
            if (_scroll.VScroll != 0)
                _scroll.VScroll = 0;
        }
        else
        {
            _scrollBar.MaxValue = content;
            _scrollBar.Page = page;
            _scrollBar.Value = _scroll.VScroll;
        }

        _lastSyncedBarValue = _scrollBar.Value;
        _syncingScrollBar = false;
    }

    private void OnScrollBarValueChanged(Robust.Client.UserInterface.Controls.Range range)
    {
        // The flag covers writes made directly by SyncScrollBar; the value check covers the ones
        // that come back a frame later, when ScrollBar.FrameUpdate lerps Value towards the
        // ValueTarget we set. Without it every frame of a smooth scroll looks like a user drag.
        if (_syncingScrollBar || MathF.Abs(_scrollBar.Value - _lastSyncedBarValue) < 0.01f)
            return;

        _scroll.VScroll = _scrollBar.Value;

        // Dragging the grabber is a deliberate scroll, so stop snapping back to the newest message
        // unless it was dragged to the end.
        if (_scrollBar.IsAtEnd)
            ScrollToBottom();
        else
            StopFollowingBottom();
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
        _scroll.VScroll = float.MaxValue;
        _scrollToLatest.Visible = false;

        // Rebuilt tab contents can take multiple layout passes before ScrollContainer
        // knows its final max value, so keep snapping for a few frames.
        _pendingScrollToBottomFrames = 4;
    }

    private void QueueLayoutRefresh()
    {
        // A width change remeasures the visible rows at their final available width.
        _pendingLayoutRefreshFrames = 1;
    }

    private void StopFollowingBottom()
    {
        _isAtBottom = false;
        _followingBottom = false;
        _pendingScrollToBottomFrames = 0;
        _scrollToLatest.Visible = true;
    }

    private void UpdateScrollState()
    {
        var scrollTarget = _scroll.VScroll;

        // Replacing a tab can clamp the old offset while new rows are still being measured.
        // That geometry change must not cancel follow mode; input handlers do that explicitly.
        var scrollBottom = scrollTarget + _scroll.Height + BottomTolerance;
        var contentHeight = _rows.DesiredSize.Y;
        _isAtBottom = scrollBottom >= contentHeight;

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

    /// <summary>
    ///     Reserves the scrollbar's width before measuring the message area. A horizontal BoxContainer
    ///     measures the scroll area at the full width but arranges it narrower, causing wrapped text
    ///     to change size and invalidate the scroll area's measurement again on every frame.
    /// </summary>
    private sealed class ChatScrollLayout : Container
    {
        private readonly ChatScrollContainer _scroll;
        private readonly VScrollBar _scrollBar;

        public ChatScrollLayout(ChatScrollContainer scroll, VScrollBar scrollBar)
        {
            _scroll = scroll;
            _scrollBar = scrollBar;
            HorizontalExpand = true;
            VerticalExpand = true;
            AddChild(scroll);
            AddChild(scrollBar);
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _scrollBar.Measure(availableSize);
            var scrollWidth = MathF.Max(0, availableSize.X - _scrollBar.DesiredSize.X);
            _scroll.Measure(new Vector2(scrollWidth, availableSize.Y));

            return new Vector2(_scroll.DesiredSize.X + _scrollBar.DesiredSize.X,
                MathF.Max(_scroll.DesiredSize.Y, _scrollBar.DesiredSize.Y));
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            var scrollWidth = MathF.Max(0, finalSize.X - _scrollBar.DesiredSize.X);
            _scroll.Arrange(UIBox2.FromDimensions(Vector2.Zero, new Vector2(scrollWidth, finalSize.Y)));
            _scrollBar.Arrange(new UIBox2(scrollWidth, 0, finalSize.X, finalSize.Y));
            return finalSize;
        }
    }

    private sealed class ChatScrollContainer : ScrollContainer
    {
        private readonly ChatLogPanel _owner;
        public event Action<float, float, float>? OnUserMouseWheel;

        public ChatScrollContainer(ChatLogPanel owner)
        {
            _owner = owner;
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            _owner._rows.SetViewport(VScroll, availableSize.Y, _owner._followingBottom);
            return base.MeasureOverride(availableSize);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            base.ArrangeOverride(finalSize);
            // ScrollContainer now knows the updated height, so it can accept an anchor correction
            // without clamping it against the previous window's estimated scrollbar maximum.
            var offset = _owner._rows.AnchoredScroll;
            if (MathF.Abs(VScroll - offset) > 0.01f)
            {
                VScroll = offset;
                base.ArrangeOverride(finalSize);
            }
            return finalSize;
        }

        protected override void MouseWheel(GUIMouseWheelEventArgs args)
        {
            var previousTarget = VScrollTarget;
            base.MouseWheel(args);
            OnUserMouseWheel?.Invoke(args.Delta.Y, previousTarget, VScrollTarget);
        }
    }
}
