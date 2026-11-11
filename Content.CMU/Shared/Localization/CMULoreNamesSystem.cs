using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.CMU14.Localization;

/// <summary>
///     Shows RMC's renamed brands and organisations (UNMC, Xenonid, WeYa...) under their lore names (USCM, Xenomorph,
///     WeYu...) in entity names, descriptions and suffixes, without touching the prototypes themselves.
///     At startup it writes an <c>ent-ID</c> localization override for every entity whose text needs converting into a
///     virtual locale file, then reloads localization. Entity text already localized through an <c>ent-</c> message
///     is left alone, since a second message with the same ID can't override it reliably.
/// </summary>
public sealed class CMULoreNamesSystem : EntitySystem
{
    private static readonly ResPath LocaleFile = new("/Locale/en-US/cmu-lore-names.ftl");

    // Longest phrases first so the full names win over their abbreviations.
    private static readonly (Regex Pattern, string Replacement)[] Replacements =
    [
        (new Regex(@"\bUnited Nations Marine Corps\b"), "United States Colonial Marine Corps"),
        (new Regex(@"\bUnited Nations Marines\b"), "United States Colonial Marines"),
        (new Regex(@"\bUNMC\b"), "USCM"),
        (new Regex(@"\bXenonid"), "Xenomorph"),
        (new Regex(@"\bxenonid"), "xenomorph"),
        (new Regex(@"\bXENONID"), "XENOMORPH"),
        (new Regex(@"\bWeston[- ]Yamada\b"), "Weyland-Yutani"),
        (new Regex(@"\bWe-Ya\b"), "We-Yu"),
        (new Regex(@"\bWeYa\b"), "WeYu"),
        (new Regex(@"\bWEYA\b"), "WEYU"),
        (new Regex(@"\bThree Star Empire\b"), "Three World Empire"),
        (new Regex(@"\bTSE\b"), "TWE"),
        (new Regex(@"\bSPP\b"), "UPP"),
        (new Regex(@"\bRoyal Crown Marines\b"), "Royal Marines Commando"),
        // Not "RCM-P", the colonial police outposts on Souter.
        (new Regex(@"\bRCM\b(?!-P)"), "RMC"),
    ];

    // Cheap first pass so most text skips the replacements entirely.
    private static readonly Regex Candidates = new(@"UNMC|United Nations Marine|[Xx]enonid|XENONID|Weston|We-?Ya|WEYA|Three Star|TSE|SPP|Royal Crown|RCM");

    [Dependency] private ILocalizationManager _loc = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IResourceManager _resources = default!;

    private CMULoreNamesRoot? _root;

    public override void Initialize()
    {
        base.Initialize();
        _prototypes.PrototypesReloaded += OnPrototypesReloaded;
        Rebuild();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _prototypes.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<EntityPrototype>())
            Rebuild();
    }

    /// <summary>
    ///     Converts RMC's names in a piece of player-facing text.
    /// </summary>
    public static string Convert(string text)
    {
        if (!Candidates.IsMatch(text))
            return text;

        foreach (var (pattern, replacement) in Replacements)
        {
            text = pattern.Replace(text, replacement);
        }

        return text;
    }

    private void Rebuild()
    {
        if (_root == null)
        {
            // The resource manager outlives this system (e.g. pooled test instances), so only mount once.
            if (_resources.ContentFileExists(LocaleFile))
                return;

            _root = new CMULoreNamesRoot(LocaleFile);
            _resources.AddRoot(ResPath.Root, _root);
        }

        var ftl = new StringBuilder();
        foreach (var proto in _prototypes.EnumeratePrototypes<EntityPrototype>())
        {
            var locId = $"ent-{proto.ID}";
            if (_root.Ids.Contains(locId))
            {
                // Ours from last time; rebuild it from the prototype.
            }
            else if (_loc.HasString(locId))
            {
                continue;
            }

            var name = proto.SetName ?? proto.Name;
            var desc = proto.SetDesc ?? proto.Description;
            var suffix = proto.SetSuffix ?? proto.EditorSuffix;
            var newName = Convert(name);
            var newDesc = Convert(desc);
            var newSuffix = suffix == null ? null : Convert(suffix);
            if (newName == name && newDesc == desc && newSuffix == suffix)
                continue;

            ftl.Append(locId).Append(" = ").AppendLine(Literal(newName));
            // With any attribute present the engine expects a .desc too, so always write it.
            ftl.Append("    .desc = ").AppendLine(Literal(newDesc));
            if (!string.IsNullOrEmpty(newSuffix))
                ftl.Append("    .suffix = ").AppendLine(Literal(newSuffix));
        }

        var contents = ftl.ToString();
        if (contents == _root.Contents)
            return;

        _root.SetContents(contents);
        _loc.ReloadLocalizations();
    }

    /// <summary>
    ///     The whole text as one Fluent string literal, so braces, brackets, leading dots and newlines can't break it.
    /// </summary>
    private static string Literal(string text)
    {
        var builder = new StringBuilder("{ \"");
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\n':
                    builder.Append("\\u000A");
                    break;
                case '\r':
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        return builder.Append("\" }").ToString();
    }
}

/// <summary>
///     A content root holding the one generated locale file.
/// </summary>
internal sealed class CMULoreNamesRoot(ResPath file) : IContentRoot
{
    private readonly ResPath _relative = file.ToRelativePath();
    private byte[] _bytes = [];

    public string Contents { get; private set; } = string.Empty;

    public readonly HashSet<string> Ids = new();

    public void SetContents(string contents)
    {
        Contents = contents;
        _bytes = Encoding.UTF8.GetBytes(contents);
        Ids.Clear();
        foreach (var line in contents.Split('\n'))
        {
            if (line.StartsWith("ent-"))
                Ids.Add(line[..line.IndexOf(' ')]);
        }
    }

    public void Mount()
    {
    }

    public bool TryGetFile(ResPath relPath, [NotNullWhen(true)] out Stream? stream)
    {
        if (relPath != _relative)
        {
            stream = null;
            return false;
        }

        stream = new MemoryStream(_bytes, false);
        return true;
    }

    public bool FileExists(ResPath relPath)
    {
        return relPath == _relative;
    }

    public IEnumerable<ResPath> FindFiles(ResPath path)
    {
        if (_relative.TryRelativeTo(path, out _) || path == ResPath.Self)
            yield return _relative;
    }

    public IEnumerable<string> GetRelativeFilePaths()
    {
        yield return _relative.ToString();
    }
}
