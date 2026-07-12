using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._CMU14.Administration.Console;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ServerSawmillsCommand : LocalizedCommands
{
    [Dependency] private ILogManager _logManager = default!;
    private static readonly string PrimaryClr = Color.Green.ToHex();
    private static readonly string SecondaryClr = Color.Yellow.ToHex();
    public override string Command => "serversawmills";
    public override string Description => "Lists sawmills (non-info) log level (or use --all/filters).";
    public override string Help => $"Usage: {Command} [--all] [filter|level] [level]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var explicitAll = false;
        var filterArgs = new List<string>();
        foreach (string arg in args)
        {
            if (arg.Equals("--all", StringComparison.OrdinalIgnoreCase))
                explicitAll = true;
            else
                filterArgs.Add(arg);
        }

        if (!ServerSawmillsCommand.TryParseFilterArgs(shell, filterArgs, out string? nameFilter,
            out bool hasLevelFilter,
            out bool wantInherited, out LogLevel? levelFilter))
            return;

        bool showAll = nameFilter != null || hasLevelFilter || explicitAll;
        List<ISawmill> sawmills = _logManager.AllSawmills
            .Where(s => nameFilter == null || s.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
            .Where(s =>
            {
                if (hasLevelFilter)
                    return wantInherited ? s.Level == null : s.Level == levelFilter;
                if (!showAll) // skip inherited/default
                    return s.Level != null && s.Level != LogLevel.Info;
                return true;
            }).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();

        if (sawmills.Count == 0)
        {
            shell.WriteMarkup($"[color={SecondaryClr}]No sawmills matching criteria.[/color]");
            return;
        }

        int col = Math.Max(40, sawmills.Max(s => s.Name.Length) + 2);
        foreach (ISawmill sawmill in sawmills)
        {
            (string levelText, Color colour) = ServerSawmillsCommand.GetLevelTextAndColour(sawmill.Level);
            string line = $"[color={PrimaryClr}]{sawmill.Name.PadRight(col)}[/color]" +
                $" [color={colour.ToHex()}]{levelText}[/color]";
            shell.WriteMarkup(line);
        }

        string suffix = showAll ? " (all)" : " (only overridden)";
        if (hasLevelFilter)
            suffix += wantInherited ? " with inherited level" : $" with level '{levelFilter}'";

        shell.WriteMarkup($"[color={SecondaryClr}]--- {sawmills.Count} sawmill(s){suffix} ---[/color]");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        string[] cleanedArgs = args.Where(a => !a.Equals("--all", StringComparison.OrdinalIgnoreCase)).ToArray();
        return cleanedArgs.Length switch
        {
            0 => CompletionResult.FromHintOptions(["--all"], "[--all]"), 1 => CompletionResult.FromHintOptions(
                new[] { "--all" }.Concat(Enum.GetNames<LogLevel>()).Append("null")
                    .Concat(_logManager.AllSawmills.Select(s => s.Name)
                        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)), "<name or level>"),
            2 => CompletionResult.FromHintOptions(Enum.GetNames<LogLevel>().Append("null"), "<level filter>"),
            _ => CompletionResult.Empty
        };
    }

    private static (string text, Color colour) GetLevelTextAndColour(LogLevel? level)
    {
        return level switch
        {
            null             => ("inherited", Color.DarkGray), LogLevel.Verbose => ("Verbose", Color.DarkGray),
            LogLevel.Debug   => ("Debug", Color.Gray), LogLevel.Info            => ("Info", Color.Cyan),
            LogLevel.Warning => ("Warning", Color.Yellow), LogLevel.Error       => ("Error", Color.Red),
            LogLevel.Fatal   => ("Fatal", Color.Magenta), _                     => (level.Value.ToString(), Color.White)
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
            || Enum.TryParse<LogLevel>(filterArgs[0], true, out _);
        if (firstIsLevel)
        {
            hasLevelFilter = true;
            wantInherited = filterArgs[0] == "null";
            if (!wantInherited)
            {
                Enum.TryParse(filterArgs[0], true, out LogLevel parsed);
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
                if (!Enum.TryParse(filterArgs[1], true, out LogLevel parsed))
                {
                    shell.WriteError($"Unknown level '{filterArgs[1]}'. Valid: {
                        string.Join(", ", Enum.GetNames<LogLevel>())}, null");
                    return false;
                }

                levelFilter = parsed;
            }
        }

        return true;
    }
}
