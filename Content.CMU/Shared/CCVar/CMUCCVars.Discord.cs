// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     URL of the Discord webhook that receives all help relay traffic (ahelp/mhelp/bughelp)
    ///     unless a per-source override is set. Empty disables Discord delivery for sources without
    ///     an override; the in-game flows keep working.
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
    ///     Per-source override of <c>cmu.help_relay_webhook</c> for ahelp traffic. Empty falls through
    ///     to the primary webhook. Ahelp traffic still originates from the ahelp relay; this is the
    ///     R3 channel-split knob, kept here so no code changes when the split happens.
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
    ///     URL of the Discord webhook for bug reports. Empty falls through to
    ///     the primary webhook; both empty disables the bughelp command.
    /// </summary>
    public static readonly CVarDef<string> HelpRelayWebhookBugHelp =
        CVarDef.Create("cmu.help_relay_webhook_bughelp", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

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
}
