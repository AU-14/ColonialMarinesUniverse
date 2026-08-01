using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Shared.Maths;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed class ChatTabSettings
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public ushort Channels { get; set; }
    public List<string> RadioLabels { get; set; } = new();

    public ChatChannel ChannelMask
    {
        get => (ChatChannel) Channels;
        set => Channels = (ushort) value;
    }
}

public sealed class ChatStyleSettings
{
    public string Target { get; set; } = string.Empty;
    public string? Color { get; set; }
    public string Font { get; set; } = ChatUserSettings.FontDefault;
    public int? FontSize { get; set; }
}

public sealed class ChatSplitSettings
{
    public bool Enabled { get; set; }
    public string SecondaryTabId { get; set; } = ChatUserSettings.RadioTabId;
    public int SecondaryRatioPercent { get; set; } = ChatUserSettings.DefaultSplitSecondaryRatioPercent;
}

public sealed record ChatStyleTarget(string Key, string Name, string DefaultColor, int? DefaultFontSize = null);

public sealed record ChatRadioTarget(string Label, string Name, string Color);

public static class ChatUserSettings
{
    public const string AllTabId = "all";
    public const string RadioTabId = "radio";
    public const string AdminTabId = "admin";
    public const string FontDefault = "Default";

    private const int MaxTabs = 12;
    private const int MaxTabTitleLength = 12;
    private const int MaxStyles = 64;
    public const int DefaultSplitSecondaryRatioPercent = 42;
    public const int MinSplitSecondaryRatioPercent = 22;
    public const int MaxSplitSecondaryRatioPercent = 78;
    public const int MinFontSize = 8;
    public const int MaxFontSize = 18;
    public const int DefaultFontSize = 12;
    public const ushort AllChannelBits = ushort.MaxValue;

    private static readonly Regex FirstColorTag = new(@"\[color=[^\]]+\]", RegexOptions.IgnoreCase);
    private static readonly Regex FirstFontTag = new(@"\[font(?<attrs>[^\]]*)\]", RegexOptions.IgnoreCase);
    private static readonly Regex FontSizeAttribute = new(@"\s+size=(?<size>\d+)", RegexOptions.IgnoreCase);

    public static readonly ChatStyleTarget[] BaseStyleTargets =
    {
        new(ChannelKey(ChatChannel.Local), "Local / Say", "#D6DCE0"),
        new(ChannelKey(ChatChannel.Whisper), "Whisper", "#B8BEC4"),
        new(ChannelKey(ChatChannel.Emotes), "Emotes", "#C9A7EA"),
        new(ChannelKey(ChatChannel.Radio), "Radio", "#9FE7B2"),
        new(LabelKey("RAD"), "Radio: RAD", "#9FE7B2"),
        new(ChannelKey(ChatChannel.LOOC), "LOOC", "#61D7D6"),
        new(ChannelKey(ChatChannel.OOC), "OOC", "#73BDF6"),
        new(ChannelKey(ChatChannel.Dead), "Deadchat", "#B9A2FF"),
        new(ChannelKey(ChatChannel.Admin), "Admin", "#FF7777"),
        new(ChannelKey(ChatChannel.AdminAlert), "Admin Alert", "#FF5F5F"),
        new(ChannelKey(ChatChannel.AdminChat), "Admin Chat", "#FF72C7"),
        new(ChannelKey(ChatChannel.MentorChat), "Mentor Chat", "#FFB55F"),
        new(ChannelKey(ChatChannel.Server), "Server", "#DDA94B"),
        new(ChannelKey(ChatChannel.Notifications), "Notifications", "#DDA94B"),
        new(ChannelKey(ChatChannel.Damage), "Damage", "#FF8A70"),
        new(ChannelKey(ChatChannel.Visual), "Actions", "#D6DCE0")
    };

    public static IReadOnlyList<ChatStyleTarget> CreateStyleTargets(IEnumerable<RadioChannelPrototype>? radioChannels = null)
    {
        var targets = new List<ChatStyleTarget>(BaseStyleTargets);
        var keys = targets.Select(target => target.Key).ToList();

        if (radioChannels == null)
            return targets;

        foreach (var channel in radioChannels.OrderBy(channel => channel.LocalizedName))
        {
            var label = channel.LocalizedName.Trim();
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var key = LabelKey(label);
            if (ContainsIgnoreCase(keys, key))
                continue;

            keys.Add(key);
            targets.Add(new ChatStyleTarget(
                key,
                $"Radio: {label}",
                channel.Color.ToHex()));
        }

        return targets;
    }

    public static IReadOnlyList<ChatRadioTarget> CreateRadioTargets(IEnumerable<RadioChannelPrototype>? radioChannels = null)
    {
        var targets = new List<ChatRadioTarget>();
        var labels = new List<string>();

        if (radioChannels == null)
            return targets;

        foreach (var channel in radioChannels.OrderBy(channel => channel.LocalizedName))
        {
            var label = NormalizeRadioLabel(channel.LocalizedName);
            if (string.IsNullOrWhiteSpace(label) || ContainsIgnoreCase(labels, label))
                continue;

            labels.Add(label);
            targets.Add(new ChatRadioTarget(label, channel.LocalizedName, channel.Color.ToHex()));
        }

        return targets;
    }

    public static List<ChatTabSettings> LoadTabs(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return CreateDefaultTabs();

        var tabs = new List<ChatTabSettings>();
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = SplitEscaped(line, '|');
            if (fields.Count < 3 || !ushort.TryParse(fields[2], out var channels))
                continue;

            tabs.Add(new ChatTabSettings
            {
                Id = fields[0],
                Title = fields[1],
                Channels = channels,
                RadioLabels = fields.Count >= 4
                    ? LoadRadioLabels(fields[3])
                    : new List<string>()
            });
        }

        return NormalizeTabs(tabs);
    }

    public static string SaveTabs(IReadOnlyList<ChatTabSettings> tabs)
    {
        var normalized = NormalizeTabs(tabs);
        var saved = new List<string>();
        foreach (var tab in normalized)
        {
            saved.Add($"{EscapeValue(tab.Id)}|{EscapeValue(tab.Title)}|{tab.Channels}|{EscapeValue(SaveRadioLabels(tab.RadioLabels))}");
        }

        return string.Join("\n", saved);
    }

    public static List<ChatStyleSettings> LoadStyles(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new List<ChatStyleSettings>();

        var styles = new List<ChatStyleSettings>();
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = SplitEscaped(line, '|');
            if (fields.Count < 3)
                continue;

            styles.Add(new ChatStyleSettings
            {
                Target = fields[0],
                Color = string.IsNullOrWhiteSpace(fields[1]) ? null : fields[1],
                Font = fields[2],
                FontSize = fields.Count >= 4 ? NormalizeFontSize(fields[3]) : null
            });
        }

        return NormalizeStyles(styles);
    }

    public static string SaveStyles(IReadOnlyList<ChatStyleSettings> styles)
    {
        var normalized = NormalizeStyles(styles);
        var saved = new List<string>();
        foreach (var style in normalized)
        {
            saved.Add($"{EscapeValue(style.Target)}|{EscapeValue(style.Color ?? string.Empty)}|{EscapeValue(style.Font)}|{style.FontSize?.ToString() ?? string.Empty}");
        }

        return string.Join("\n", saved);
    }

    public static ChatSplitSettings LoadSplitPane(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new ChatSplitSettings();

        var fields = SplitEscaped(raw.Trim(), '|');
        return new ChatSplitSettings
        {
            Enabled = fields.Count >= 1 && fields[0] == "1",
            SecondaryTabId = fields.Count >= 2 && !string.IsNullOrWhiteSpace(fields[1])
                ? fields[1].Trim()
                : RadioTabId,
            SecondaryRatioPercent = fields.Count >= 3 && int.TryParse(fields[2], out var ratio)
                ? NormalizeSplitRatioPercent(ratio)
                : DefaultSplitSecondaryRatioPercent
        };
    }

    public static string SaveSplitPane(bool enabled, string secondaryTabId, int secondaryRatioPercent)
    {
        var tabId = string.IsNullOrWhiteSpace(secondaryTabId)
            ? RadioTabId
            : secondaryTabId.Trim();
        return $"{(enabled ? "1" : "0")}|{EscapeValue(tabId)}|{NormalizeSplitRatioPercent(secondaryRatioPercent)}";
    }

    public static List<ChatTabSettings> CreateDefaultTabs()
    {
        return new List<ChatTabSettings>
        {
            new() { Id = AllTabId, Title = "ALL", Channels = AllChannelBits },
            new() { Id = RadioTabId, Title = "RADIO", ChannelMask = ChatChannel.Radio }
        };
    }

    public static string CreateCustomTabId()
    {
        return string.Concat("custom-", Guid.NewGuid().ToString("N").Substring(0, 8));
    }

    public static string SanitizeTabTitle(string title)
    {
        var cleaned = new string(title
            .Where(c => !char.IsControl(c))
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "TAB";

        if (cleaned.Length > MaxTabTitleLength)
            cleaned = cleaned.Substring(0, MaxTabTitleLength);

        return cleaned.ToUpperInvariant();
    }

    public static ChatStyleTarget? GetTarget(string key)
    {
        return BaseStyleTargets.FirstOrDefault(target => string.Equals(target.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsValidStyleTarget(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return false;

        return GetTarget(key) != null ||
               key.StartsWith("label:", StringComparison.OrdinalIgnoreCase) && key.Length > "label:".Length;
    }

    public static ChatStyleSettings? ResolveStyle(IReadOnlyList<ChatStyleSettings> styles, ChatMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.Display?.ChannelLabel))
        {
            var labelKey = LabelKey(message.Display.ChannelLabel);
            var labelStyle = styles.FirstOrDefault(style => string.Equals(style.Target, labelKey, StringComparison.OrdinalIgnoreCase));
            if (labelStyle != null)
                return labelStyle;
        }

        var channelKey = ChannelKey(message.Channel);
        return styles.FirstOrDefault(style => string.Equals(style.Target, channelKey, StringComparison.OrdinalIgnoreCase));
    }

    public static Color? ResolveColor(ChatStyleSettings? style)
    {
        if (style?.Color == null)
            return null;

        return Color.TryFromHex(style.Color);
    }

    public static int? ResolveFontSize(ChatStyleSettings? style)
    {
        return NormalizeFontSize(style?.FontSize);
    }

    public static int? ResolveMarkupFontSize(string markup)
    {
        var fontMatch = FirstFontTag.Match(markup);
        if (!fontMatch.Success)
            return null;

        var sizeMatch = FontSizeAttribute.Match(fontMatch.Groups["attrs"].Value);
        if (!sizeMatch.Success)
            return null;

        return NormalizeFontSize(sizeMatch.Groups["size"].Value);
    }

    public static string ApplyFontMarkup(string markup, ChatStyleSettings? style, int? fallbackFontSize = null)
    {
        var styleFontSize = ResolveFontSize(style);
        var fallback = NormalizeFontSize(fallbackFontSize);

        if (FirstFontTag.IsMatch(markup))
        {
            return FirstFontTag.Replace(
                markup,
                match =>
                {
                    var attrs = match.Groups["attrs"].Value;
                    if (styleFontSize == null && FontSizeAttribute.IsMatch(attrs))
                        return match.Value;

                    return BuildFontTag(attrs, styleFontSize ?? fallback);
                },
                1);
        }

        var fontSize = styleFontSize ?? fallback;
        if (fontSize == null)
            return markup;

        return $"{BuildFontTag(string.Empty, fontSize)}{markup}[/font]";
    }

    public static string ApplyStyleMarkup(string markup, ChatStyleSettings? style, int? fallbackFontSize = null)
    {
        if (ResolveColor(style) is { } color)
        {
            var colorTag = $"[color={color.ToHex()}]";
            markup = FirstColorTag.IsMatch(markup)
                ? FirstColorTag.Replace(markup, colorTag, 1)
                : $"{colorTag}{markup}[/color]";
        }

        return ApplyFontMarkup(markup, style, fallbackFontSize);
    }

    public static string ChannelKey(ChatChannel channel)
    {
        return $"channel:{channel}";
    }

    public static string LabelKey(string label)
    {
        return $"label:{label.Trim().ToUpperInvariant()}";
    }

    public static string NormalizeRadioLabel(string? label)
    {
        return string.IsNullOrWhiteSpace(label)
            ? string.Empty
            : label.Trim().ToUpperInvariant();
    }

    public static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;

        var parsed = Color.TryFromHex(color.Trim());
        return parsed?.ToHex();
    }

    public static int? NormalizeFontSize(string? fontSize)
    {
        if (string.IsNullOrWhiteSpace(fontSize) || !int.TryParse(fontSize.Trim(), out var parsed))
            return null;

        return NormalizeFontSize(parsed);
    }

    private static int? NormalizeFontSize(int? fontSize)
    {
        if (fontSize == null)
            return null;

        return Math.Clamp(fontSize.Value, MinFontSize, MaxFontSize);
    }

    public static int NormalizeSplitRatioPercent(int ratio)
    {
        return Math.Clamp(ratio, MinSplitSecondaryRatioPercent, MaxSplitSecondaryRatioPercent);
    }

    private static string BuildFontTag(string existingAttrs, int? fontSize)
    {
        var attrs = existingAttrs;
        if (fontSize != null)
        {
            attrs = FontSizeAttribute.Replace(attrs, string.Empty, 1).TrimEnd();
            attrs += $" size={fontSize.Value}";
        }

        return $"[font{attrs}]";
    }

    private static List<ChatTabSettings> NormalizeTabs(IReadOnlyList<ChatTabSettings>? tabs)
    {
        if (tabs == null || tabs.Count == 0)
            return CreateDefaultTabs();

        var normalized = new List<ChatTabSettings>();
        var ids = new List<string>();
        var hasAll = false;

        foreach (var tab in tabs)
        {
            if (normalized.Count >= MaxTabs)
                break;

            var id = string.IsNullOrWhiteSpace(tab.Id)
                ? CreateCustomTabId()
                : tab.Id.Trim();

            while (ContainsIgnoreCase(ids, id))
                id = CreateCustomTabId();

            ids.Add(id);
            var isAll = string.Equals(id, AllTabId, StringComparison.OrdinalIgnoreCase);
            hasAll |= isAll;
            normalized.Add(new ChatTabSettings
            {
                Id = id,
                Title = isAll ? "ALL" : SanitizeTabTitle(tab.Title),
                Channels = isAll ? AllChannelBits : tab.Channels,
                RadioLabels = isAll ? new List<string>() : NormalizeRadioLabels(tab.RadioLabels)
            });
        }

        if (LooksLikeOldDefaultTabs(normalized))
            return CreateDefaultTabs();

        if (!hasAll)
        {
            normalized.Insert(0, CreateDefaultTabs()[0]);
            if (normalized.Count > MaxTabs)
                normalized.RemoveAt(normalized.Count - 1);
        }
        else
        {
            var allIndex = normalized.FindIndex(tab => string.Equals(tab.Id, AllTabId, StringComparison.OrdinalIgnoreCase));
            if (allIndex > 0)
            {
                var allTab = normalized[allIndex];
                normalized.RemoveAt(allIndex);
                normalized.Insert(0, allTab);
            }
        }

        return normalized.Count == 0 ? CreateDefaultTabs() : normalized;
    }

    private static bool LooksLikeOldDefaultTabs(IReadOnlyList<ChatTabSettings> tabs)
    {
        if (tabs.Count != 5)
            return false;

        return string.Equals(tabs[0].Id, AllTabId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(tabs[1].Id, "ic", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(tabs[2].Id, RadioTabId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(tabs[3].Id, "ooc", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(tabs[4].Id, AdminTabId, StringComparison.OrdinalIgnoreCase);
    }

    private static List<ChatStyleSettings> NormalizeStyles(IReadOnlyList<ChatStyleSettings>? styles)
    {
        if (styles == null || styles.Count == 0)
            return new List<ChatStyleSettings>();

        var normalized = new List<ChatStyleSettings>();
        var targets = new List<string>();

        foreach (var style in styles)
        {
            if (normalized.Count >= MaxStyles)
                break;

            if (!IsValidStyleTarget(style.Target) || ContainsIgnoreCase(targets, style.Target))
                continue;

            targets.Add(style.Target);
            var color = NormalizeColor(style.Color);
            var fontSize = NormalizeFontSize(style.FontSize);
            if (color == null && fontSize == null)
                continue;

            normalized.Add(new ChatStyleSettings
            {
                Target = style.Target,
                Color = color,
                Font = FontDefault,
                FontSize = fontSize
            });
        }

        return normalized;
    }

    private static bool ContainsIgnoreCase(List<string> values, string value)
    {
        foreach (var existing in values)
        {
            if (string.Equals(existing, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static List<string> LoadRadioLabels(string value)
    {
        var labels = new List<string>();
        foreach (var raw in value.Split(','))
        {
            var label = NormalizeRadioLabel(raw);
            if (string.IsNullOrWhiteSpace(label) || ContainsIgnoreCase(labels, label))
                continue;

            labels.Add(label);
        }

        return labels;
    }

    private static string SaveRadioLabels(IReadOnlyList<string> labels)
    {
        return string.Join(",", NormalizeRadioLabels(labels));
    }

    private static List<string> NormalizeRadioLabels(IReadOnlyList<string>? labels)
    {
        var normalized = new List<string>();
        if (labels == null)
            return normalized;

        foreach (var raw in labels)
        {
            var label = NormalizeRadioLabel(raw);
            if (string.IsNullOrWhiteSpace(label) || ContainsIgnoreCase(normalized, label))
                continue;

            normalized.Add(label);
        }

        return normalized;
    }

    private static string EscapeValue(string value)
    {
        var escaped = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            switch (c)
            {
                case '\\':
                    escaped.Append(@"\\");
                    break;
                case '|':
                    escaped.Append(@"\|");
                    break;
                case '\r':
                case '\n':
                    escaped.Append(' ');
                    break;
                default:
                    escaped.Append(c);
                    break;
            }
        }

        return escaped.ToString();
    }

    private static List<string> SplitEscaped(string value, char separator)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var escaping = false;

        foreach (var c in value)
        {
            if (escaping)
            {
                current.Append(c);
                escaping = false;
                continue;
            }

            if (c == '\\')
            {
                escaping = true;
                continue;
            }

            if (c == separator)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (escaping)
            current.Append('\\');

        fields.Add(current.ToString());
        return fields;
    }
}
