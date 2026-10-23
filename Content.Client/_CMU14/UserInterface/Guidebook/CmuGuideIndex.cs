using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Content.Shared.Guidebook;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;

namespace Content.Client._CMU14.UserInterface.Guidebook;

/// <summary>
///     Plain-text copy of every guide page, so the guidebook can search inside pages and not just titles.
///     Built on the first search and kept until the entry list changes; the whole book is well under a megabyte.
/// </summary>
public sealed class CmuGuideIndex
{
    /// <summary>A page's text between two headings. <see cref="Number"/> counts headings in document order.</summary>
    public sealed record Section(string? Heading, int Number, string Text);

    /// <summary>One search result. The snippet is split so the matched run can be coloured.</summary>
    public sealed record Hit(
        GuideEntry Entry,
        string Title,
        bool IsTitle,
        int Section,
        string? Heading,
        string Before,
        string Match,
        string After);

    private const int MaxHitsPerPage = 2;
    private const int MaxPageHits = 60;
    private const int SnippetBefore = 46;
    private const int SnippetAfter = 74;

    private static readonly Regex TextLink = new(@"\[textlink=""([^""]*)""[^\]]*\]", RegexOptions.Compiled);
    private static readonly Regex Markup = new(@"\[/?[a-zA-Z]+(=[^\]]*)?\]", RegexOptions.Compiled);
    private static readonly Regex Tag = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex Space = new(@"[ \t]+", RegexOptions.Compiled);

    private readonly IResourceManager _resource;
    private readonly Dictionary<ProtoId<GuideEntryPrototype>, List<Section>> _pages = new();

    private Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> _entries = new();
    private bool _built;

    public CmuGuideIndex(IResourceManager resource)
    {
        _resource = resource;
    }

    public void SetEntries(Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry> entries)
    {
        _entries = entries;
        _pages.Clear();
        _built = false;
    }

    public List<Hit> Search(string query, Func<GuideEntry, string> displayName)
    {
        var hits = new List<Hit>();
        query = query.Trim();
        if (query.Length < 2)
            return hits;

        Build();

        var needle = query.ToLowerInvariant();
        var pageHits = new List<Hit>();

        foreach (var entry in _entries.Values)
        {
            var name = displayName(entry);
            var at = name.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal);
            if (at >= 0)
            {
                hits.Add(new Hit(entry, name, true, -1, null,
                    name.Substring(0, at),
                    name.Substring(at, needle.Length),
                    name.Substring(at + needle.Length)));
            }

            if (!_pages.TryGetValue(entry.Id, out var sections))
                continue;

            var found = 0;
            foreach (var section in sections)
            {
                if (found >= MaxHitsPerPage)
                    break;

                var index = section.Text.ToLowerInvariant().IndexOf(needle, StringComparison.Ordinal);
                if (index < 0)
                    continue;

                found++;
                pageHits.Add(Snippet(entry, name, section, index, needle.Length));
            }
        }

        hits.AddRange(pageHits.Take(MaxPageHits));
        return hits;
    }

    private static Hit Snippet(GuideEntry entry, string title, Section section, int index, int length)
    {
        var text = section.Text;
        var start = Math.Max(0, index - SnippetBefore);
        var end = Math.Min(text.Length, index + length + SnippetAfter);

        var before = text.Substring(start, index - start);
        var after = text.Substring(index + length, end - index - length);

        if (start > 0)
            before = "..." + before;

        if (end < text.Length)
            after += "...";

        return new Hit(entry, title, false, section.Number, section.Heading, before, text.Substring(index, length), after);
    }

    private void Build()
    {
        if (_built)
            return;

        _built = true;
        foreach (var entry in _entries.Values)
        {
            if (_pages.ContainsKey(entry.Id))
                continue;

            _pages[entry.Id] = ReadSections(entry);
        }
    }

    private List<Section> ReadSections(GuideEntry entry)
    {
        var sections = new List<Section>();
        string raw;

        try
        {
            using var file = _resource.ContentFileReadText(entry.Text);
            raw = file.ReadToEnd();
        }
        catch (Exception)
        {
            return sections;
        }

        var heading = (string?) null;
        var number = -1;
        var body = new StringBuilder();

        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var text = Strip(line);
            if (text.Length == 0)
                continue;

            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                sections.Add(new Section(heading, number, body.ToString()));
                body.Clear();
                number++;
                heading = text.TrimStart('#').Trim();
                continue;
            }

            body.Append(text);
            body.Append(' ');
        }

        sections.Add(new Section(heading, number, body.ToString()));
        return sections;
    }

    private static string Strip(string line)
    {
        line = TextLink.Replace(line, "$1");
        line = Markup.Replace(line, string.Empty);
        line = Tag.Replace(line, string.Empty);
        return Space.Replace(line, " ").Trim();
    }
}
