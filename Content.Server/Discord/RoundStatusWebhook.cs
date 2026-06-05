using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Content.Server.Discord;

public enum RoundStatusWebhookKind
{
    Starting,
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
    TimeSpan? Duration = null);

public static class RoundStatusWebhook
{
    public static readonly RoundStatusWebhookColors DefaultColors = new(
        0xF0C419,
        0x23EB49,
        0xCD1010,
        0x6B7280);

    public static WebhookPayload CreatePayload(
        RoundStatusWebhookKind kind,
        RoundStatusWebhookData status,
        IEnumerable<string?> roleIds,
        RoundStatusWebhookColors? colors = null)
    {
        colors ??= DefaultColors;
        var content = BuildRoleMentions(roleIds);
        var fields = new List<WebhookEmbedField>
        {
            new() { Name = "Current Players", Value = status.PlayerCount.ToString(), Inline = true },
            new() { Name = "Current Map", Value = UnknownIfEmpty(status.MapName), Inline = true },
            new() { Name = "Current GOVFOR", Value = UnknownIfEmpty(status.Govfor), Inline = true },
            new() { Name = "Current Gamemode", Value = UnknownIfEmpty(status.Gamemode), Inline = true },
            new() { Name = "Round ID", Value = $"#{status.RoundId}", Inline = true },
        };

        if (status.Duration is { } duration)
            fields.Add(new WebhookEmbedField { Name = "Duration", Value = FormatDuration(duration), Inline = true });

        var payload = new WebhookPayload
        {
            Content = content,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Title = GetTitle(kind, status.RoundId),
                    Description = "Server status",
                    Color = GetColor(kind, colors.Value),
                    Fields = fields,
                },
            },
        };

        if (!string.IsNullOrWhiteSpace(content))
            payload.AllowedMentions.AllowRoleMentions();

        return payload;
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
            RoundStatusWebhookKind.Starting => "Server starting",
            RoundStatusWebhookKind.Running => $"Round #{roundId} running",
            RoundStatusWebhookKind.Ended => $"Round #{roundId} ended",
            RoundStatusWebhookKind.Shutdown => "Server shutting down",
            _ => "Server status",
        };
    }

    private static int GetColor(RoundStatusWebhookKind kind, RoundStatusWebhookColors colors)
    {
        return kind switch
        {
            RoundStatusWebhookKind.Starting => colors.Starting,
            RoundStatusWebhookKind.Running => colors.Running,
            RoundStatusWebhookKind.Ended => colors.Ended,
            RoundStatusWebhookKind.Shutdown => colors.Shutdown,
            _ => colors.Running,
        };
    }

    private static string UnknownIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value;
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
}
