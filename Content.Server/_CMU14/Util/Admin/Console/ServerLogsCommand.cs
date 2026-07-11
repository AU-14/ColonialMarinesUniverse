using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Administration;
using Content.Shared._CMU14.Administration.Console;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._CMU14.Administration.Console;

[AdminCommand(AdminFlags.Host)]
public sealed partial class ServerLogsCommand : LocalizedCommands
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IEntityManager _entityManager = default!;
    [Dependency] private ServerLogsDownloadManager _download = default!;

    // private static readonly string LogDir = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "bin", "logs", "logs")); // yes its double
    private static readonly string LogDir = Path.GetFullPath("/App/persist/logs/live");
    private static readonly string PrimaryClr = Color.Green.ToHex();
    private static readonly string SecondaryClr = Color.Yellow.ToHex();
    private static readonly string[] LogSearchPatterns = ["*.log", "*.txt"];
    private static readonly char[] DirectorySeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];
    private const int MaxLines = 5000; // client default: con.max_entries=3000
    public override string Command => "serverlogs";

    public override string Description
        => "Prints or downloads the server (or specified file) logs, with --tail to chat.";

    public override string Help => $"Usage: {Command} [filter] [lines] | {Command} --list | {Command} --download [--file <name>] | {Command} --file <name> [filter] | {Command} --follow [filter] | {Command} --stop";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Contains("--stop"))
        {
            if (shell.Player == null)
                return;

            if (!_playerManager.TryGetSessionById(shell.Player.UserId, out ICommonSession? session))
                return;

            if (session.AttachedEntity is { } uid && _entityManager.HasComponent<ServerLogsFollowerComponent>(uid))
            {
                _entityManager.RemoveComponent<ServerLogsFollowerComponent>(uid);
                shell.WriteLine("Follow (tail) stopped for server logs.");
            }
            else
                shell.WriteError("No active logs follow to stop.");

            return;
        }

        if (args is ["--list", ..])
        {
            ListLogFiles(shell);
            return;
        }

        (bool followMode, bool downloadMode, string? filter, int lineCount, string? explicitFile) = ServerLogsCommand.ParseArgs(args);
        if (downloadMode && followMode)
        {
            shell.WriteError("--download cannot be combined with --follow or --tail.");
            return;
        }

        FileInfo? logFile = ResolveLogFile(explicitFile);
        if (logFile == null)
        {
            shell.WriteError(string.IsNullOrEmpty(explicitFile)
                ? "No default server log file found, try specifying one."
                : "Log file not found or not allowed.");
            return;
        }

        if (downloadMode)
        {
            await DownloadLogFile(shell, logFile);
            return;
        }

        try
        {
            List<string> lines = ServerLogsCommand.ReadLastLines(logFile.FullName, lineCount)
                .Where(l => !l.Contains("serverlogs"))
                .Where(l => filter == null || l.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            var output = new StringBuilder();
            string filterPrefix = !string.IsNullOrEmpty(filter) ? $"filtered '{filter}' on " : string.Empty;
            output.AppendLine($"[color={PrimaryClr}]--- {logFile.Name} ({filterPrefix}last {lines.Count} lines) ---[/color]");

            foreach (string line in lines)
            {
                // logs saved with ANSI coloring
                string markupLine = ServerLogsCommand.ConvertAnsiToMarkup(line);
                output.AppendLine($">{markupLine}");
            }

            output.AppendLine($"[color={PrimaryClr}]--- end of {filterPrefix}{lines.Count} log lines ---[/color]");
            shell.WriteMarkup(output.ToString());

            if (followMode)
            {
                if (shell.Player == null) return;
                if (!_playerManager.TryGetSessionById(shell.Player.UserId, out ICommonSession? session))
                {
                    shell.WriteError("Unable to find your session...");
                    return;
                }

                if (session.AttachedEntity is not { } uid)
                {
                    shell.WriteError("You must have a mind (spawned), to subscribe to server logs tail.");
                    return;
                }

                var comp = _entityManager.EnsureComponent<ServerLogsFollowerComponent>(uid);
                comp.FilePath = logFile.FullName;
                comp.LastPosition = logFile.Length; // start from end
                comp.Filter = filter;
                comp.Session = session;

                if (string.IsNullOrEmpty(filter))
                {
                    shell.WriteMarkup($"[color={SecondaryClr}]No filter set, consider using a filter to reduce noise.[/color]");
                }

                shell.WriteMarkup($"[color={PrimaryClr}]Now following {logFile.Name} for '{filter}', use 'serverlogs --stop' to cancel.[/color]");
            }
        }
        catch (Exception ex) { shell.WriteError($"Failed to read log file '{logFile.Name}': {ex.Message}"); }
    }

    private async Task DownloadLogFile(IConsoleShell shell, FileInfo logFile)
    {
        if (shell.Player == null)
        {
            shell.WriteError("You must run serverlogs --download from a connected client console.");
            return;
        }

        if (!_playerManager.TryGetSessionById(shell.Player.UserId, out ICommonSession? session))
        {
            shell.WriteError("Unable to find your session.");
            return;
        }

        logFile.Refresh();
        if (!TryCreateSafeLogFileInfo(logFile.FullName, out logFile))
        {
            shell.WriteError("Log file is no longer available or is not allowed.");
            return;
        }

        if (logFile.Length > ServerLogsDownloadConstants.MaxDownloadBytes)
        {
            shell.WriteError($"Log file is too large to download safely. Limit: {ByteHelpers.FormatBytes(ServerLogsDownloadConstants.MaxDownloadBytes)}.");
            return;
        }

        try
        {
            shell.WriteLine($"Starting download of {logFile.Name} ({ByteHelpers.FormatBytes(logFile.Length)}).");
            await _download.SendLogFile(session, logFile);
            shell.WriteLine($"Finished sending {logFile.Name}. It will save on your client under user data path {ServerLogsDownloadConstants.ClientDownloadDirectory}.");
        }
        catch (Exception)
        {
            shell.WriteError($"Failed to download log file '{logFile.Name}'.");
        }
    }

    private void ListLogFiles(IConsoleShell shell)
    {
        var files = EnumerateLogFiles()
            .OrderBy(f => f.LastWriteTimeUtc)
            .ToList();

        if (files.Count == 0) { shell.WriteLine("No log files found."); return; }

        shell.WriteMarkup($"[color={PrimaryClr}]--- {files.Count} log file(s) ---[/color]");
        foreach (var file in files)
        {
            var color = file.Length == 0 ? SecondaryClr : PrimaryClr;
            var sizeStr = file.Length > 0 ? $"{file.Length,8:N0} B" : "  empty  ";
            shell.WriteMarkup($"[color={color}]{file.Name,-40}[/color]" +
                $" [color={SecondaryClr}]{sizeStr}  {file.LastWriteTimeUtc:yyyy-MM-dd HH:mm:ss} UTC[/color]");
        }
    }

    private FileInfo? ResolveLogFile(string? explicitFile)
    {
        if (!string.IsNullOrEmpty(explicitFile))
            return TryFindLogFile(explicitFile);

        return EnumerateLogFiles("server-log*.txt")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    private FileInfo? TryFindLogFile(string fileName)
    {
        if (!IsSafeExplicitFileName(fileName))
            return null;

        var candidates = new List<string> { fileName };
        if (string.IsNullOrEmpty(Path.GetExtension(fileName)))
        {
            candidates.Add(fileName + ".txt");
            candidates.Add(fileName + ".log");
        }

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(Path.Combine(LogDir, candidate));
            if (TryCreateSafeLogFileInfo(fullPath, out var info))
                return info;
        }

        return null;
    }

    private static IEnumerable<FileInfo> EnumerateLogFiles(string? pattern = null)
    {
        if (!Directory.Exists(LogDir))
            yield break;

        var searchPatterns = pattern != null ? new[] { pattern } : LogSearchPatterns;
        foreach (var searchPattern in searchPatterns)
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(LogDir, searchPattern, SearchOption.TopDirectoryOnly);
            }
            catch
            {
                yield break;
            }

            foreach (var file in files)
            {
                var fullPath = Path.GetFullPath(file);
                if (TryCreateSafeLogFileInfo(fullPath, out var info))
                    yield return info;
            }
        }
    }

    private static bool IsSafeExplicitFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || Path.IsPathRooted(fileName)
            || fileName.IndexOfAny(DirectorySeparators) != -1
            || fileName is "." or ".."
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return string.IsNullOrEmpty(extension) || ServerLogsDownloadConstants.IsAllowedLogExtension(extension);
    }

    private static bool TryCreateSafeLogFileInfo(string fullPath, out FileInfo fileInfo)
    {
        fileInfo = new FileInfo(fullPath);
        if (!File.Exists(fullPath)
            || !IsInLogDirectory(fullPath)
            || !ServerLogsDownloadConstants.IsAllowedLogExtension(fileInfo.Extension)
            || (fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            fileInfo = null!;
            return false;
        }

        return true;
    }

    private static bool IsInLogDirectory(string fullPath)
    {
        var relative = Path.GetRelativePath(LogDir, fullPath);
        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    // supports standard SGR colors (30‑37 + 90‑97) and reset (0)
    internal static string ConvertAnsiToMarkup(string ansiLine)
    {
        var sb = new StringBuilder();
        var i = 0;
        var inColor = false;

        while (i < ansiLine.Length)
        {
            // detect ANSI escape start \e[ or \e] & handle OSC sequences
            if (ansiLine[i] == '\e' && i + 1 < ansiLine.Length && ansiLine[i + 1] == ']')
            {
                if (inColor)
                {
                    sb.Append("[/color]");
                    inColor = false;
                }

                // scan forward to hit the terminator \a (BEL) or \e\\ (ST)
                int j = i + 2;
                while (j < ansiLine.Length && ansiLine[j] != '\a'
                    && !(ansiLine[j] == '\e' && j + 1 < ansiLine.Length && ansiLine[j + 1] == '\\'))
                {
                    j++;
                }

                // advance past the terminator
                if (j < ansiLine.Length)
                {
                    if (ansiLine[j] == '\a')
                        i = j + 1;
                    else // \e\\
                        i = j + 2;
                }
                else
                    i = ansiLine.Length; // unterminated OSC, eat rest of line

                continue;
            }

            // handle CSI sequences (SGR colors)
            if (ansiLine[i] == '\e' && i + 1 < ansiLine.Length && ansiLine[i + 1] == '[')
            {
                if (inColor)
                {
                    sb.Append("[/color]");
                    inColor = false;
                }

                // scan forward to terminator
                int j = i + 2;
                while (j < ansiLine.Length && !char.IsAsciiLetter(ansiLine[j]))
                {
                    j++;
                }

                if (j < ansiLine.Length && ansiLine[j] == 'm')
                {
                    string sequence = ansiLine[(i + 2)..j];
                    i = j + 1;

                    foreach (string codeStr in sequence.Split(';'))
                    {
                        if (!int.TryParse(codeStr, out int code)
                            || !ServerLogsCommand.TryGetColorMarkup(code, out string? color)) continue;
                        if (inColor)
                        {
                            sb.Append("[/color]");
                            inColor = false;
                        }

                        sb.Append($"[color={color}]");
                        inColor = true;
                    }
                }
                else

                    // eat non color escape (e.g., \e[2J, \e[K)
                    i = j < ansiLine.Length ? j + 1 : ansiLine.Length;

                continue;
            }

            // unrecognized escape, skip the \e & next char, to avoid infinite loop
            if (ansiLine[i] == '\e')
            {
                if (inColor)
                {
                    sb.Append("[/color]");
                    inColor = false;
                }

                i += 2;
                continue;
            }

            int nextEscape = ansiLine.IndexOf('\e', i);
            if (nextEscape == -1) nextEscape = ansiLine.Length;
            string text = ansiLine[i..nextEscape];
            sb.Append(FormattedMessage.EscapeText(text));
            i = nextEscape;
        }

        if (inColor)
            sb.Append("[/color]");

        return sb.ToString();
    }

    internal static bool TryGetColorMarkup(int ansiCode, out string? colorName)
    {
        switch (ansiCode)
        {
            case 30: colorName = "black"; break;
            case 31: colorName = "red"; break;
            case 32: colorName = "green"; break;
            case 33: colorName = "yellow"; break;
            case 34: colorName = "blue"; break;
            case 35: colorName = "magenta"; break;
            case 36: colorName = "cyan"; break;
            case 37: colorName = "white"; break;
            case 90: colorName = "darkgray"; break;
            case 91: colorName = "red"; break;
            case 92: colorName = "green"; break;
            case 93: colorName = "yellow"; break;
            case 94: colorName = "blue"; break;
            case 95: colorName = "magenta"; break;
            case 96: colorName = "cyan"; break;
            case 97: colorName = "white"; break;

            default:
                colorName = null;
                return false;
        }

        return true;
    }

    private static (bool followMode, bool downloadMode, string? filter, int lineCount, string? explicitFile) ParseArgs(string[] args)
    {
        var followMode = false;
        var downloadMode = false;
        string? filter = null;
        var lineCount = 50;
        string? explicitFile = null;

        for (var i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.Equals("--follow") || arg.Equals("--tail"))
                followMode = true;
            else if (arg.Equals("--download"))
                downloadMode = true;
            else if (arg.Equals("--filter") && i + 1 < args.Length)
                filter = args[++i];
            else if (arg.Equals("--file") && i + 1 < args.Length)
                explicitFile = args[++i];
            else if (int.TryParse(arg, out int n))
                lineCount = Math.Clamp(n, 1, MaxLines);
            else
                filter = arg;
        }

        return (followMode, downloadMode, filter, lineCount, explicitFile);
    }

    // single 64KB chunk, scan in mem for \n, read forward -> avoids O(n) disk seeks and large heap allocations of
    // earlier approaches
    private static List<string> ReadLastLines(string filePath, int lineCount)
    {
        var result = new List<string>();
        if (lineCount <= 0) return result;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        long fileSize = fs.Length;
        if (fileSize == 0) return result;

        // needed bytes ~150B per \n, clamped at 64KB and 1MB.
        int estimatedNeededBytes = lineCount * 150;
        int bufferSize = Math.Clamp(estimatedNeededBytes, 65536, 1048576);
        long startPos = Math.Max(0, fileSize - bufferSize);
        var bytesToRead = (int)(fileSize - startPos);

        var buffer = new byte[bytesToRead];
        fs.Position = startPos;
        fs.ReadExactly(buffer, 0, bytesToRead);

        var newlineCount = 0;
        long targetPosition = startPos;

        // scan membuf backward
        for (int i = bytesToRead - 1; i >= 0; i--)
        {
            // ignore trailing \n at absolut eof (backward scan)
            if (startPos + i == fileSize - 1 && buffer[i] == '\n')
                continue;

            if (buffer[i] == '\n')
            {
                newlineCount++;
                if (newlineCount > lineCount)
                {
                    targetPosition = startPos + i + 1; // cutoff point
                    break;
                }
            }
        }

        // read forward from cutoff & ensure targetPosition valid UTF-8 byte
        if (targetPosition > 0)
        {
            fs.Position = targetPosition;
            int b = fs.ReadByte();
            while (targetPosition > 0 && b is >= 0x80 and < 0xC0)
            {
                targetPosition--;
                fs.Position = targetPosition;
                b = fs.ReadByte();
            }
        }

        fs.Position = targetPosition; // reset after read forward

        using var reader = new StreamReader(fs, Encoding.UTF8, false, 4096, true);
        while (reader.ReadLine() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
                result.Add(line);
        }

        // trailing empty line (forward read)
        while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
        {
            result.RemoveAt(result.Count - 1);
        }

        // small file? trim
        if (result.Count > lineCount)
            return result.Skip(result.Count - lineCount).ToList();

        return result;
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0 || (args.Length == 1 && string.IsNullOrEmpty(args[0])))
        {
            return CompletionResult.FromHintOptions(
                ["--list", "--download", "--follow", "--stop", "--file", "--filter"],
                "option");
        }

        string lastArg = args[^1];

        if (lastArg == "--file")
            return CompletionResult.FromHintOptions(GetLogFileCompletions(string.Empty), "log file");

        if (args is [.., "--file", _])
        {
            string currentPath = lastArg;
            return CompletionResult.FromHintOptions(GetLogFileCompletions(currentPath), "log file");
        }

        if (string.IsNullOrEmpty(lastArg) && args.Length >= 2)
        {
            string prevArg = args[^2];
            if (prevArg == "--filter")
                return CompletionResult.FromHint("filter pattern");
            if (prevArg == "--file")
                return CompletionResult.FromHintOptions(GetLogFileCompletions(string.Empty), "log file");
            return CompletionResult.FromHint("filter pattern or number of lines");
        }

        if (lastArg.StartsWith('-'))
        {
            HashSet<string> usedFlags = args.Where(a => a.StartsWith('-')).ToHashSet();
            var flags = new List<string>();
            if (!usedFlags.Contains("--list")) flags.Add("--list");
            if (!usedFlags.Contains("--download")) flags.Add("--download");
            if (!usedFlags.Contains("--follow") && !usedFlags.Contains("--tail"))
            {
                flags.Add("--follow");
                flags.Add("--tail");
            }

            if (!usedFlags.Contains("--stop")) flags.Add("--stop");
            if (!usedFlags.Contains("--file")) flags.Add("--file");
            if (!usedFlags.Contains("--filter")) flags.Add("--filter");
            return CompletionResult.FromHintOptions(flags, "flag");
        }

        var options = new List<CompletionOption>
        {
            new("50", "number of lines (default)"),
            new(MaxLines.ToString(), "number of lines (max)")
        };
        return CompletionResult.FromHintOptions(options, "filter pattern or number of lines");
    }

    private List<CompletionOption> GetLogFileCompletions(string filter)
    {
        var completions = new List<CompletionOption>();
        try
        {
            foreach (var file in EnumerateLogFiles())
            {
                string relPath = file.Name;
                if (string.IsNullOrEmpty(filter) || relPath.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                    completions.Add(new(relPath, "log file"));
            }
        }
        catch { }

        return completions;
    }
}
