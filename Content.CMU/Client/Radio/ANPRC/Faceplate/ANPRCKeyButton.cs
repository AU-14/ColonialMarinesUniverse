using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     One moulded key off the set's keypad: a raised cap with a primary legend and the small
///     secondary legend the real keys carry above them. Drawn rather than styled so the cap can
///     depress, light up while it is holding a setting on, and flash when it is struck - the
///     panel has no other way to tell an operator the press landed.
/// </summary>
public sealed class ANPRCKeyButton : ContainerButton
{
    private const float FlashSeconds = 0.18f;

    private readonly BoxContainer _stack;
    private readonly Label _primary;
    private readonly Label? _secondary;

    private float _flash;

    /// <summary>The key is holding a setting on - MON with the monitor up, SCAN while scanning.</summary>
    public bool Lit { get; set; }

    /// <summary>The key is one press from doing something that cannot be taken back.</summary>
    public bool Armed { get; set; }

    public ANPRCKeyButton(
        string primary,
        string? secondary = null,
        int primarySize = 11,
        float minWidth = 0f,
        float minHeight = 0f)
    {
        MinWidth = minWidth;
        MinHeight = minHeight;

        _stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
        };

        var stack = _stack;

        if (secondary != null)
        {
            _secondary = new Label
            {
                Text = secondary,
                FontOverride = ANPRCPanelStyle.Mono(7),
                FontColorOverride = ANPRCPanelStyle.KeyLegendSecondary,
                HorizontalAlignment = HAlignment.Center,
            };

            stack.AddChild(_secondary);
        }

        _primary = new Label
        {
            Text = primary,
            FontOverride = ANPRCPanelStyle.Mono(primarySize, true),
            FontColorOverride = ANPRCPanelStyle.KeyLegend,
            HorizontalAlignment = HAlignment.Center,
        };

        stack.AddChild(_primary);
        AddChild(stack);

        OnPressed += _ => _flash = FlashSeconds;
    }

    /// <summary>
    ///     Clip the legend rather than letting it set the key's width. For keys whose text is data
    ///     rather than a fixed marking - a memory row, a net name - since those would otherwise
    ///     widen the page past its own scroll viewport.
    ///
    ///     A clipped label measures as no width at all, and the legend stack is centred, which
    ///     means it would be arranged at that zero width and the text would never be drawn - the
    ///     key stayed full size and clickable with nothing written on it.
    ///
    ///     So a clipped legend stretches across the cap and reads from the left, which is how a
    ///     row of data wants to line up anyway. Note that the stretching is what has to change:
    ///     a Control's HAlignment.Left means "size to DesiredSize, then sit on the left", which
    ///     for a clipped label is still nothing wide. Only the Label's own Align may be set to
    ///     Left - that one is about where the text sits inside the box, not how big the box is.
    /// </summary>
    public bool ClipLegend
    {
        get => _primary.ClipText;
        set
        {
            _primary.ClipText = value;

            _stack.HorizontalAlignment = value ? HAlignment.Stretch : HAlignment.Center;

            _primary.HorizontalAlignment = HAlignment.Stretch;
            _primary.Align = value ? Label.AlignMode.Left : Label.AlignMode.Center;

            if (_secondary != null)
            {
                _secondary.HorizontalAlignment = HAlignment.Stretch;
                _secondary.Align = value ? Label.AlignMode.Left : Label.AlignMode.Center;
            }
        }
    }

    public string PrimaryText
    {
        get => _primary.Text ?? string.Empty;

        // assigning a label's text invalidates its measure whether or not the text changed, and
        // several of these are rewritten every frame, so the compare is worth making here once
        set
        {
            if (_primary.Text != value)
                _primary.Text = value;
        }
    }

    public void SetSecondaryVisible(bool visible)
    {
        if (_secondary != null)
            _secondary.Visible = visible;
    }

    /// <summary>
    ///     The small legend above the primary. Swapped while the pad is typing letters, so a key
    ///     shows the group it is keying rather than the function it runs when nothing is being
    ///     typed.
    /// </summary>
    public string SecondaryText
    {
        get => _secondary?.Text ?? string.Empty;

        set
        {
            // assigning a label's text invalidates its measure whether or not it changed
            if (_secondary != null && _secondary.Text != value)
                _secondary.Text = value;
        }
    }

    /// <summary>The small legend is the one doing the work - lit rather than the primary.</summary>
    public bool SecondaryLit { get; set; }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_flash > 0f)
            _flash = MathF.Max(0f, _flash - args.DeltaSeconds);

        var legend = Disabled
            ? ANPRCPanelStyle.KeyLegendDisabled
            : Armed
                ? ANPRCPanelStyle.KeyArmed
                : Lit
                    ? ANPRCPanelStyle.KeyLit
                    : ANPRCPanelStyle.KeyLegend;

        _primary.FontColorOverride = legend;

        if (_secondary != null)
        {
            _secondary.FontColorOverride = Disabled
                ? ANPRCPanelStyle.KeyLegendDisabled
                : SecondaryLit
                    ? ANPRCPanelStyle.KeyLit
                    : ANPRCPanelStyle.KeyLegendSecondary;
        }
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = PixelSizeBox;

        if (box.Width <= 0 || box.Height <= 0)
            return;

        var pressed = DrawMode == DrawModeEnum.Pressed;

        var face = Disabled
            ? ANPRCPanelStyle.KeyFaceDisabled
            : pressed
                ? ANPRCPanelStyle.KeyFacePressed
                : DrawMode == DrawModeEnum.Hover
                    ? ANPRCPanelStyle.KeyFaceHover
                    : ANPRCPanelStyle.KeyFace;

        // the well the cap sits in, so a depressed key reads as having gone down into the panel
        handle.DrawRect(box, ANPRCPanelStyle.KeyShadow);

        var scale = UIScale;
        var inset = (pressed ? 2f : 1f) * scale;
        var capTop = box.Top + inset;
        var capBottom = box.Bottom - (pressed ? 1f : 2f) * scale;
        var cap = new UIBox2(box.Left + scale, capTop, box.Right - scale, capBottom);

        handle.DrawRect(cap, face);

        if (!pressed && !Disabled)
        {
            // bevel: light off the top edge, shadow under the bottom
            handle.DrawRect(
                new UIBox2(cap.Left, cap.Top, cap.Right, cap.Top + scale),
                ANPRCPanelStyle.KeyHighlight);
            handle.DrawRect(
                new UIBox2(cap.Left, cap.Bottom - scale, cap.Right, cap.Bottom),
                ANPRCPanelStyle.KeyShadow);
        }

        if (Lit && !Disabled)
        {
            // a lit key carries a lamp strip along its foot rather than glowing all over,
            // which keeps the legend readable
            handle.DrawRect(
                new UIBox2(cap.Left + 2f * scale, cap.Bottom - 3f * scale, cap.Right - 2f * scale, cap.Bottom - scale),
                ANPRCPanelStyle.KeyLit);
        }

        if (Armed && !Disabled)
        {
            handle.DrawRect(
                new UIBox2(cap.Left, cap.Top, cap.Right, cap.Top + 2f * scale),
                ANPRCPanelStyle.KeyArmed);
            handle.DrawRect(
                new UIBox2(cap.Left, cap.Bottom - 2f * scale, cap.Right, cap.Bottom),
                ANPRCPanelStyle.KeyArmed);
        }

        if (_flash > 0f)
        {
            var alpha = _flash / FlashSeconds * 0.5f;
            handle.DrawRect(cap, ANPRCPanelStyle.KeyFlash.WithAlpha(alpha));
        }
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var min = Vector2.Zero;

        foreach (var child in Children)
        {
            child.Measure(availableSize);
            min = Vector2.Max(min, child.DesiredSize);
        }

        // room for the cap bevel the draw pass adds around the legend
        return min + new Vector2(8f, 6f);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var content = new UIBox2(4f, 3f, MathF.Max(4f, finalSize.X - 4f), MathF.Max(3f, finalSize.Y - 3f));

        foreach (var child in Children)
        {
            child.Arrange(content);
        }

        return finalSize;
    }
}
