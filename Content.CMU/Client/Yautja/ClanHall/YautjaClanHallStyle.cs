using System.Numerics;
using Content.Client.Resources;
using Content.Shared.CMU14.Yautja;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.CMU14.Yautja.ClanHall;

// the clan hall is the bracer projecting it: the bracer's blood-red display palette on a dark
// alloy bezel, with honor picked out in gold since that's the number everyone is here for
internal static class YautjaClanHallStyle
{
    public static readonly Color Bezel = Color.FromHex("#15110f");
    public static readonly Color BezelEdge = Color.FromHex("#3b2f27");
    public static readonly Color BezelShadow = Color.FromHex("#060403");
    public static readonly Color Screen = Color.FromHex("#0b0202");
    public static readonly Color Surface = YautjaBracerUiStyle.Surface;
    public static readonly Color Card = YautjaBracerUiStyle.Card;
    public static readonly Color Row = YautjaBracerUiStyle.Row;
    public static readonly Color Border = YautjaBracerUiStyle.Border;
    public static readonly Color MutedBorder = YautjaBracerUiStyle.MutedBorder;
    public static readonly Color Text = YautjaBracerUiStyle.Text;
    public static readonly Color Muted = YautjaBracerUiStyle.Muted;
    public static readonly Color Dim = YautjaBracerUiStyle.Dim;
    public static readonly Color Red = YautjaBracerUiStyle.Red;
    public static readonly Color HotRed = YautjaBracerUiStyle.HotRed;
    public static readonly Color Honor = Color.FromHex("#e8a23a");
    public static readonly Color HonorDim = Color.FromHex("#7a5320");
    public static readonly Color Online = Color.FromHex("#ff6a4a");
    public static readonly Color Staff = Color.FromHex("#c9b27a");

    public const string Osd = "/Fonts/UAVOSD/UAV-OSD-Sans-Mono.ttf";
    public const string Sans = "/Fonts/NotoSans/NotoSans-Regular.ttf";
    public const string SansBold = "/Fonts/NotoSans/NotoSans-Bold.ttf";

    private static readonly ResPath RankIcons = new("/Textures/CMU14/Yautja/rank_icons.rsi");
    private static readonly ResPath Masks = new("/Textures/CMU14/Yautja/masks");

    public static Font Font(string path, int size)
    {
        return IoCManager.Resolve<IResourceCache>().GetFont(path, size);
    }

    public static StyleBoxFlat Box(Color background, Color? border = null, float borderThickness = 1, float padH = 0, float padV = 0)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border ?? background,
            BorderThickness = new Thickness(border == null ? 0 : borderThickness),
            ContentMarginLeftOverride = padH,
            ContentMarginRightOverride = padH,
            ContentMarginTopOverride = padV,
            ContentMarginBottomOverride = padV,
        };
    }

    public static PanelContainer Panel(Color background, Color? border = null, float padH = 8, float padV = 6)
    {
        return new PanelContainer { PanelOverride = Box(background, border, 1, padH, padV) };
    }

    // no clipping by default: a clipping label measures zero wide and vanishes in an hbox unless it expands
    public static Label Label(string text, Font font, Color color, bool clip = false)
    {
        return new Label { Text = text, FontOverride = font, FontColorOverride = color, ClipText = clip };
    }

    // wraps, unlike Label. takes the stylesheet's body font, there's no per-label override
    public static RichTextLabel Paragraph(string text, Color color)
    {
        var label = new RichTextLabel { HorizontalExpand = true };
        var message = new FormattedMessage();
        message.PushColor(color);
        message.AddText(text);
        message.Pop();
        label.SetMessage(message, defaultColor: color);
        return label;
    }

    public static BoxContainer HBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    public static BoxContainer VBox(int separation = 6)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    public static Control Gap()
    {
        return new Control { HorizontalExpand = true };
    }

    public static Color ClanColor(string hex)
    {
        return Color.TryFromHex(hex, out var color) ? color : Text;
    }

    // clan colours are picked by players, so keep them readable on the red-black screen
    public static Color Readable(Color color)
    {
        var luma = 0.299f * color.R + 0.587f * color.G + 0.114f * color.B;
        return luma < 0.35f ? Color.InterpolateBetween(color, Text, 0.45f) : color;
    }

    public static Texture? RankIcon(YautjaRank rank)
    {
        var state = rank switch
        {
            YautjaRank.Unblooded => "unblooded",
            YautjaRank.YoungBlood => "youngblood",
            YautjaRank.Elite => "elite",
            YautjaRank.Elder => "elder",
            YautjaRank.Leader => "leader",
            YautjaRank.Ancient => "ancient",
            _ => "blooded",
        };
        return Frame0(new SpriteSpecifier.Rsi(RankIcons, state));
    }

    public static Texture? Sigil(string sigil)
    {
        return Frame0(new SpriteSpecifier.Rsi(Masks / $"{YautjaClanSigils.OrDefault(sigil)}.rsi", "icon"));
    }

    private static Texture? Frame0(SpriteSpecifier specifier)
    {
        try
        {
            return IoCManager.Resolve<IEntityManager>().System<SpriteSystem>().Frame0(specifier);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static TextureRect Icon(Texture? texture, float size)
    {
        return new TextureRect
        {
            Texture = texture,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(size, size),
            TextureScale = new Vector2(size / 32f, size / 32f),
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    public static string RankName(YautjaRank rank)
    {
        return Loc.GetString(YautjaRankMetadata.For(rank).LocalizedName);
    }

    public static string Number(int value)
    {
        return value.ToString("N0");
    }

    // yautja script, made up on the spot: each glyph is a little column of strokes and dots
    // picked from its seed, so the same word always draws the same marks
    public static void DrawGlyphs(DrawingHandleScreen handle, Vector2 origin, int seed, int count, float scale, Color color)
    {
        var x = origin.X;
        for (var i = 0; i < count; i++)
        {
            var bits = Hash(seed * 31 + i);
            var h = 7f * scale;
            var top = origin.Y;
            // spine
            if ((bits & 1) != 0)
                handle.DrawLine(new Vector2(x + 2 * scale, top), new Vector2(x + 2 * scale, top + h), color);
            // ticks to the right at three heights
            for (var t = 0; t < 3; t++)
            {
                if ((bits >> (t + 1) & 1) == 0)
                    continue;

                var y = top + (1 + t * 2.5f) * scale;
                handle.DrawLine(new Vector2(x + 2 * scale, y), new Vector2(x + 5 * scale, y + ((bits >> 6 & 1) != 0 ? scale : -scale)), color);
            }

            // dots
            if ((bits >> 4 & 1) != 0)
                handle.DrawRect(UIBox2.FromDimensions(x, top + h - scale, scale, scale), color);
            if ((bits >> 5 & 1) != 0)
                handle.DrawRect(UIBox2.FromDimensions(x + 4 * scale, top, scale, scale), color);

            x += 7 * scale;
        }
    }

    public static int Hash(int value)
    {
        unchecked
        {
            var h = (uint) value * 2654435761u;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (int) (h & 0x7fffffff);
        }
    }
}

// a flat button that actually reacts to hover/press, which StyleBoxOverride on a plain Button doesn't
internal class HallButton : ContainerButton
{
    private readonly PanelContainer? _panel;
    public readonly Label Label;
    private Color _accent;
    private bool _selected;

    public HallButton(string text, Font font, Color accent, float padH = 10, float padV = 4)
    {
        _accent = accent;
        _panel = new PanelContainer { HorizontalExpand = true, VerticalExpand = true };
        Label = YautjaClanHallStyle.Label(text, font, YautjaClanHallStyle.Text);
        Label.HorizontalAlignment = HAlignment.Center;
        Label.VerticalAlignment = VAlignment.Center;
        Label.Margin = new Thickness(padH, padV);
        _panel.AddChild(Label);
        AddChild(_panel);
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

    public Color Accent
    {
        get => _accent;
        set
        {
            _accent = value;
            Restyle();
        }
    }

    public string Text
    {
        get => Label.Text ?? string.Empty;
        set => Label.Text = value;
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        Restyle();
    }

    protected virtual void Restyle()
    {
        // BaseButton's constructor fires DrawModeChanged before ours has built the panel
        if (_panel == null)
            return;

        var s = YautjaClanHallStyle.Card;
        var border = YautjaClanHallStyle.MutedBorder;
        var ink = YautjaClanHallStyle.Text;
        if (Disabled)
        {
            s = YautjaClanHallStyle.Surface;
            ink = YautjaClanHallStyle.Dim;
        }
        else if (_selected || DrawMode == DrawModeEnum.Pressed)
        {
            s = Color.InterpolateBetween(YautjaClanHallStyle.Card, _accent, 0.35f);
            border = _accent;
        }
        else if (DrawMode == DrawModeEnum.Hover)
        {
            s = YautjaClanHallStyle.Row;
            border = _accent;
        }

        _panel.PanelOverride = YautjaClanHallStyle.Box(s, border);
        Label.FontColorOverride = ink;
    }
}

// destructive actions take two presses. the first arms it for a few seconds and says so
internal sealed class ConfirmButton : HallButton
{
    private readonly string _text;
    private readonly string _confirm;
    private TimeSpan? _armedUntil;

    public event Action? Confirmed;

    public ConfirmButton(string text, string confirm, Font font)
        : base(text, font, YautjaClanHallStyle.HotRed)
    {
        _text = text;
        _confirm = confirm;
        OnPressed += _ =>
        {
            var now = IoCManager.Resolve<IGameTiming>().RealTime;
            if (_armedUntil is { } until && now < until)
            {
                Disarm();
                Confirmed?.Invoke();
                return;
            }

            _armedUntil = now + TimeSpan.FromSeconds(3);
            Text = _confirm;
            Selected = true;
        };
    }

    private void Disarm()
    {
        _armedUntil = null;
        Text = _text;
        Selected = false;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);
        if (_armedUntil is { } until && IoCManager.Resolve<IGameTiming>().RealTime >= until)
            Disarm();
    }
}
