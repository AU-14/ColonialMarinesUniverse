using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Requisitions;

public enum RequisitionsTerminalStyle
{
    UsmcPhosphor,
    WeylandAmber,
    ColonialCyan,
    UppRedline,
    FieldMono,
}

public sealed record RequisitionsTerminalTheme(
    string Name,
    string Subtitle,
    Color Background,
    Color Surface,
    Color SurfaceRaised,
    Color SurfaceSelected,
    Color Text,
    Color TextBright,
    Color TextDim,
    Color Accent,
    Color Caution,
    Color Alert)
{
    private static readonly RequisitionsTerminalTheme[] Themes =
    {
        new("USCM PHOSPHOR", "LOAD BAY", "#020A06", "#071A10", "#0D2A19", "#123D24",
            "#86E8AE", "#C9FFDC", "#4E9C6D", "#28F77A", "#FFB544", "#FF5C57"),
        new("WEYLAND AMBER", "MANIFEST COMPOSER", "#0C0802", "#1B1205", "#302008", "#49300A",
            "#E8B966", "#FFE1A3", "#9B733C", "#FFB52E", "#FFE071", "#FF5B36"),
        new("COLONIAL CYAN", "OPERATOR WORKSTATION", "#02090D", "#071821", "#0B2935", "#103D4A",
            "#8DDDE8", "#D6FAFF", "#4E929E", "#37DCF2", "#F4CA5B", "#FF6675"),
        new("UPP REDLINE", "TACTICAL LEDGER", "#0C0203", "#1A0809", "#2B0C0E", "#421116",
            "#E89A90", "#FFD8D1", "#9C5A54", "#FF4D43", "#FFAE42", "#FFDFD9"),
        new("FIELD MONO", "COMPACT TERMINAL", "#08090A", "#151719", "#24272A", "#34383C",
            "#C4C9CE", "#FFFFFF", "#7E858C", "#E4E8EC", "#F2C94C", "#FF6B6B"),
    };

    public static RequisitionsTerminalTheme Get(RequisitionsTerminalStyle style)
    {
        return Themes[Math.Clamp((int) style, 0, Themes.Length - 1)];
    }

    public static RequisitionsTerminalStyle Next(RequisitionsTerminalStyle style)
    {
        return (RequisitionsTerminalStyle) (((int) style + 1) % Themes.Length);
    }

    public CrtStyleBox Panel(Color color, bool grid = false, bool corners = false)
    {
        return new CrtStyleBox
        {
            BackgroundColor = color,
            BorderColor = Accent.WithAlpha(0.45f),
            BorderThickness = new Thickness(1),
            ScanlineColor = Accent.WithAlpha(0.025f),
            GridColor = Accent.WithAlpha(0.025f),
            CornerColor = Accent.WithAlpha(0.65f),
            DrawGrid = grid,
            DrawCornerTicks = corners,
            MaxScanlines = 5,
            ScanlineSpacing = 72,
        };
    }

    public void ApplyButton(Button button, bool primary = false, bool warning = false)
    {
        var hue = warning ? Caution : primary ? Accent : Text;
        button.StyleBoxOverride = new CrtStyleBox
        {
            BackgroundColor = primary ? SurfaceSelected : SurfaceRaised,
            BorderColor = hue.WithAlpha(0.65f),
            BorderThickness = new Thickness(1),
            DrawCornerTicks = false,
            DrawScanlines = false,
            ContentMarginLeftOverride = 10,
            ContentMarginRightOverride = 10,
            ContentMarginTopOverride = 5,
            ContentMarginBottomOverride = 4,
        };
        button.Label.FontColorOverride = hue;
        button.Label.HorizontalExpand = true;
        button.Label.Align = Label.AlignMode.Center;
    }

    private RequisitionsTerminalTheme(
        string name,
        string subtitle,
        string background,
        string surface,
        string surfaceRaised,
        string surfaceSelected,
        string text,
        string textBright,
        string textDim,
        string accent,
        string caution,
        string alert)
        : this(name, subtitle,
            Color.FromHex(background), Color.FromHex(surface), Color.FromHex(surfaceRaised),
            Color.FromHex(surfaceSelected), Color.FromHex(text), Color.FromHex(textBright),
            Color.FromHex(textDim), Color.FromHex(accent), Color.FromHex(caution), Color.FromHex(alert))
    {
    }
}
