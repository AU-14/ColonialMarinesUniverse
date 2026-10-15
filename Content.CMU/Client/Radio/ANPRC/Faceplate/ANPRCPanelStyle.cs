using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.CMU14.Radio.ANPRC;

/// <summary>
///     Backlight detents behind the LT key. The real set's display can be run bright, run
///     down to a night level, or run with the lamp off entirely, and an operator working
///     after dark has a reason to care which.
/// </summary>
public enum ANPRCBacklight : byte
{
    Day,
    Night,
    Off,
}

/// <summary>The phosphor colours one backlight detent renders the screen in.</summary>
public sealed record ANPRCLcdPalette(
    Color Back,
    Color Grid,
    Color Bright,
    Color Mid,
    Color Dim,
    Color Unlit,
    Color Warn,
    Color Bad);

/// <summary>
///     Shared chrome for the AN/PRC-117G panel: the olive chassis, the engraved legends, the
///     LCD phosphor and the monospace faces the whole set is lettered in. Everything that is
///     purely about how the radio looks lives here so the pages can stay about radio state.
/// </summary>
public static class ANPRCPanelStyle
{
    // ----- chassis -------------------------------------------------------------------------------

    public static readonly Color ChassisDeep = Color.FromHex("#191E14");
    public static readonly Color Chassis = Color.FromHex("#2B3222");
    public static readonly Color ChassisRaised = Color.FromHex("#3A4430");
    public static readonly Color ChassisEdge = Color.FromHex("#141810");

    public static readonly Color Engraved = Color.FromHex("#A8B694");
    public static readonly Color EngravedDim = Color.FromHex("#6E7A5E");

    public static readonly Color BezelOuter = Color.FromHex("#0E120B");
    public static readonly Color BezelInner = Color.FromHex("#1D2618");

    // ----- keys ----------------------------------------------------------------------------------

    public static readonly Color KeyFace = Color.FromHex("#33322E");
    public static readonly Color KeyFaceHover = Color.FromHex("#454440");
    public static readonly Color KeyFacePressed = Color.FromHex("#242320");
    public static readonly Color KeyFaceDisabled = Color.FromHex("#262A22");
    public static readonly Color KeyHighlight = Color.FromHex("#55544E");
    public static readonly Color KeyShadow = Color.FromHex("#111110");

    public static readonly Color KeyLegend = Color.FromHex("#D9DCCF");
    public static readonly Color KeyLegendSecondary = Color.FromHex("#8E9483");
    public static readonly Color KeyLegendDisabled = Color.FromHex("#5A5F52");

    // a key that is currently holding a setting on, and the flash a key gives when struck
    public static readonly Color KeyLit = Color.FromHex("#8FE86B");
    public static readonly Color KeyFlash = Color.FromHex("#C9F5A8");
    public static readonly Color KeyArmed = Color.FromHex("#E0693F");

    // ----- readouts ------------------------------------------------------------------------------

    public static readonly Color Good = Color.FromHex("#78D69B");
    public static readonly Color Caution = Color.FromHex("#D6B85A");
    public static readonly Color Bad = Color.FromHex("#D65A5A");
    public static readonly Color Muted = Color.FromHex("#7E8A71");

    private static readonly ANPRCLcdPalette DayPalette = new(
        Back: Color.FromHex("#0B2010"),
        Grid: Color.FromHex("#123018"),
        Bright: Color.FromHex("#7BF5A8"),
        Mid: Color.FromHex("#43C177"),
        Dim: Color.FromHex("#2E8351"),
        Unlit: Color.FromHex("#1A4A2E"),
        Warn: Color.FromHex("#F0D060"),
        Bad: Color.FromHex("#F07070"));

    private static readonly ANPRCLcdPalette NightPalette = new(
        Back: Color.FromHex("#06140A"),
        Grid: Color.FromHex("#0C2012"),
        Bright: Color.FromHex("#3FCF8E"),
        Mid: Color.FromHex("#2D9E60"),
        Dim: Color.FromHex("#256845"),
        Unlit: Color.FromHex("#143C25"),
        Warn: Color.FromHex("#C4A84C"),
        Bad: Color.FromHex("#BE5757"));

    // lamp off. still legible, the way a reflective panel is in daylight, but it stops
    // being the brightest thing on the operator at night
    private static readonly ANPRCLcdPalette OffPalette = new(
        Back: Color.FromHex("#0A0D0A"),
        Grid: Color.FromHex("#12160F"),
        Bright: Color.FromHex("#8C9A88"),
        Mid: Color.FromHex("#6D7A6A"),
        Dim: Color.FromHex("#515C4F"),
        Unlit: Color.FromHex("#2E352D"),
        Warn: Color.FromHex("#9A8F5F"),
        Bad: Color.FromHex("#9A6060"));

    public static ANPRCLcdPalette Palette(ANPRCBacklight backlight) => backlight switch
    {
        ANPRCBacklight.Day => DayPalette,
        ANPRCBacklight.Off => OffPalette,
        _ => NightPalette,
    };

    public static string BacklightLabel(ANPRCBacklight backlight) => Loc.GetString(backlight switch
    {
        ANPRCBacklight.Day => "anprc-fp-backlight-day",
        ANPRCBacklight.Off => "anprc-fp-off",
        _ => "anprc-fp-backlight-night",
    });

    public static ANPRCBacklight NextBacklight(ANPRCBacklight backlight) => backlight switch
    {
        ANPRCBacklight.Night => ANPRCBacklight.Day,
        ANPRCBacklight.Day => ANPRCBacklight.Off,
        _ => ANPRCBacklight.Night,
    };

    // ----- fonts ---------------------------------------------------------------------------------

    private static readonly Dictionary<(int Size, bool Bold), Font> FontCache = new();

    /// <summary>
    ///     The set is lettered in one monospace face throughout - screen, keycaps and engraving -
    ///     which is most of what makes a panel read as equipment rather than as a menu.
    /// </summary>
    public static Font Mono(int size, bool bold = false)
    {
        if (FontCache.TryGetValue((size, bold), out var cached))
            return cached;

        var cache = IoCManager.Resolve<IResourceCache>();
        var path = bold
            ? "/Fonts/RobotoMono/RobotoMono-Bold.ttf"
            : "/Fonts/RobotoMono/RobotoMono-Regular.ttf";

        var font = new VectorFont(cache.GetResource<FontResource>(path), size);
        FontCache[(size, bold)] = font;

        return font;
    }

    // ----- helpers -------------------------------------------------------------------------------

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

    public static BoxContainer Row(int separation = 4)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = separation,
        };
    }

    public static BoxContainer Column(int separation = 3)
    {
        return new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = separation,
        };
    }
}
