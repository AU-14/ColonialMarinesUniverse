using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Draws the vote popup at a larger scale - wider options, taller rows. Aimed at high
    ///     resolutions and ultrawides, where the default sizing leaves the vote small and hard to
    ///     read against a lot of screen.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoteUiLarge =
        CVarDef.Create("cmu.vote_ui_large", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public const string CMUMenuBarStyleOutlined = "outlined";
    public const string CMUMenuBarStyleRaised = "raised";

    /// <summary>
    ///     How the in-round top menu bar draws its keys - <see cref="CMUMenuBarStyleOutlined"/> or
    ///     <see cref="CMUMenuBarStyleRaised"/>.
    /// </summary>
    public static readonly CVarDef<string> CMUMenuBarStyle =
        CVarDef.Create("cmu.menu_bar_style", CMUMenuBarStyleOutlined, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Test look for the Separated layout's chat panel: <c>off</c>, or a housing colour -
    ///     <c>gunmetal</c>, <c>olive</c> or <c>beige</c>. Draws the panel as a molded housing with the
    ///     chat on a tube screen and the menu bar as keycaps.
    /// </summary>
    public static readonly CVarDef<string> CMUChatHousing =
        CVarDef.Create("cmu.chat_housing", "off", CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Playtime in minutes below which the guidebook opens itself in the lobby.
    ///     Disabled by default (0); players can open the guidebook manually.
    /// </summary>
    public static readonly CVarDef<int> CMUGuidebookAutoOpenPlaytime =
        CVarDef.Create("cmu.guidebook_auto_open_minutes", 0, CVar.CLIENTONLY);

    /// <summary>
    ///     Where the player has dragged the lobby's round clock, as a fraction of the free space
    ///     around it - 0.5, 0.5 being centred. Negative means untouched, so the clock takes its
    ///     default place in the gap between the action panel and the server-info screen.
    /// </summary>
    public static readonly CVarDef<float> CMULobbyClockX =
        CVarDef.Create("cmu.lobby_clock_x", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMULobbyClockY =
        CVarDef.Create("cmu.lobby_clock_y", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Whether the round clock is folded into the action column instead of floating over the
    ///     lobby. ARCHIVE so it doubles as the preference; does not clear the dragged position.
    /// </summary>
    public static readonly CVarDef<bool> CMULobbyClockMinimized =
        CVarDef.Create("cmu.lobby_clock_minimized", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Draw chat in a plain proportional face instead of the terminal one, leaving the rest of
    ///     the CRT theme alone. An accessibility option, not a cosmetic one.
    /// </summary>
    public static readonly CVarDef<bool> CMUChatReadableFont =
        CVarDef.Create("cmu.chat_readable_font", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public const string CMUUiFontTheme = "theme";
    public const string CMUUiFontNotoSans = "noto-sans";
    public const string CMUUiFontComicSans = "comic-sans";
    public const string CMUUiFontRobotoMono = "roboto-mono";
    public const string CMUUiFontCozette = "cozette";
    public const string CMUUiFontNotoSansDisplay = "noto-sans-display";

    /// <summary>Font family for menus and chat; theme keeps each theme's original typography.</summary>
    public static readonly CVarDef<string> CMUUiFont =
        CVarDef.Create("cmu.ui_font", CMUUiFontTheme, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Whether this client has completed or saved its UI setup.</summary>
    public static readonly CVarDef<bool> CMUUiConfigured =
        CVarDef.Create("cmu.ui_configured", false, CVar.CLIENTONLY | CVar.ARCHIVE);
    public const string CMUChatBigFontOff = "off";
    public const string CMUChatBigFontOne = "one";
    public const string CMUChatBigFontTwo = "two";

    /// <summary>
    ///     How many points chat is drawn above its normal size - one of
    ///     <see cref="CMUChatBigFontOff"/>, <see cref="CMUChatBigFontOne"/> or
    ///     <see cref="CMUChatBigFontTwo"/>. Chat only; nothing else in the UI moves.
    /// </summary>
    public static readonly CVarDef<string> CMUChatBigFont =
        CVarDef.Create("cmu.chat_big_font", CMUChatBigFontOff, CVar.CLIENTONLY | CVar.ARCHIVE);

    public const string CMUChatRowTintOff = "off";
    public const string CMUChatRowTintMuted = "muted";
    public const string CMUChatRowTintFull = "full";

    /// <summary>
    ///     How strongly a chat row is tinted by its channel under the CRT theme - one of
    ///     <see cref="CMUChatRowTintFull"/>, <see cref="CMUChatRowTintMuted"/> or
    ///     <see cref="CMUChatRowTintOff"/>. Off is what the theme shipped with; every row sat on the
    ///     ground and only the prefix said which channel it was.
    /// </summary>
    public static readonly CVarDef<string> CMUChatRowTint =
        CVarDef.Create("cmu.chat_row_tint", CMUChatRowTintFull, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Draw the CRT scanline effect over chat independently of other menu effects.
    /// </summary>
    public static readonly CVarDef<bool> CMUChatCrtHaze =
        CVarDef.Create("cmu.chat_crt_haze", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Overall strength of the CRT effect - scanlines, grain and the roll bar together, 0 to 1.
    ///     The individual settings below shape each one; this scales the lot.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectIntensity =
        CVarDef.Create("cmu.crt_effect_intensity", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Distance in pixels between scanlines. The single most important number in the effect:
    ///     below about 3 the line and the gap stop resolving separately and the whole thing reads as
    ///     a flat darkening rather than as lines.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectPitch =
        CVarDef.Create("cmu.crt_effect_pitch", 3f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Strength of the animated per-pixel grain, 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectStatic =
        CVarDef.Create("cmu.crt_effect_static", 0.35f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds between roll-bar passes. Two minutes: the sweep takes about two seconds, so the
    ///     bar is on screen for under two percent of the time and is genuinely a thing you catch
    ///     rather than a thing you watch.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectRollPeriod =
        CVarDef.Create("cmu.crt_effect_roll_period", 120f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Seconds one roll-bar crossing takes.</summary>
    public static readonly CVarDef<float> CMUCrtEffectRollSweep =
        CVarDef.Create("cmu.crt_effect_roll_sweep", 2.1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Roll-bar half-height, as a fraction of the surface.</summary>
    public static readonly CVarDef<float> CMUCrtEffectRollHeight =
        CVarDef.Create("cmu.crt_effect_roll_height", 0.045f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Peak horizontal shear inside the roll bar, as a fraction of width. This is the effect:
    ///     the band moves the image rather than lighting it up.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectRollDisplace =
        CVarDef.Create("cmu.crt_effect_roll_displace", 0.053f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     How much light the roll bar adds. Zero by default: the shear carries the effect on its
    ///     own, and any light the band adds is a moving bright patch on a surface being read.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectRollLift =
        CVarDef.Create("cmu.crt_effect_roll_lift", 0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Barrel distortion. Bulges the picture toward the viewer like a real tube. Edge midpoints
    ///     stay pinned to the window, so the cost of raising this is rounded corners eating into the
    ///     picture - past about 0.15 they reach far enough in to clip content.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectCurvature =
        CVarDef.Create("cmu.crt_effect_curvature", 0.05f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Radial corner darkening. A true gradient, unlike the old eight-rectangle version.</summary>
    public static readonly CVarDef<float> CMUCrtEffectVignette =
        CVarDef.Create("cmu.crt_effect_vignette", 0.35f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Runs the CRT surface texture - scanlines and grain - over ordinary menus. Separate from
    ///     the effect's own settings because a prop terminal and a settings page want the same
    ///     texture at very different strengths, and because this is the one that can hurt
    ///     readability.
    /// </summary>
    public static readonly CVarDef<bool> CMUCrtMenuEffect =
        CVarDef.Create("cmu.crt_menu_effect", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
