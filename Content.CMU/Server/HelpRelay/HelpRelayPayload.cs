using System.Linq;
using System.Text.Json.Serialization;

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

    /// <summary>Embed title; conversations may leave it empty.</summary>
    public string? Title { get; init; }

    /// <summary>Set to create a forum thread (one per bug report). Empty = plain channel message.</summary>
    public string? ThreadName { get; init; }

    /// <summary>Verified Discord link id to ping. Only ever set from rmc_linked_accounts.</summary>
    public ulong? PingUserId { get; init; }

    /// <summary>Caller-gathered context; when null the manager gathers it on the calling thread.</summary>
    public HelpContext? Context { get; init; }

    /// <summary>Optional file attachment (report.json); set with <see cref="AttachmentName"/>.</summary>
    public string? AttachmentJson { get; init; }
}

public static class HelpRelayPayload
{
    private const int DescriptionCap = 3000;

    public static HelpWebhookPayload Build(HelpSource source, HelpPost post, string? revision)
    {
        var context = post.Context;
        var payload = new HelpWebhookPayload
        {
            ThreadName = post.ThreadName,
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
                    ],
                    Footer = new()
                    {
                        Text = string.Join(" · ", new[]
                        {
                            revision is { Length: > 0 } ? $"rev {revision}" : null,
                            context?.Server,
                            "CMU help relay",
                        }.Where(p => !string.IsNullOrEmpty(p))),
                    },
                },
            ],
        };

        // Only verified link-table ids are pinged, so impersonation via the message is impossible.
        if (post.PingUserId is { } id)
        {
            payload.Content = $"<@{id}>";
            payload.AllowedMentions = new() { Users = [$"{id}"] };
        }

        return payload;
    }
}

// Discord webhook API shapes; self-contained so no upstream Discord DTOs need extending.
public sealed class HelpWebhookPayload
{
    [JsonPropertyName("thread_name")] public string? ThreadName { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }
    [JsonPropertyName("allowed_mentions")] public HelpWebhookMentions? AllowedMentions { get; set; }
    [JsonPropertyName("embeds")] public List<HelpWebhookEmbed> Embeds { get; set; } = [];
}

public sealed class HelpWebhookMentions
{
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
