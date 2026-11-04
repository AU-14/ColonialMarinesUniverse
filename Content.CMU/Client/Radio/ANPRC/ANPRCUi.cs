using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     Colours, faces and the handful of building blocks every page of the AN/PRC-117G panel
///     uses. The set's readouts are lettered in a monospace face on a phosphor screen; everything
///     that explains something is set in the ordinary UI face so it reads like help, not like a
///     readout.
/// </summary>
public static class ANPRCUi
{
    // ----- chassis -------------------------------------------------------------------------------

    public static readonly Color Chassis = Color.FromHex("#0E0F13");
    public static readonly Color Panel = Color.FromHex("#17181D");
    public static readonly Color PanelRaised = Color.FromHex("#1E2027");
    public static readonly Color PanelEdge = Color.FromHex("#343846");

    public static readonly Color Text = Color.FromHex("#C8D2E8");
    public static readonly Color TextDim = Color.FromHex("#8792A8");
    public static readonly Color HeadingText = Color.FromHex("#A9C3E8");

    public static readonly Color Good = Color.FromHex("#78D69B");
    public static readonly Color Warn = Color.FromHex("#E0C060");
    public static readonly Color Bad = Color.FromHex("#E06A6A");
    public static readonly Color Info = Color.FromHex("#7FB0E8");

    // ----- the screen ----------------------------------------------------------------------------

    public static readonly Color LcdBack = Color.FromHex("#07130A");
    public static readonly Color LcdEdge = Color.FromHex("#245236");
    public static readonly Color LcdBright = Color.FromHex("#4FE39A");
    public static readonly Color LcdMid = Color.FromHex("#35AE6C");
    public static readonly Color LcdDim = Color.FromHex("#4A7A55");
    public static readonly Color LcdOff = Color.FromHex("#1E4A2E");

    public static Color Severity(ANPRCSeverity severity) => severity switch
    {
        ANPRCSeverity.Bad => Bad,
        ANPRCSeverity.Warn => Warn,
        ANPRCSeverity.Info => Info,
        _ => Good,
    };

    // ----- fonts ---------------------------------------------------------------------------------

    private static readonly Dictionary<(int Size, bool Bold), Font> MonoCache = new();

    public static Font Mono(int size, bool bold = false)
    {
        if (MonoCache.TryGetValue((size, bold), out var cached))
            return cached;

        var cache = IoCManager.Resolve<IResourceCache>();
        var path = bold
            ? "/Fonts/RobotoMono/RobotoMono-Bold.ttf"
            : "/Fonts/RobotoMono/RobotoMono-Regular.ttf";

        var font = new VectorFont(cache.GetResource<FontResource>(path), size);
        MonoCache[(size, bold)] = font;

        return font;
    }

    // ----- building blocks -----------------------------------------------------------------------

    public static PanelContainer Framed(Control child, Color fill, Color border, float thickness = 1f)
    {
        var panel = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = fill,
                BorderColor = border,
                BorderThickness = new Thickness(thickness),
            },
        };

        panel.AddChild(child);

        return panel;
    }

    public static PanelContainer Card(Control child, Color? border = null)
    {
        return Framed(child, Panel, border ?? PanelEdge);
    }

    public static BoxContainer Column(int separation = 4, int margin = 0)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = separation,
            Margin = new Thickness(margin),
        };
    }

    public static BoxContainer Row(int separation = 4)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = separation,
        };
    }

    public static Label Label(string text, Color color, Font? font = null)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = color,
            FontOverride = font,
        };
    }

    /// <summary>A section title inside a page.</summary>
    public static Label Heading(string text)
    {
        return new Label
        {
            Text = text,
            FontColorOverride = HeadingText,
            FontOverride = Mono(11, true),
            Margin = new Thickness(0, 4, 0, 0),
        };
    }

    /// <summary>
    ///     Text that wraps to the width it is given. Plain labels never wrap, and a long one
    ///     stretches the whole window instead, so anything longer than a few words goes here.
    ///     The text is added as text, never parsed as markup, because some of it came off the air.
    /// </summary>
    public static RichTextLabel Wrapped(string text, Color color)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        SetWrapped(label, text, color);

        return label;
    }

    public static void SetWrapped(RichTextLabel label, string text, Color color)
    {
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();

        label.SetMessage(message);
    }

    public static Button Button(string text, string? tooltip = null, Action? pressed = null)
    {
        var button = new Button
        {
            Text = text,
            ToolTip = tooltip,
        };

        if (pressed != null)
            button.OnPressed += _ => pressed();

        return button;
    }

    /// <summary>A bar graph out of plain ASCII, which every face on the panel actually has.</summary>
    public static string Bars(int filled, int total)
    {
        filled = Math.Clamp(filled, 0, total);

        return new string('|', filled) + new string('.', total - filled);
    }
}

/// <summary>
///     A button for things that cannot be walked back. The first press arms it and says what the
///     second press will do; the second press within a few seconds does it.
/// </summary>
public sealed class ANPRCConfirmButton : Button
{
    private static readonly TimeSpan ArmWindow = TimeSpan.FromSeconds(4);

    private readonly string _idleText;
    private readonly string _armedText;

    private DateTime? _armedUntil;

    public event Action? OnConfirmed;

    public ANPRCConfirmButton(string idleText, string armedText, string? tooltip = null)
    {
        _idleText = idleText;
        _armedText = armedText;

        Text = idleText;
        ToolTip = tooltip;

        OnPressed += _ =>
        {
            if (_armedUntil is { } until && DateTime.UtcNow <= until)
            {
                Disarm();
                OnConfirmed?.Invoke();
                return;
            }

            _armedUntil = DateTime.UtcNow + ArmWindow;
            Text = _armedText;
            ModulateSelfOverride = ANPRCUi.Bad;
        };
    }

    public void Disarm()
    {
        _armedUntil = null;
        Text = _idleText;
        ModulateSelfOverride = null;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_armedUntil is { } until && (DateTime.UtcNow > until || Disabled))
            Disarm();
    }
}

/// <summary>
///     A bar under a scrolled area that says there is more below and jumps down when clicked.
///     A scrollbar on its own is not a hint: an operator played for hours without finding out
///     the old panel scrolled at all.
/// </summary>
public sealed class ANPRCMoreBelow : Button
{
    private readonly ScrollContainer _scroll;
    private readonly Control _content;

    public ANPRCMoreBelow(ScrollContainer scroll, Control content)
    {
        _scroll = scroll;
        _content = content;

        Text = Loc.GetString("anprc-op-more-below");
        ToolTip = Loc.GetString("anprc-op-more-below-tooltip");
        Visible = false;

        OnPressed += _ =>
        {
            var value = _scroll.GetScrollValue();
            _scroll.SetScrollValue(new System.Numerics.Vector2(value.X, value.Y + _scroll.Size.Y * 0.8f));
        };
    }

    /// <summary>Call once a frame from something that is always ticking; a hidden control is not.</summary>
    public void Check()
    {
        // content taller than the frame, and the bottom of it not yet in view
        var remaining = _content.Size.Y - _scroll.Size.Y - _scroll.GetScrollValue().Y;
        Visible = _scroll.VisibleInTree && remaining > 6f;
    }
}
