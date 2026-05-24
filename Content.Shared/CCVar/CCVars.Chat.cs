using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Chat rate limit values are accounted in periods of this size (seconds).
    ///     After the period has passed, the count resets.
    /// </summary>
    /// <seealso cref="ChatRateLimitCount"/>
    public static readonly CVarDef<float> ChatRateLimitPeriod =
        CVarDef.Create("chat.rate_limit_period", 2f, CVar.SERVERONLY);

    /// <summary>
    ///     How many chat messages are allowed in a single rate limit period.
    /// </summary>
    /// <remarks>
    ///     The total rate limit throughput per second is effectively
    ///     <see cref="ChatRateLimitCount"/> divided by <see cref="ChatRateLimitCount"/>.
    /// </remarks>
    /// <seealso cref="ChatRateLimitPeriod"/>
    public static readonly CVarDef<int> ChatRateLimitCount =
        CVarDef.Create("chat.rate_limit_count", 10, CVar.SERVERONLY);

    /// <summary>
    ///     Minimum delay (in seconds) between notifying admins about chat message rate limit violations.
    ///     A negative value disables admin announcements.
    /// </summary>
    public static readonly CVarDef<int> ChatRateLimitAnnounceAdminsDelay =
        CVarDef.Create("chat.rate_limit_announce_admins_delay", 15, CVar.SERVERONLY);

    public static readonly CVarDef<int> ChatMaxMessageLength =
        CVarDef.Create("chat.max_message_length", 1000, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> ChatMaxAnnouncementLength =
        CVarDef.Create("chat.max_announcement_length", 256, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<bool> ChatSanitizerEnabled =
        CVarDef.Create("chat.chat_sanitizer_enabled", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ChatShowTypingIndicator =
        CVarDef.Create("chat.show_typing_indicator", true, CVar.ARCHIVE | CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> ChatEnableFancyBubbles =
        CVarDef.Create("chat.enable_fancy_bubbles",
            true,
            CVar.CLIENTONLY | CVar.ARCHIVE,
            "Toggles displaying fancy speech bubbles, which display the speaking character's name.");

    public static readonly CVarDef<bool> ChatFancyNameBackground =
        CVarDef.Create("chat.fancy_name_background",
            false,
            CVar.CLIENTONLY | CVar.ARCHIVE,
            "Toggles displaying a background under the speaking character's name.");

    /// <summary>
    ///     A message broadcast to each player that joins the lobby.
    ///     May be changed by admins ingame through use of the "set-motd" command.
    ///     In this case the new value, if not empty, is broadcast to all connected players and saved between rounds.
    ///     May be requested by any player through use of the "get-motd" command.
    /// </summary>
    public static readonly CVarDef<string> MOTD =
        CVarDef.Create("chat.motd",
            "",
            CVar.SERVER | CVar.SERVERONLY | CVar.ARCHIVE,
            "A message broadcast to each player that joins the lobby.");

    /// <summary>
    /// A string containing a list of newline-separated words to be highlighted in the chat.
    /// </summary>
    public static readonly CVarDef<string> ChatHighlights =
        CVarDef.Create("chat.highlights", "", CVar.CLIENTONLY | CVar.ARCHIVE, "A list of newline-separated words to be highlighted in the chat.");

    /// <summary>
    /// An option to toggle the automatic filling of the highlights with the character's info, if available.
    /// </summary>
    public static readonly CVarDef<bool> ChatAutoFillHighlights =
        CVarDef.Create("chat.auto_fill_highlights", false, CVar.CLIENTONLY | CVar.ARCHIVE, "Toggles automatically filling the highlights with the character's information.");

    /// <summary>
    /// The color in which the highlights will be displayed.
    /// </summary>
    public static readonly CVarDef<string> ChatHighlightsColor =
        CVarDef.Create("chat.highlights_color", "#17FFC1FF", CVar.CLIENTONLY | CVar.ARCHIVE, "The color in which the highlights will be displayed.");

    /// <summary>
    /// Client-side chat tab definitions, saved between sessions.
    /// </summary>
    public static readonly CVarDef<string> ChatTabs =
        CVarDef.Create("chat.tabs", "", CVar.CLIENTONLY | CVar.ARCHIVE, "Client-side chat tab definitions.");

    /// <summary>
    /// Client-side channel and radio-label font/color overrides.
    /// </summary>
    public static readonly CVarDef<string> ChatChannelStyles =
        CVarDef.Create("chat.channel_styles", "", CVar.CLIENTONLY | CVar.ARCHIVE, "Client-side chat channel font and color overrides.");

    /// <summary>
    /// Client-side split chat pane state, saved between sessions.
    /// </summary>
    public static readonly CVarDef<string> ChatSplitPane =
        CVarDef.Create("chat.split_pane", "", CVar.CLIENTONLY | CVar.ARCHIVE, "Client-side split chat pane state.");

    /// <summary>
    /// Use the pre-rework chat presentation.
    /// </summary>
    public static readonly CVarDef<bool> ChatLegacyMode =
        CVarDef.Create("chat.legacy_mode", false, CVar.CLIENTONLY | CVar.ARCHIVE, "Use the legacy chat presentation.");

    /// <summary>
    /// Color the full text of each structured chat row instead of only channel accents and inline highlight markup.
    /// </summary>
    public static readonly CVarDef<bool> ChatColorWholeMessage =
        CVarDef.Create("chat.color_whole_message", true, CVar.CLIENTONLY | CVar.ARCHIVE, "Color the full text of each structured chat row.");
}
