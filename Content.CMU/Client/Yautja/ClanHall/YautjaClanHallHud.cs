using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client.CMU14.Yautja.ClanHall;

// the predator hud look: black field, everything drawn as thin red light. structure is dim,
// things you can use are bright, one hot orange-red for the thing that matters. no filled panels
internal static class Hud
{
    public static readonly Color Background = Color.FromHex("#050101");
    public static readonly Color Grid = Color.FromHex("#ff2a1e").WithAlpha(0.016f);
    public static readonly Color Dim = Color.FromHex("#5c1611");
    public static readonly Color Line = Color.FromHex("#a8231a");
    public static readonly Color Bright = Color.FromHex("#ff3b2b");
    public static readonly Color Hot = Color.FromHex("#ff7a3c");
    public static readonly Color Label = Color.FromHex("#e0473b");
    public static readonly Color Muted = Color.FromHex("#94342b");
    public static readonly Color Text = Color.FromHex("#f3d9d2");
    public static readonly Color Ink = Color.FromHex("#120302");

    public const string Osd = "/Fonts/ChakraPetch/ChakraPetch-SemiBold.ttf";
    public const string Sans = "/Fonts/ChakraPetch/ChakraPetch-Regular.ttf";
    public const string SansBold = "/Fonts/ChakraPetch/ChakraPetch-Bold.ttf";

    public static Font Font(string path, int size)
    {
        return IoCManager.Resolve<IResourceCache>().GetFont(path, size);
    }

    // a line with a soft halo under it, which is what makes it read as light rather than ink
    public static void Glow(DrawingHandleScreen handle, Vector2 a, Vector2 b, Color color, float scale)
    {
        var dir = b - a;
        var length = dir.Length();
        if (length < 0.01f)
            return;

        var normal = new Vector2(-dir.Y, dir.X) / length;
        for (var i = 1; i <= 2; i++)
        {
            var offset = normal * i * scale;
            handle.DrawLine(a + offset, b + offset, color.WithAlpha(color.A * 0.18f / i));
            handle.DrawLine(a - offset, b - offset, color.WithAlpha(color.A * 0.18f / i));
        }

        handle.DrawLine(a, b, color);
    }

    // rectangle outline with bright corner brackets; the brackets carry the shape, the edges stay quiet
    public static void Frame(DrawingHandleScreen handle, UIBox2 box, float scale, Color edge, Color corner, float bracket = 10)
    {
        handle.DrawRect(box, edge, filled: false);
        var b = bracket * scale;
        var tl = box.TopLeft;
        var tr = box.TopRight;
        var bl = box.BottomLeft;
        var br = box.BottomRight;
        Glow(handle, tl, tl + new Vector2(b, 0), corner, scale);
        Glow(handle, tl, tl + new Vector2(0, b), corner, scale);
        Glow(handle, tr, tr - new Vector2(b, 0), corner, scale);
        Glow(handle, tr, tr + new Vector2(0, b), corner, scale);
        Glow(handle, bl, bl + new Vector2(b, 0), corner, scale);
        Glow(handle, bl, bl - new Vector2(0, b), corner, scale);
        Glow(handle, br, br - new Vector2(b, 0), corner, scale);
        Glow(handle, br, br - new Vector2(0, b), corner, scale);
    }

    public static void Ring(DrawingHandleScreen handle, Vector2 center, float radius, Color color, int segments = 72)
    {
        var step = MathF.Tau / segments;
        var prev = center + new Vector2(radius, 0);
        for (var i = 1; i <= segments; i++)
        {
            var next = center + new Vector2(MathF.Cos(i * step), MathF.Sin(i * step)) * radius;
            handle.DrawLine(prev, next, color);
            prev = next;
        }
    }

    public static void Arc(DrawingHandleScreen handle, Vector2 center, float radius, float from, float to, Color color, float width, float scale)
    {
        var steps = Math.Max(2, (int) MathF.Ceiling(MathF.Abs(to - from) / 0.04f));
        for (var w = -width / 2; w <= width / 2; w += 0.75f * scale)
        {
            var r = radius + w;
            var prev = center + new Vector2(MathF.Cos(from), MathF.Sin(from)) * r;
            for (var i = 1; i <= steps; i++)
            {
                var a = from + (to - from) * i / steps;
                var next = center + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
                handle.DrawLine(prev, next, color);
                prev = next;
            }
        }
    }

    public static Label Write(string text, Font font, Color color, bool clip = false)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color, ClipText = clip };
    }

    public static RichTextLabel Paragraph(string text, Color color)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        var message = new Robust.Shared.Utility.FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        label.SetMessage(message, defaultColor: color);
        return label;
    }

    public static StyleBoxFlat Field()
    {
        return new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#0d0302"),
            BorderColor = Dim,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };
    }

    public static LineEdit LineEdit(string placeholder = "")
    {
        return new LineEdit { PlaceHolder = placeholder, StyleBoxOverride = Field() };
    }
}

// the black field everything sits on, with the faint grid from the references
internal sealed class HudBackground : PanelContainer
{
    protected override void Draw(DrawingHandleScreen handle)
    {
        var box = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
        handle.DrawRect(box, Hud.Background);
        var step = 32 * UIScale;
        for (var x = step; x < PixelSize.X; x += step)
            handle.DrawLine(new Vector2(x, 0), new Vector2(x, PixelSize.Y), Hud.Grid);
        for (var y = step; y < PixelSize.Y; y += step)
            handle.DrawLine(new Vector2(0, y), new Vector2(PixelSize.X, y), Hud.Grid);
        Hud.Frame(handle, new UIBox2(box.Left + 1, box.Top + 1, box.Right - 2, box.Bottom - 2), UIScale, Hud.Line, Hud.Bright, 18);
        base.Draw(handle);
    }
}

// a section: hairline box with bracket corners and a small solid tag for its title.
// header holds the tag plus any controls for the section, body holds the content
internal sealed class HudFrame : Container
{
    private const float Pad = 10;
    public readonly BoxContainer Header;
    public readonly BoxContainer Body;

    public HudFrame(string? title, Font font)
    {
        Header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        if (title != null)
            Header.AddChild(new HudTag(title, font));
        Body = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        AddChild(Header);
        AddChild(Body);
        HorizontalExpand = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inner = Vector2.Max(Vector2.Zero, availableSize - new Vector2(Pad * 2, Pad * 2));
        Header.Measure(inner);
        Body.Measure(inner with { Y = MathF.Max(0, inner.Y - Header.DesiredSize.Y - 6) });
        return new Vector2(
            MathF.Max(Header.DesiredSize.X, Body.DesiredSize.X) + Pad * 2,
            Header.DesiredSize.Y + 6 + Body.DesiredSize.Y + Pad * 2);
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var width = finalSize.X - Pad * 2;
        Header.Arrange(UIBox2.FromDimensions(Pad, Pad, width, Header.DesiredSize.Y));
        var top = Pad + Header.DesiredSize.Y + 6;
        Body.Arrange(UIBox2.FromDimensions(Pad, top, width, finalSize.Y - top - Pad));
        return finalSize;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        Hud.Frame(handle, new UIBox2(0.5f, 0.5f, PixelSize.X - 1, PixelSize.Y - 1), s, Hud.Dim, Hud.Line);
    }
}

// solid red block with dark text, the "ACTIVE" chip from the references. used for titles and status
internal sealed class HudTag : Control
{
    private readonly Label _label;
    private Color _fill = Hud.Bright;
    private bool _outline;

    public HudTag(string text, Font font)
    {
        _label = Hud.Write(text.ToUpperInvariant(), font, Hud.Ink);
        _label.Margin = new Thickness(6, 1, 9, 1);
        AddChild(_label);
        VerticalAlignment = VAlignment.Center;
    }

    public Color Fill
    {
        get => _fill;
        set => _fill = value;
    }

    // secondary info: outlined in the colour, text in the colour, no fill
    public bool Outline
    {
        get => _outline;
        set
        {
            _outline = value;
            _label.FontColorOverride = value ? _fill : Hud.Ink;
        }
    }

    public string Text
    {
        set => _label.Text = value.ToUpperInvariant();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        // right edge cut on a slant, the only shape flourish the tags get
        var w = PixelSize.X;
        var h = PixelSize.Y;
        var cut = 5 * UIScale;
        var points = new[] { new Vector2(0, 0), new Vector2(w, 0), new Vector2(w - cut, h), new Vector2(0, h) };
        if (_outline)
        {
            var edge = new[] { new Vector2(0.5f, 0.5f), new Vector2(w - 0.5f, 0.5f), new Vector2(w - cut, h - 0.5f), new Vector2(0.5f, h - 0.5f) };
            for (var i = 0; i < edge.Length; i++)
                handle.DrawLine(edge[i], edge[(i + 1) % edge.Length], _fill);
        }
        else
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, _fill);
        }

        base.Draw(handle);
    }
}

// a hud button: outlined in red, lights up under the cursor, goes solid when pressed or selected.
// confirm mode wants two presses for anything destructive
internal class HudButton : ContainerButton
{
    private readonly Label? _label;
    private readonly string _text;
    private bool _selected;
    private bool _confirm;
    private TimeSpan? _armedUntil;
    private string _confirmText = string.Empty;

    public event Action? Activated;

    public HudButton(string text, Font font, bool confirm = false)
    {
        _text = text;
        _confirm = confirm;
        _label = Hud.Write(text, font, Hud.Label);
        _label.HorizontalAlignment = HAlignment.Center;
        _label.VerticalAlignment = VAlignment.Center;
        _label.Margin = new Thickness(10, 3);
        AddChild(_label);
        if (confirm)
            _confirmText = Loc.GetString("cmu-yautja-hall-confirm");

        OnPressed += _ =>
        {
            if (!_confirm)
            {
                Activated?.Invoke();
                return;
            }

            var now = IoCManager.Resolve<IGameTiming>().RealTime;
            if (_armedUntil is { } until && now < until)
            {
                Disarm();
                Activated?.Invoke();
                return;
            }

            _armedUntil = now + TimeSpan.FromSeconds(3);
            _label.Text = _confirmText;
            Restyle();
        };
        Restyle();
    }

    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Restyle();
        }
    }

    private bool Armed => _armedUntil != null;

    private void Disarm()
    {
        _armedUntil = null;
        if (_label != null)
            _label.Text = _text;
        Restyle();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        Restyle();
    }

    private void Restyle()
    {
        // BaseButton's constructor gets here before ours has made the label
        if (_label == null)
            return;

        var solid = !Disabled && (_selected || Armed || DrawMode == DrawModeEnum.Pressed);
        _label.FontColorOverride = Disabled ? Hud.Dim : solid ? Hud.Ink : DrawMode == DrawModeEnum.Hover ? Hud.Bright : Hud.Label;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_armedUntil is { } until && IoCManager.Resolve<IGameTiming>().RealTime >= until)
            Disarm();
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = UIScale;
        var w = PixelSize.X - 1;
        var h = PixelSize.Y - 1;
        var cut = 6 * s;
        var points = new[] { new Vector2(0, 0), new Vector2(w - cut, 0), new Vector2(w, cut), new Vector2(w, h), new Vector2(0, h) };

        var solid = !Disabled && (_selected || Armed || DrawMode == DrawModeEnum.Pressed);
        var hover = !Disabled && DrawMode == DrawModeEnum.Hover;
        if (solid)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, Armed ? Hud.Hot : Hud.Bright);
        }
        else if (hover)
        {
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, Hud.Bright.WithAlpha(0.12f));
        }

        var edge = Disabled ? Hud.Dim : hover || solid ? Hud.Bright : Hud.Line;
        for (var i = 0; i < points.Length; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Length];
            if (hover)
                Hud.Glow(handle, a, b, edge, s);
            else
                handle.DrawLine(a, b, edge);
        }

        base.Draw(handle);
    }
}

// a list row with no chrome: just text. the cursor puts a bracket on it, selection keeps it lit
internal sealed class HudRow : ContainerButton
{
    private bool _selected;

    public HudRow()
    {
        HorizontalExpand = true;
    }

    public bool Selected
    {
        get => _selected;
        set => _selected = value;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = UIScale;
        var hover = DrawMode == DrawModeEnum.Hover || DrawMode == DrawModeEnum.Pressed;
        if (_selected || hover)
        {
            var box = UIBox2.FromDimensions(Vector2.Zero, PixelSize);
            handle.DrawRect(box, Hud.Bright.WithAlpha(_selected ? 0.14f : 0.07f));
            var c = _selected ? Hud.Bright : Hud.Line;
            var h = PixelSize.Y;
            handle.DrawLine(new Vector2(0, 0), new Vector2(0, h), c);
            handle.DrawLine(new Vector2(0, 0), new Vector2(5 * s, 0), c);
            handle.DrawLine(new Vector2(0, h - 1), new Vector2(5 * s, h - 1), c);
        }

        base.Draw(handle);
    }
}

// the honor gauge, after the dial in the references: dotted outer ring, ticked scale, a hot arc
// showing how far up the board this sits, the number in the middle
internal sealed class HudGauge : Control
{
    private const float Start = MathF.PI * 0.75f;
    private const float Sweep = MathF.PI * 1.5f;

    private readonly Font _number;
    private readonly Font _label;
    private float _fill;
    private string _value = "0";
    private string _caption = string.Empty;
    private string _sub = string.Empty;

    public HudGauge(float size, Font number, Font label)
    {
        _number = number;
        _label = label;
        MinSize = new Vector2(size, size);
    }

    public void Set(float fill, string value, string caption, string sub)
    {
        _fill = Math.Clamp(fill, 0, 1);
        _value = value;
        _caption = caption;
        _sub = sub;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var c = PixelSize / 2;
        var r = MathF.Min(PixelSize.X, PixelSize.Y) / 2 - 4 * s;

        // dotted outer ring
        for (var i = 0; i < 90; i++)
        {
            var a = i * MathF.Tau / 90;
            var p = c + new Vector2(MathF.Cos(a), MathF.Sin(a)) * r;
            handle.DrawRect(UIBox2.FromDimensions(p - new Vector2(0.75f * s), new Vector2(1.5f * s)), Hud.Line);
        }

        Hud.Ring(handle, c, r * 0.86f, Hud.Dim);

        // ticks along the scale, every fifth one long
        for (var i = 0; i <= 50; i++)
        {
            var a = Start + Sweep * i / 50;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var outer = r * 0.84f;
            var inner = i % 5 == 0 ? r * 0.74f : r * 0.79f;
            handle.DrawLine(c + dir * inner, c + dir * outer, i / 50f <= _fill ? Hud.Bright : Hud.Dim);
        }

        // the reading
        Hud.Arc(handle, c, r * 0.66f, Start, Start + Sweep, Hud.Dim, 1.5f * s, s);
        if (_fill > 0)
            Hud.Arc(handle, c, r * 0.66f, Start, Start + Sweep * _fill, Hud.Hot, 4 * s, s);

        // small triangle markers at the quarter points, like the dial's glyph ring
        for (var i = 0; i <= 4; i++)
        {
            var a = Start + Sweep * i / 4;
            var dir = new Vector2(MathF.Cos(a), MathF.Sin(a));
            var tip = c + dir * r * 0.92f;
            var side = new Vector2(-dir.Y, dir.X) * 3.5f * s;
            var back = tip + dir * 6 * s;
            handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, new[] { tip, back + side, back - side }, Hud.Bright);
        }

        DrawCentered(handle, _number, _value, c - new Vector2(0, 8 * s), Hud.Text);
        DrawCentered(handle, _label, _caption, c + new Vector2(0, 20 * s), Hud.Label);
        DrawCentered(handle, _label, _sub, c + new Vector2(0, r * 0.62f), Hud.Muted);
    }

    // DrawString puts the string's top-left at the point given, so centre it by hand
    private void DrawCentered(DrawingHandleScreen handle, Font font, string text, Vector2 center, Color color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var size = handle.GetDimensions(font, text, UIScale);
        handle.DrawString(font, center - size / 2, text, UIScale, color);
    }
}

// a segmented bar, the vertical-graph blocks from the references laid flat
internal sealed class HudBar : Control
{
    private const int Segments = 20;
    private float _fill;

    public HudBar(float fill)
    {
        _fill = Math.Clamp(fill, 0, 1);
        MinSize = new Vector2(120, 10);
        VerticalAlignment = VAlignment.Center;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        var s = UIScale;
        var gap = 2 * s;
        var w = (PixelSize.X - gap * (Segments - 1)) / Segments;
        var lit = (int) MathF.Round(_fill * Segments);
        for (var i = 0; i < Segments; i++)
        {
            var box = UIBox2.FromDimensions(i * (w + gap), 0, w, PixelSize.Y);
            handle.DrawRect(box, i < lit ? (i == lit - 1 ? Hud.Hot : Hud.Bright) : Hud.Dim.WithAlpha(0.6f));
        }
    }
}

// two columns where the left one gets whatever the right one doesn't. measures the left with the
// width it will really get, so wrapped text wraps instead of shoving the right column off screen
internal sealed class HudColumns : Container
{
    private readonly Control _left;
    private readonly Control _right;
    private readonly float _rightWidth;
    private const float Gap = 14;

    public HudColumns(Control left, Control right, float rightWidth)
    {
        _left = left;
        _right = right;
        _rightWidth = rightWidth;
        AddChild(left);
        AddChild(right);
        HorizontalExpand = true;
    }

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var leftWidth = MathF.Max(0, availableSize.X - _rightWidth - Gap);
        _left.Measure(availableSize with { X = leftWidth });
        _right.Measure(availableSize with { X = _rightWidth });
        return new Vector2(availableSize.X, MathF.Max(_left.DesiredSize.Y, _right.DesiredSize.Y));
    }

    protected override Vector2 ArrangeOverride(Vector2 finalSize)
    {
        var leftWidth = MathF.Max(0, finalSize.X - _rightWidth - Gap);
        _left.Arrange(UIBox2.FromDimensions(0, 0, leftWidth, _left.DesiredSize.Y));
        _right.Arrange(UIBox2.FromDimensions(leftWidth + Gap, 0, _rightWidth, _right.DesiredSize.Y));
        return finalSize;
    }
}
