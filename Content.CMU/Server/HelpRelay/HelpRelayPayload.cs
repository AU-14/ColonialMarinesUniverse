using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Content.Server.GameTicking;

namespace Content.Server.CMU14.HelpRelay;

/// <summary>
/// Round context stamped on every relayed message, and reused by the bug report artifact. Prototype
/// IDs only, never localized display names: relayed messages may become AI input, and display names
/// don't grep.
/// </summary>
public sealed record HelpContext
{
    public required int RoundId { get; init; }
    public TimeSpan Elapsed { get; init; }
    public string? Gamemode { get; init; }
    public string? Map { get; init; }
    public required string Server { get; init; }
}

/// <summary>
/// One relaying message, source-agnostic. The caller supplies what only it knows (text, who, thread);
/// the relay supplies everything shared (color, fields, footer, revision, queueing).
/// </summary>
public sealed record HelpPost
{
    /// <summary>Message text. Capped by the builder.</summary>
    public required string Description { get; init; }

    /// <summary>Author suffix for the embed author line ("ckey (Char · Job)", "ckey → ckey", ...).
    /// Null renders as "&lt;source&gt; · staff".</summary>
    public string? Author { get; init; }

    /// <summary>Discord user id mentioned in the message content; allowed_mentions opens for exactly
    /// this user. Null mentions nobody and suppresses everything.</summary>
    public ulong? PingUserId { get; init; }

    /// <summary>Embed title; conversations may leave it empty.</summary>
    public string? Title { get; init; }

    /// <summary>Caller-gathered context; when null the manager gathers it on the calling thread.</summary>
    public HelpContext? Context { get; init; }

    /// <summary>Optional report.json attachment body for bughelp posts.</summary>
    public string? AttachmentJson { get; init; }

    /// <summary>Forum thread name for the post; requires a forum/media channel webhook.</summary>
    public string? ThreadName { get; init; }

    /// <summary>Forum tag ids to apply to the created thread.</summary>
    public IReadOnlyList<string>? AppliedTags { get; init; }

    /// <summary>Caller-specific embed fields, appended after the shared Round/Mode/Map/Server set.</summary>
    public IReadOnlyList<HelpWebhookEmbedField>? ExtraFields { get; init; }
}

public static class HelpRelayPayload
{
    private const int DescriptionCap = 3000;

    /// <summary>Transcript description limit for conversation relays; Discord caps embed descriptions at 4096.</summary>
    public const int ConversationDescriptionMax = 4000;

    /// <summary>Per-line cap inside a conversation transcript, mirroring the ahelp relay.</summary>
    public const int ConversationLineCap = 3000;

    private const string LineTooLongText = "... **(too long)**";

    public static HelpWebhookPayload Build(HelpSource source, HelpPost post, string? revision, string? builtAt = null)
    {
        var context = post.Context;
        var ping = post.PingUserId;
        return new HelpWebhookPayload
        {
            Content = ping is { } userId ? $"<@{userId}>" : null,
            AllowedMentions = ping == null
                ? HelpWebhookAllowedMentions.Quiet
                : HelpWebhookAllowedMentions.ForUser(ping.Value),
            ThreadName = post.ThreadName,
            AppliedTags = post.AppliedTags is { Count: > 0 } tags ? new List<string>(tags) : null,
            Embeds =
            [
                new HelpWebhookEmbed
                {
                    Author = new() { Name = $"{source.Label()} · {post.Author ?? "staff"}" },
                    Title = post.Title,
                    Description = post.Description.Length > DescriptionCap
                        ? post.Description[..DescriptionCap] + "…"
                        : post.Description,
                    Color = source.Color(),
                    Timestamp = DateTime.UtcNow.ToString("o"),
                    Fields =
                    [
                        new() { Name = "Round", Value = $"{context?.RoundId ?? 0}" },
                        new() { Name = "Mode", Value = context?.Gamemode ?? "?" },
                        new() { Name = "Map", Value = context?.Map ?? "?" },
                        new() { Name = "Server", Value = context?.Server ?? "?" },
                        ..post.ExtraFields ?? [],
                    ],
                    Footer = new() { Text = FooterText(revision, builtAt, context?.Server) },
                },
            ],
        };
    }

    /// <summary>
    /// One transcript line in the ahelp relay format: direction icon, round time while in round, speaker, text.
    /// </summary>
    public static string BuildConversationLine(string speaker, bool fromStaff, string? roundTime, string text)
    {
        if (text.Length > ConversationLineCap)
            text = text[..(ConversationLineCap - LineTooLongText.Length)] + LineTooLongText;

        var line = new StringBuilder(fromStaff ? ":outbox_tray:" : ":inbox_tray:");
        if (roundTime != null)
            line.Append($" **[{roundTime}]**");
        line.Append($" **{speaker}:** {text}");
        return line.ToString();
    }

    /// <summary>A separator line when the round run level changed mid-conversation, as the ahelp relay does.</summary>
    public static string BuildRunLevelSeparator(GameRunLevel runLevel) => runLevel switch
    {
        GameRunLevel.PreRoundLobby => "\n\n:arrow_forward: _**Pre-round lobby started**_\n",
        GameRunLevel.InRound => "\n\n:arrow_forward: _**Round started**_\n",
        GameRunLevel.PostRound => "\n\n:stop_button: _**Post-round started**_\n",
        _ => string.Empty,
    };

    /// <summary>The edit-in-place conversation embed: webhook username carries who, description is the transcript.</summary>
    public static HelpWebhookPayload BuildConversationPayload(
        HelpSource source,
        string username,
        string description,
        string? revision,
        string? server,
        string? builtAt = null)
    {
        return new HelpWebhookPayload
        {
            Username = $"{source.Label()} · {username}",
            AllowedMentions = HelpWebhookAllowedMentions.Quiet,
            Embeds =
            [
                new HelpWebhookEmbed
                {
                    Description = description,
                    Color = source.Color(),
                    Footer = new() { Text = FooterText(revision, builtAt, server) },
                },
            ],
        };
    }

    private static string FooterText(string? revision, string? builtAt, string? server)
    {
        return string.Join(" · ", new[]
        {
            revision is { Length: > 0 } ? $"rev {revision}" : null,
            builtAt is { Length: > 0 } ? $"built {builtAt}" : null,
            server,
            "CMU help relay",
        }.Where(p => !string.IsNullOrEmpty(p)));
    }
}

// Discord webhook API shapes; self-contained so no upstream Discord DTOs need extending.
/// <summary>
/// A post the relay gave up on, persisted for manual recovery. Carries the payload and attachment
/// as parsed JSON; never the webhook URL.
/// </summary>
public sealed class DroppedPost
{
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;

    [JsonPropertyName("dropped_at")] public string DroppedAt { get; set; } = string.Empty;

    [JsonPropertyName("payload")] public JsonNode? Payload { get; set; }

    [JsonPropertyName("attachment")] public JsonNode? Attachment { get; set; }
}

public sealed class HelpWebhookPayload
{
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("username")] public string? Username { get; set; }
    [JsonPropertyName("thread_name")] public string? ThreadName { get; set; }
    [JsonPropertyName("applied_tags")] public List<string>? AppliedTags { get; set; }
    [JsonPropertyName("allowed_mentions")] public HelpWebhookAllowedMentions? AllowedMentions { get; set; }
    [JsonPropertyName("embeds")] public List<HelpWebhookEmbed> Embeds { get; set; } = [];
}

/// <summary>
/// {"parse":[]} suppresses every mention. Relay embeds and transcripts carry player input, and
/// message edits re-parse content with default allowances unless the edit sets this explicitly.
/// </summary>
public sealed class HelpWebhookAllowedMentions
{
    public static readonly HelpWebhookAllowedMentions Quiet = new();

    public static HelpWebhookAllowedMentions ForUser(ulong userId) => new()
    {
        Users = [userId.ToString()],
    };

    [JsonPropertyName("parse")] public List<string> Parse { get; set; } = [];

    /// <summary>User ids allowed to ping despite the empty parse; how a reporter gets mentioned.</summary>
    [JsonPropertyName("users")] public List<string>? Users { get; set; }
}

public sealed class HelpWebhookEmbed
{
    [JsonPropertyName("author")] public HelpWebhookEmbedAuthor? Author { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("color")] public int Color { get; set; }
    [JsonPropertyName("fields")] public List<HelpWebhookEmbedField> Fields { get; set; } = [];
    [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
    [JsonPropertyName("footer")] public HelpWebhookEmbedFooter? Footer { get; set; }
}

public sealed class HelpWebhookEmbedAuthor
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public sealed class HelpWebhookEmbedField
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("value")] public string Value { get; set; } = string.Empty;
    [JsonPropertyName("inline")] public bool Inline { get; set; } = true;
}

public sealed class HelpWebhookEmbedFooter
{
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}
