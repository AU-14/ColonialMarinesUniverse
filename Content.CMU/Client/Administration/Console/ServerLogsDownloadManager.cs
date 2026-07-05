using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Content.Shared._CMU14.Administration.Console;
using Robust.Client.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.Administration.Console;

public sealed partial class ServerLogsDownloadManager : IPostInjectInit
{
    private const int CopyBufferSize = 64 * 1024;
    private const int MaxClientFileNameLength = 120;

    [Dependency] private IClientConsoleHost _console = default!;
    [Dependency] private ILogManager _log = default!;
    [Dependency] private IResourceManager _resource = default!;
    [Dependency] private ITransferManager _transfer = default!;

    private ISawmill _sawmill = default!;

    public void PostInject()
    {
        _sawmill = _log.GetSawmill("serverlogs.download");
        _transfer.RegisterTransferMessage(ServerLogsDownloadConstants.TransferKey, ReceiveDownload);
    }

    private async void ReceiveDownload(TransferReceivedEvent transfer)
    {
        ResPath? outputPath = null;

        try
        {
            await using var input = transfer.DataStream;

            var fileName = SanitizeFileName(await ReadFileName(input));
            var length = await ReadInt64(input);
            if (length < 0 || length > ServerLogsDownloadConstants.MaxDownloadBytes)
                throw new InvalidDataException("Server sent an invalid log file size.");

            _resource.UserData.CreateDir(ServerLogsDownloadConstants.ClientDownloadDirectory);
            outputPath = GetUniqueDownloadPath(fileName);

            await using (var output = _resource.UserData.Open(
                outputPath.Value,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                await CopyExactly(input, output, length);
                await output.FlushAsync();
            }

            _console.WriteLine(null,
                $"Downloaded server log '{fileName}' ({ByteHelpers.FormatBytes(length)}) to user data path {outputPath.Value}.");

            try
            {
                _resource.UserData.OpenOsWindow(ServerLogsDownloadConstants.ClientDownloadDirectory);
            }
            catch (Exception e)
            {
                _sawmill.Warning($"Failed to open server log download directory: {e}");
            }
        }
        catch (Exception e)
        {
            if (outputPath is { } partialPath)
                _resource.UserData.Delete(partialPath);

            _console.WriteError(null, $"Failed to download server log: {e.Message}");
            _sawmill.Error($"Failed to download server log: {e}");
        }
    }

    private ResPath GetUniqueDownloadPath(string fileName)
    {
        var extension = GetExtension(fileName);
        var stem = GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
            stem = "server-log";

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var baseName = $"{stem}-{timestamp}{extension}";

        var path = ServerLogsDownloadConstants.ClientDownloadDirectory / baseName;
        for (var i = 2; _resource.UserData.Exists(path); i++)
        {
            path = ServerLogsDownloadConstants.ClientDownloadDirectory / $"{stem}-{timestamp}-{i}{extension}";
        }

        return path;
    }

    private static string SanitizeFileName(string fileName)
    {
        fileName = GetLeafFileName(fileName);

        var builder = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
        {
            builder.Append(IsInvalidFileNameChar(c) ? '_' : c);
        }

        var sanitized = builder.ToString().Trim();
        if (!ResPath.IsValidFilename(sanitized) || sanitized is "." or "..")
            sanitized = "server-log.txt";

        var extension = GetExtension(sanitized);
        if (!ServerLogsDownloadConstants.IsAllowedLogExtension(extension))
            sanitized += ".txt";

        if (sanitized.Length <= MaxClientFileNameLength)
            return sanitized;

        extension = GetExtension(sanitized);
        var stem = GetFileNameWithoutExtension(sanitized);
        var maxStemLength = Math.Max(1, MaxClientFileNameLength - extension.Length);
        return $"{stem[..Math.Min(stem.Length, maxStemLength)]}{extension}";
    }

    private static string GetLeafFileName(string fileName)
    {
        fileName = fileName.Replace('\\', '/');
        var separator = fileName.LastIndexOf('/');
        return separator >= 0 ? fileName[(separator + 1)..] : fileName;
    }

    private static bool IsInvalidFileNameChar(char c)
    {
        return c < ' ' || c is '/' or '\\' or ':' or '*' or '?' or '"' or '<' or '>' or '|';
    }

    private static string GetExtension(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        if (dot <= 0 || dot == fileName.Length - 1)
            return string.Empty;

        return fileName[dot..];
    }

    private static string GetFileNameWithoutExtension(string fileName)
    {
        var extension = GetExtension(fileName);
        return extension.Length == 0 ? fileName : fileName[..^extension.Length];
    }

    private static async Task<string> ReadFileName(Stream stream)
    {
        var lengthBuffer = new byte[sizeof(int)];
        await stream.ReadExactlyAsync(lengthBuffer);

        var length = BinaryPrimitives.ReadInt32LittleEndian(lengthBuffer);
        if (length is <= 0 or > ServerLogsDownloadConstants.MaxFileNameBytes)
            throw new InvalidDataException("Server sent an invalid log file name.");

        var fileNameBuffer = new byte[length];
        await stream.ReadExactlyAsync(fileNameBuffer);
        return Encoding.UTF8.GetString(fileNameBuffer);
    }

    private static async Task<long> ReadInt64(Stream stream)
    {
        var buffer = new byte[sizeof(long)];
        await stream.ReadExactlyAsync(buffer);
        return BinaryPrimitives.ReadInt64LittleEndian(buffer);
    }

    private static async Task CopyExactly(Stream source, Stream destination, long length)
    {
        var buffer = new byte[CopyBufferSize];
        var remaining = length;
        while (remaining > 0)
        {
            var read = await source.ReadAsync(buffer.AsMemory(0, (int) Math.Min(buffer.Length, remaining)));
            if (read == 0)
                throw new InvalidDataException("Server log transfer ended early.");

            await destination.WriteAsync(buffer.AsMemory(0, read));
            remaining -= read;
        }
    }
}
