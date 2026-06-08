using System.Linq;
using System.Text.RegularExpressions;
using Content.Shared.Database;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Administration.UI.CustomControls;

public readonly record struct AdminLogMessageDetail(string Label, string Value);

public static class AdminLogMessageFormatter
{
    public static readonly Color EntityColor = Color.FromHex("#A9D18E");
    public static readonly Color PlayerColor = Color.FromHex("#F3D38A");
    public static readonly Color PrototypeColor = Color.FromHex("#8DB4E2");
    public static readonly Color MetadataColor = Color.FromHex("#798392");
    public static readonly Color DamageColor = Color.FromHex("#FF8E7A");
    public static readonly Color MessageColor = Color.FromHex("#D6B8FF");
    public static readonly Color CoordinateColor = Color.FromHex("#7FD6D6");
    public static readonly Color AdminColor = Color.FromHex("#F7B267");

    private const int MaxDetails = 3;
    private const int MaxDetailValueLength = 32;

    private static readonly Regex EntityRepresentationRegex = new(
        @"(?<name>[\p{L}\p{N}_#' .\-]{2,90})\s+\((?<details>[^()\r\n]{1,180}(?:/n|,)[^()\r\n]{0,180})\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DamageRegex = new(
        @"\b(?<value>\d+(?:\.\d+)?)\s+(?<kind>(?:[A-Za-z]+\s+)?damage)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex CoordinateRegex = new(
        @"\b(?:[A-Z][A-Za-z0-9_ -]{1,40}:\s*)?-?\d+(?:\.\d+)?,\s*-?\d+(?:\.\d+)?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedTextRegex = new(
        "\"[^\"]{1,200}\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex AccountRegex = new(
        @"\b[A-Za-z0-9_.-]+@[A-Za-z0-9_.-]+\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] NameSeparators =
    [
        " owned by ",
        " shot by ",
        " hit ",
        " healed ",
        " damaged ",
        " deleted ",
        " spawned ",
        " inserted ",
        " placed ",
        " using ",
        " with ",
        " from ",
        " into ",
        " by ",
        " to ",
        " on ",
        " in ",
        " as ",
    ];

    private static readonly string[] LeadingNameSeparators =
    [
        "shot by ",
        "hit ",
        "healed ",
        "damaged ",
        "deleted ",
        "spawned ",
        "inserted ",
        "placed ",
        "using ",
        "with ",
        "from ",
        "into ",
        "by ",
        "to ",
        "on ",
        "in ",
        "as ",
    ];

    public static FormattedMessage Format(LogType type, string message)
    {
        var formatted = new FormattedMessage();
        AppendEntityAwareText(formatted, type, message);
        return formatted;
    }

    public static List<AdminLogMessageDetail> GetDetails(LogType type, string message)
    {
        var details = new List<AdminLogMessageDetail>(MaxDetails);

        foreach (Match match in EntityRepresentationRegex.Matches(message))
        {
            if (TryAddFirstPrototype(details, match.Groups["details"].Value))
                break;
        }

        var damageMatch = DamageRegex.Match(message);
        if (damageMatch.Success)
            AddDetail(details, "damage", damageMatch.Groups["value"].Value);

        var coordinateMatch = CoordinateRegex.Match(message);
        if (coordinateMatch.Success)
            AddDetail(details, "coord", coordinateMatch.Value);

        if (type == LogType.Chat)
            AddChatDetails(details, message);

        if (IsAdminCommandType(type))
            AddCommandDetails(details, message);

        return details;
    }

    private static void AppendEntityAwareText(FormattedMessage formatted, LogType type, string message)
    {
        var position = 0;

        foreach (Match match in EntityRepresentationRegex.Matches(message))
        {
            if (!match.Success || match.Index < position)
                continue;

            var nameGroup = match.Groups["name"];
            var detailsGroup = match.Groups["details"];
            var nameOffset = FindSemanticNameOffset(nameGroup.Value);
            var semanticNameStart = nameGroup.Index + nameOffset;

            if (semanticNameStart < position)
                continue;

            AppendInlineText(formatted, type, message[position..semanticNameStart]);

            var name = message[semanticNameStart..(nameGroup.Index + nameGroup.Length)];
            AppendColored(formatted, name, LooksLikePlayerDetails(detailsGroup.Value) ? PlayerColor : EntityColor);
            AppendColored(formatted, " (", MetadataColor);
            AppendDetailText(formatted, detailsGroup.Value);
            AppendColored(formatted, ")", MetadataColor);

            position = match.Index + match.Length;
        }

        if (position < message.Length)
            AppendInlineText(formatted, type, message[position..]);
    }

    private static void AppendInlineText(FormattedMessage formatted, LogType type, string text)
    {
        if (text.Length == 0)
            return;

        var spans = new List<InlineSpan>();

        AddMatches(spans, DamageRegex.Matches(text), DamageColor, priority: 40);
        AddMatches(spans, CoordinateRegex.Matches(text), CoordinateColor, priority: 30);
        AddMatches(spans, QuotedTextRegex.Matches(text), MessageColor, priority: 25);
        AddMatches(spans, AccountRegex.Matches(text), AdminColor, priority: 20);

        if (type == LogType.Chat)
            AddTailAfterColon(spans, text, MessageColor, priority: 10);

        if (IsAdminCommandType(type))
            AddCommandTail(spans, text);

        AppendSpans(formatted, text, spans);
    }

    private static void AppendDetailText(FormattedMessage formatted, string details)
    {
        var segmentStart = 0;

        for (var i = 0; i <= details.Length; i++)
        {
            if (i < details.Length && details[i] != ',')
                continue;

            var segment = details[segmentStart..i];
            AppendDetailSegment(formatted, segment, segmentStart == 0);

            if (i < details.Length)
                AppendColored(formatted, ",", MetadataColor);

            segmentStart = i + 1;
        }
    }

    private static void AppendDetailSegment(FormattedMessage formatted, string segment, bool firstSegment)
    {
        var leading = segment.Length - segment.TrimStart().Length;
        var trailing = segment.Length - segment.TrimEnd().Length;
        var valueStart = leading;
        var valueLength = Math.Max(0, segment.Length - leading - trailing);

        if (leading > 0)
            AppendColored(formatted, segment[..leading], MetadataColor);

        if (valueLength > 0)
        {
            var value = segment.Substring(valueStart, valueLength);
            var color = GetDetailSegmentColor(value, firstSegment);
            AppendColored(formatted, value, color);
        }

        if (trailing > 0)
            AppendColored(formatted, segment[^trailing..], MetadataColor);
    }

    private static Color GetDetailSegmentColor(string value, bool firstSegment)
    {
        if (value.Contains('@'))
            return AdminColor;

        if (!firstSegment && LooksLikePrototype(value))
            return PrototypeColor;

        return MetadataColor;
    }

    private static void AppendSpans(FormattedMessage formatted, string text, List<InlineSpan> spans)
    {
        if (spans.Count == 0)
        {
            formatted.AddText(text);
            return;
        }

        spans.Sort((a, b) =>
        {
            var startComparison = a.Start.CompareTo(b.Start);
            return startComparison != 0
                ? startComparison
                : b.Priority.CompareTo(a.Priority);
        });

        var position = 0;
        var coveredUntil = 0;

        foreach (var span in spans)
        {
            if (span.Start < coveredUntil)
                continue;

            if (span.Start > position)
                formatted.AddText(text[position..span.Start]);

            AppendColored(formatted, text.Substring(span.Start, span.Length), span.Color);
            position = span.Start + span.Length;
            coveredUntil = position;
        }

        if (position < text.Length)
            formatted.AddText(text[position..]);
    }

    private static void AppendColored(FormattedMessage formatted, string text, Color color)
    {
        if (text.Length == 0)
            return;

        formatted.PushColor(color);
        formatted.AddText(text);
        formatted.Pop();
    }

    private static void AddMatches(List<InlineSpan> spans, MatchCollection matches, Color color, int priority)
    {
        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            spans.Add(new InlineSpan(match.Index, match.Length, color, priority));
        }
    }

    private static void AddTailAfterColon(List<InlineSpan> spans, string text, Color color, int priority)
    {
        var colon = text.IndexOf(':');
        if (colon < 0 || colon == text.Length - 1)
            return;

        var start = colon + 1;
        while (start < text.Length && text[start] == ' ')
        {
            start++;
        }

        if (start < text.Length)
            spans.Add(new InlineSpan(start, text.Length - start, color, priority));
    }

    private static void AddCommandTail(List<InlineSpan> spans, string text)
    {
        AddTailAfterKeyword(spans, text, "issued command:", MessageColor, priority: 15);
        AddTailAfterKeyword(spans, text, "issued command of", MessageColor, priority: 15);
        AddTailAfterKeyword(spans, text, "with arguments:", MessageColor, priority: 15);
    }

    private static void AddTailAfterKeyword(
        List<InlineSpan> spans,
        string text,
        string keyword,
        Color color,
        int priority)
    {
        var keywordIndex = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (keywordIndex < 0)
            return;

        var start = keywordIndex + keyword.Length;
        while (start < text.Length && text[start] == ' ')
        {
            start++;
        }

        if (start < text.Length)
            spans.Add(new InlineSpan(start, text.Length - start, color, priority));
    }

    private static int FindSemanticNameOffset(string name)
    {
        foreach (var separator in LeadingNameSeparators)
        {
            if (name.StartsWith(separator, StringComparison.OrdinalIgnoreCase))
                return separator.Length;
        }

        var best = -1;
        var bestLength = 0;
        foreach (var separator in NameSeparators)
        {
            var index = name.LastIndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (index <= best)
                continue;

            best = index;
            bestLength = separator.Length;
        }

        if (best >= 0)
            return best + bestLength;

        var leading = 0;
        while (leading < name.Length && char.IsWhiteSpace(name[leading]))
        {
            leading++;
        }

        return leading;
    }

    private static bool LooksLikePlayerDetails(string details)
    {
        return details.Contains('@') ||
               details.Contains("Job", StringComparison.Ordinal);
    }

    private static bool LooksLikePrototype(string value)
    {
        if (value.Length < 4 ||
            value.Contains(' ') ||
            value.Contains('/') ||
            value.Contains('@') ||
            value.All(char.IsDigit))
        {
            return false;
        }

        return value.Any(char.IsUpper) && value.Any(char.IsLower);
    }

    private static bool TryAddFirstPrototype(List<AdminLogMessageDetail> details, string detailText)
    {
        foreach (var segment in detailText.Split(','))
        {
            var value = segment.Trim();
            if (!LooksLikePrototype(value))
                continue;

            AddDetail(details, "proto", value);
            return true;
        }

        return false;
    }

    private static void AddChatDetails(List<AdminLogMessageDetail> details, string message)
    {
        const string over = " over ";
        var overIndex = message.IndexOf(over, StringComparison.OrdinalIgnoreCase);
        var colonIndex = message.IndexOf(':');
        if (overIndex < 0 || colonIndex <= overIndex)
            return;

        var channel = message[(overIndex + over.Length)..colonIndex].Trim();
        AddDetail(details, "channel", channel);
    }

    private static void AddCommandDetails(List<AdminLogMessageDetail> details, string message)
    {
        var command = GetCommandAfter(message, "issued command:");
        command ??= GetCommandAfter(message, "issued command of");

        if (command != null)
            AddDetail(details, "command", command);
    }

    private static string? GetCommandAfter(string message, string marker)
    {
        var markerIndex = message.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
            return null;

        var start = markerIndex + marker.Length;
        while (start < message.Length && message[start] == ' ')
        {
            start++;
        }

        var end = start;
        while (end < message.Length && !char.IsWhiteSpace(message[end]) && message[end] != '.')
        {
            end++;
        }

        return end > start ? message[start..end] : null;
    }

    private static void AddDetail(List<AdminLogMessageDetail> details, string label, string value)
    {
        if (details.Count >= MaxDetails)
            return;

        value = TrimDetailValue(value);
        if (value.Length == 0)
            return;

        var detail = new AdminLogMessageDetail(label, value);
        if (!details.Contains(detail))
            details.Add(detail);
    }

    private static string TrimDetailValue(string value)
    {
        value = value.Trim();
        if (value.Length <= MaxDetailValueLength)
            return value;

        return $"{value[..(MaxDetailValueLength - 3)]}...";
    }

    private static bool IsAdminCommandType(LogType type)
    {
        return type is LogType.AdminCommands or LogType.RMCAdminCommandLogging;
    }

    private readonly record struct InlineSpan(int Start, int Length, Color Color, int Priority);
}
