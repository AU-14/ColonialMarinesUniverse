// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     URL of the Discord webhook that receives ahelp and mhelp traffic unless a per-source
    ///     override is set. Bughelp never uses it: bug posts need forum threads, which require
    ///     <c>cmu.help_relay_webhook_bughelp</c>. Empty disables Discord delivery for sources
    ///     without an override; the in-game flows keep working.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhook =
        CVarDef.Create("cmu.help_relay_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     The code revision this server build was published from. Filled by the publish pipeline so
    ///     relayed messages can be diffed against the exact code state players saw. Empty on dev builds.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayRevision =
        CVarDef.Create("cmu.help_relay_revision", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     The date this server build was published, ISO yyyy-MM-dd. The revision hash says what code
    ///     runs but nothing about its age; the date tells triage whether a report predates current
    ///     fixes. Filled by the publish pipeline. Empty on dev builds.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayBuiltAt =
        CVarDef.Create("cmu.help_relay_built_at", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     Per-source override of <c>cmu.help_relay_webhook</c> for ahelp traffic. Empty falls through
    ///     to the primary webhook for relay-delivered sources. Ahelp itself rides upstream's
    ///     BwoinkSystem Discord relay: a CMU14-tagged hook there makes this override win over
    ///     <c>discord_ahelp_webhook</c> when set, so ops can split ahelp onto its own channel.
    ///     Must be a text-channel webhook; the upstream relay posts plain messages, not threads.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhookAHelp =
        CVarDef.Create("cmu.help_relay_webhook_ahelp", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Per-source override of <c>cmu.help_relay_webhook</c> for mhelp traffic. Empty falls through
    ///     to the primary webhook.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhookMHelp =
        CVarDef.Create("cmu.help_relay_webhook_mhelp", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     URL of the Discord webhook for bug reports. Must be a forum-channel webhook; the
    ///     primary chat webhook cannot carry threads, so this never falls through. Empty
    ///     disables the bughelp command.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhookBugHelp =
        CVarDef.Create("cmu.help_relay_webhook_bughelp", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     URL of the Discord webhook that receives the round's station news at round end: one forum
    ///     thread per round, one message per article inside it. Must be a forum-channel webhook; the
    ///     primary chat webhook cannot carry threads, so this never falls through. Empty disables
    ///     news delivery. Upstream's <c>discord.news_webhook</c> path stays dormant either way; this
    ///     is the CMU replacement for it.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhookNews =
        CVarDef.Create("cmu.help_relay_webhook_news", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     URL of the Discord webhook that receives server-side error logs: every Error and Fatal
    ///     log line the server emits, plus Info-and-up traffic from sawmills named in
    ///     <c>cmu.help_relay_errors_sawmills</c>. Must be a text-channel webhook; this never falls
    ///     through to the primary webhook, it serves its own channel. Empty disables the relay.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhookErrors =
        CVarDef.Create("cmu.help_relay_webhook_errors", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    /// <summary>
    ///     Sawmills whose logs mirror to the error channel at every level from info up, comma
    ///     separated. Entries match the sawmill and its children (<c>eng</c> also matches
    ///     <c>eng.net</c>). Empty mirrors nothing below the default Error threshold.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayErrorsSawmills =
        CVarDef.Create("cmu.help_relay_errors_sawmills", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     Forum tag ids for bughelp report categories, grammar <c>code:&lt;id&gt;,mapping:&lt;id&gt;</c>.
    ///     Names match BugHelpCategory case-insensitively; unknown names are ignored, so tags for
    ///     future categories can sit in the config before the enum grows. Requires the bughelp
    ///     webhook to be a forum channel.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayBugHelpTags =
        CVarDef.Create("cmu.help_relay_bughelp_tags", string.Empty, CVar.SERVERONLY);

    /// <summary>
    ///     How long a player must wait between bug reports, in seconds.
    /// </summary>
    public static readonly CVarDef<float> BugHelpRateLimitPeriod =
        CVarDef.Create("cmu.bug_help_rate_limit_period", 300f, CVar.SERVERONLY);

    /// <summary>
    ///     How many bug reports a player may file per rate limit period.
    /// </summary>
    public static readonly CVarDef<int> BugHelpRateLimitCount =
        CVarDef.Create("cmu.bug_help_rate_limit_count", 3, CVar.SERVERONLY);

    /// <summary>
    ///     Discord channel whose posters get linked; the link always binds to the Discord account
    ///     that delivers the code, so this can never link one person on another's behalf. Players
    ///     normally link by DMing their code to the bot, which no one else can read.
    ///     Requires <c>discord.token</c> (the in-process DiscordLink gateway). 0 disables linking.
    /// </summary>
    public static readonly CVarDef<ulong> DiscordLinkChannel =
        CVarDef.Create("cmu.discord_link_channel", 0ul, CVar.SERVERONLY);
}
