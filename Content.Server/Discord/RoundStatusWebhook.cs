using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Content.Server.Discord;

public enum RoundStatusWebhookKind
{
    Starting,
    Lobby,
    Running,
    Ended,
    Shutdown,
}

public readonly record struct RoundStatusWebhookColors(
    int Starting,
    int Running,
    int Ended,
    int Shutdown);

public readonly record struct RoundStatusWebhookData(
    int RoundId,
    int PlayerCount,
    string MapName,
    string Govfor,
    string Gamemode,
    IReadOnlyList<RoundStatusRecentGamemode> RecentGamemodes,
    TimeSpan? Duration = null);

public readonly record struct RoundStatusRecentGamemode(int RoundId, string Gamemode, TimeSpan Duration);

public readonly record struct RoundStatusWebhookMessageIds(
    ulong StatusMessageId,
    ulong RoundEndPingMessageId,
    ulong GamemodeVotePingMessageId);

public static class RoundStatusWebhook
{
    private const int DetailValueLength = 96;
    private const int RecentGamemodeLength = 72;
    private const string FooterText = "CMU Status Network";

    public static readonly RoundStatusWebhookColors DefaultColors = new(
        0xF0C419,
        0x23EB49,
        0xCD1010,
        0x6B7280);

    private static readonly JsonSerializerOptions MessageIdsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public static WebhookPayload CreatePayload(
        RoundStatusWebhookKind kind,
        RoundStatusWebhookData status,
        IEnumerable<string?> roleIds,
        RoundStatusWebhookColors? colors = null)
    {
        colors ??= DefaultColors;
        var content = BuildRoleMentions(roleIds);

        if (kind == RoundStatusWebhookKind.Shutdown)
            return CreateOfflinePayload(content, colors.Value);

        var fields = new List<WebhookEmbedField>
        {
            new() { Name = "Status", Value = GetState(kind), Inline = true },
            new() { Name = "Players", Value = status.PlayerCount.ToString(CultureInfo.InvariantCulture), Inline = true },
            new() { Name = "Round", Value = $"#{status.RoundId}", Inline = true },
        };

        if (status.Duration is { } duration)
            fields.Add(new WebhookEmbedField { Name = "Runtime", Value = FormatDuration(duration), Inline = true });

        fields.Add(new WebhookEmbedField { Name = "Operation", Value = FormatOperation(status), Inline = false });
        fields.Add(new WebhookEmbedField { Name = "Recent Rounds", Value = FormatRecentGamemodes(status.RecentGamemodes), Inline = false });

        var payload = new WebhookPayload
        {
            Content = content,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = GetTitle(kind, status.RoundId),
                    Description = GetDescription(kind),
                    Color = GetColor(kind, colors.Value),
                    Footer = new WebhookEmbedFooter { Text = FooterText },
                    Fields = fields,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
    }

    private static WebhookPayload CreateOfflinePayload(string content, RoundStatusWebhookColors colors)
    {
        var payload = new WebhookPayload
        {
            Content = content,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = GetTitle(RoundStatusWebhookKind.Shutdown, 0),
                    Description = "Server offline.",
                    Color = colors.Shutdown,
                    Footer = new WebhookEmbedFooter { Text = FooterText },
                    Fields = new List<WebhookEmbedField>
                    {
                        new() { Name = "Status", Value = "Offline", Inline = true },
                    },
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
    }

    public static WebhookPayload CreateRolePingPayload(IEnumerable<string?> roleIds, string? message = null)
    {
        var content = BuildRoleMentions(roleIds);
        if (!string.IsNullOrWhiteSpace(content) && !string.IsNullOrWhiteSpace(message))
            content = $"{content} {message.Trim()}";

        var payload = new WebhookPayload
        {
            Content = content,
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
    }

    public static string SerializeMessageIds(RoundStatusWebhookMessageIds messageIds)
    {
        return JsonSerializer.Serialize(messageIds, MessageIdsJsonOptions);
    }

    public static bool TryDeserializeMessageIds(string? json, out RoundStatusWebhookMessageIds messageIds)
    {
        messageIds = default;

        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            messageIds = JsonSerializer.Deserialize<RoundStatusWebhookMessageIds>(
                json,
                MessageIdsJsonOptions);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static int ParseColor(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var color = value.Trim().TrimStart('#');
        if (color.Length != 6)
            return fallback;

        return int.TryParse(color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public static string? GetGamemodeRole(
        string? presetId,
        string? distressSignalRole,
        string? colonyFallRole,
        string? insurgencyRole)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return null;

        if (presetId.Equals("DistressSignal", StringComparison.OrdinalIgnoreCase))
            return NullIfEmpty(distressSignalRole);

        if (presetId.Equals("ColonyFall", StringComparison.OrdinalIgnoreCase))
            return NullIfEmpty(colonyFallRole);

        if (presetId.Equals("Insurgency", StringComparison.OrdinalIgnoreCase))
            return NullIfEmpty(insurgencyRole);

        return null;
    }

    public static IEnumerable<string> GetRoundStatusRoleIds(
        bool includeRoundEndRole,
        string? presetId,
        string? roundEndRole,
        string? distressSignalRole,
        string? colonyFallRole,
        string? insurgencyRole)
    {
        if (includeRoundEndRole && NullIfEmpty(roundEndRole) is { } endRole)
            yield return endRole;

        if (GetGamemodeRole(presetId, distressSignalRole, colonyFallRole, insurgencyRole) is { } gamemodeRole)
            yield return gamemodeRole;
    }

    public static bool TryGetMessageId(string? responseContent, out ulong messageId)
    {
        messageId = 0;

        if (string.IsNullOrWhiteSpace(responseContent))
            return false;

        try
        {
            var id = JsonNode.Parse(responseContent)?["id"]?.GetValue<string>();
            return ulong.TryParse(id, out messageId);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryGetMessageIdToDelete(ulong previousMessageId, ulong newMessageId, out ulong messageId)
    {
        messageId = 0;

        if (previousMessageId == 0 || previousMessageId == newMessageId)
            return false;

        messageId = previousMessageId;
        return true;
    }

    public static bool ShouldUpdate(TimeSpan now, TimeSpan nextUpdate, TimeSpan interval, bool hasStatusMessage)
    {
        return hasStatusMessage &&
               interval > TimeSpan.Zero &&
               now >= nextUpdate;
    }

    private static string BuildRoleMentions(IEnumerable<string?> roleIds)
    {
        return string.Join(
            " ",
            roleIds
                .Where(roleId => !string.IsNullOrWhiteSpace(roleId))
                .Distinct(StringComparer.Ordinal)
                .Select(roleId => $"<@&{roleId}>"));
    }

    private static string GetTitle(RoundStatusWebhookKind kind, int roundId)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => "CMU Round Status - Starting",
            RoundStatusWebhookKind.Lobby => "CMU Round Status - Lobby",
            RoundStatusWebhookKind.Running => $"CMU Round #{roundId} - Running",
            RoundStatusWebhookKind.Ended => $"CMU Round #{roundId} - Ended",
            RoundStatusWebhookKind.Shutdown => "CMU Round Status - Offline",
            _ => "CMU Round Status",
        };
    }

    private static int GetColor(RoundStatusWebhookKind kind, RoundStatusWebhookColors colors)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => colors.Starting,
            RoundStatusWebhookKind.Lobby => colors.Starting,
            RoundStatusWebhookKind.Running => colors.Running,
            RoundStatusWebhookKind.Ended => colors.Ended,
            RoundStatusWebhookKind.Shutdown => colors.Shutdown,
            _ => colors.Running,
        };
    }

    private static string GetDescription(RoundStatusWebhookKind kind)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => "Server starting. Preparing the next operation.",
            RoundStatusWebhookKind.Lobby => "Server online in pre-round lobby.",
            RoundStatusWebhookKind.Running => "Round in progress. Live operation status is below.",
            RoundStatusWebhookKind.Ended => "Round ended. Final operation summary is below.",
            RoundStatusWebhookKind.Shutdown => "Server offline.",
            _ => "Server status.",
        };
    }

    private static string GetState(RoundStatusWebhookKind kind)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => "Starting",
            RoundStatusWebhookKind.Lobby => "Lobby",
            RoundStatusWebhookKind.Running => "Running",
            RoundStatusWebhookKind.Ended => "Ended",
            RoundStatusWebhookKind.Shutdown => "Offline",
            _ => "Unknown",
        };
    }

    private static string FormatOperation(RoundStatusWebhookData status)
    {
        return string.Join(
            "\n",
            $"**Map:** {Shorten(status.MapName, DetailValueLength)}",
            $"**GOVFOR:** {Shorten(status.Govfor, DetailValueLength)}",
            $"**Mode:** {Shorten(status.Gamemode, DetailValueLength)}");
    }

    private static string FormatRecentGamemodes(IReadOnlyList<RoundStatusRecentGamemode> recentGamemodes)
    {
        if (recentGamemodes.Count == 0)
            return "No completed rounds yet.";

        return string.Join(
            "\n",
            recentGamemodes
                .Take(3)
                .Select(round => $"`#{round.RoundId}` {Shorten(round.Gamemode, RecentGamemodeLength)} - {FormatShortDuration(round.Duration)}"));
    }

    private static string UnknownIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Trim();
    }

    private static string Shorten(string value, int maxLength)
    {
        value = UnknownIfEmpty(value)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        while (value.Contains("  ", StringComparison.Ordinal))
        {
            value = value.Replace("  ", " ");
        }

        if (value.Length <= maxLength)
            return value;

        return maxLength <= 3
            ? value[..maxLength]
            : $"{value[..(maxLength - 3)].TrimEnd()}...";
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{(int) duration.TotalHours}h {duration.Minutes}m {duration.Seconds}s";
    }

    private static string FormatShortDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? $"{(int) duration.TotalHours}h{duration.Minutes:D2}m"
            : $"{duration.Minutes}m{duration.Seconds:D2}s";
    }
}
