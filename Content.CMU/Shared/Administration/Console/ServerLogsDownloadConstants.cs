using Robust.Shared.Utility;

namespace Content.Shared._CMU14.Administration.Console;

public static class ServerLogsDownloadConstants
{
    public const string TransferKey = "CMUServerLogsDownload";
    public const int MaxFileNameBytes = 255;
    public const long MaxDownloadBytes = 512L * 1024L * 1024L;

    private static readonly string[] AllowedLogExtensions = [".log", ".txt"];

    public static readonly ResPath ClientDownloadDirectory = new("/serverlogs");

    public static bool IsAllowedLogExtension(string? extension)
    {
        foreach (var allowed in AllowedLogExtensions)
        {
            if (string.Equals(extension, allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
