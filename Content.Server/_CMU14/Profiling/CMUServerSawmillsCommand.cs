using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.Profiling;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ServerSawmillsCommand : LocalizedCommands
{
    [Dependency] private ILogManager _logManager = default!;

    public override string Command => "serversawmills";
    public override string Description => "Lists sawmills (non-inherited) log level (or use --all/filters).";
    public override string Help => $"Usage: {Command} [--all] [filter|level] [level]";

    private static readonly string primaryClr = Color.Green.ToHex();
    private static readonly string secondaryClr = Color.Yellow.ToHex();

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        bool explicitAll = false;
        var filterArgs = new List<string>();
        foreach (var arg in args)
        {
            if (arg.Equals("--all", StringComparison.OrdinalIgnoreCase))
                explicitAll = true;
            else
                filterArgs.Add(arg);
        }

        if (!TryParseFilterArgs(shell, filterArgs, out var nameFilter, out var hasLevelFilter, out var wantInherited, out var levelFilter))
            return;

        bool showAll = nameFilter != null || hasLevelFilter || explicitAll;
        var sawmills = _logManager.AllSawmills.Where(s => nameFilter == null || s.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            .Where(s =>
            {
                if (hasLevelFilter)
                    return wantInherited ? s.Level == null : s.Level == levelFilter;
                if (!showAll) // skip inherited/default
                    return s.Level != null;
                return true;
            }).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (sawmills.Count == 0)
        {
            shell.WriteMarkup($"[color={secondaryClr}]No sawmills matching criteria.[/color]");
            return;
        }

        int col = Math.Max(40, sawmills.Max(s => s.Name.Length) + 2);
        foreach (var sawmill in sawmills)
        {
            var (levelText, colour) = GetLevelTextAndColour(sawmill.Level);
            var line = $"[color={primaryClr}]{sawmill.Name.PadRight(col)}[/color]" +
                $" [color={colour.ToHex()}]{levelText}[/color]";
            shell.WriteMarkup(line);
        }

        var suffix = showAll ? " (all)" : " (only overridden)";
        if (hasLevelFilter)
            suffix += wantInherited ? " with inherited level" : $" with level '{levelFilter}'";

        shell.WriteMarkup($"[color={secondaryClr}]--- {sawmills.Count} sawmill(s){suffix} ---[/color]");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        bool hasAll = args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase));
        var cleanedArgs = args.Where(a => !a.Equals("--all", StringComparison.OrdinalIgnoreCase)).ToArray();
        return cleanedArgs.Length switch
        {
            0 => CompletionResult.FromHintOptions(new[] { "--all" }, "[--all]"),
            1 => CompletionResult.FromHintOptions(new[] { "--all" }.Concat(Enum.GetNames<LogLevel>()).Append("null")
                    .Concat(_logManager.AllSawmills.Select(s => s.Name).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)), "<name or level>"),
            2 => CompletionResult.FromHintOptions(Enum.GetNames<LogLevel>().Append("null"), "<level filter>"),
            _ => CompletionResult.Empty
        };
    }

    private static (string text, Color colour) GetLevelTextAndColour(LogLevel? level)
    {
        return level switch
        {
            null => ("inherited", Color.DarkGray),
            LogLevel.Verbose => ("Verbose", Color.DarkGray),
            LogLevel.Debug => ("Debug", Color.Gray),
            LogLevel.Info => ("Info", Color.Cyan),
            LogLevel.Warning => ("Warning", Color.Yellow),
            LogLevel.Error => ("Error", Color.Red),
            LogLevel.Fatal => ("Fatal", Color.Magenta),
            _ => (level!.Value.ToString(), Color.White)
        };
    }

    private static bool TryParseFilterArgs(IConsoleShell shell, List<string> filterArgs, out string? nameFilter,
    out bool hasLevelFilter, out bool wantInherited, out LogLevel? levelFilter)
    {
        nameFilter = null;
        hasLevelFilter = false;
        wantInherited = false;
        levelFilter = null;

        if (filterArgs.Count == 0)
            return true;

        bool firstIsLevel = filterArgs[0] == "null"
            || Enum.TryParse<LogLevel>(filterArgs[0], ignoreCase: true, out _);
        if (firstIsLevel)
        {
            hasLevelFilter = true;
            wantInherited = filterArgs[0] == "null";
            if (!wantInherited)
            {
                Enum.TryParse<LogLevel>(filterArgs[0], ignoreCase: true, out var parsed);
                levelFilter = parsed;
            }
            return true;
        }

        nameFilter = filterArgs[0].Length > 0 ? filterArgs[0] : null;
        if (filterArgs.Count > 1)
        {
            hasLevelFilter = true;
            wantInherited = filterArgs[1] == "null";
            if (!wantInherited)
            {
                if (!Enum.TryParse<LogLevel>(filterArgs[1], ignoreCase: true, out var parsed))
                {
                    shell.WriteError($"Unknown level '{filterArgs[1]}'. Valid: {string.Join(", ", Enum.GetNames<LogLevel>())}, null");
                    return false;
                }
                levelFilter = parsed;
            }
        }

        return true;
    }
}
