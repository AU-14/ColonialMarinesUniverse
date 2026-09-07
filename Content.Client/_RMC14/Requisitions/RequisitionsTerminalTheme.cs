using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client._RMC14.Requisitions;

public sealed record RequisitionsTerminalTheme(
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
    public static readonly RequisitionsTerminalTheme Neutral = new(
        "#090C10", "#11171D", "#1A222A", "#25313B",
        "#BCC7D1", "#EDF4FA", "#788895", "#8DB4D1", "#E7AD52", "#F0645B");

    public static readonly RequisitionsTerminalTheme Manifest = new(
        "#020A06", "#071A10", "#0D2A19", "#123D24",
        "#86E8AE", "#C9FFDC", "#4E9C6D", "#28F77A", "#FFB544", "#FF5C57");

    public CrtStyleBox Panel(Color color, bool grid = false, bool corners = false)
    {
        return new CrtStyleBox
        {
            BackgroundColor = color,
            BorderColor = Accent.WithAlpha(0.45f),
            BorderThickness = new Thickness(1),
            CornerColor = Accent.WithAlpha(0.65f),
            DrawCornerTicks = corners,
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
        : this(Color.FromHex(background), Color.FromHex(surface), Color.FromHex(surfaceRaised),
            Color.FromHex(surfaceSelected), Color.FromHex(text), Color.FromHex(textBright),
            Color.FromHex(textDim), Color.FromHex(accent), Color.FromHex(caution), Color.FromHex(alert))
    {
    }
}
