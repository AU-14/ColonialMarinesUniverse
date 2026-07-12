using System.Buffers;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Content.Shared._CMU14.Administration.Console;
using Robust.Shared.Network.Transfer;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Administration.Console;

public sealed partial class ServerLogsDownloadManager : IPostInjectInit
{
    [Dependency] private ITransferManager _transfer = default!;

    public void PostInject()
    {
        _transfer.RegisterTransferMessage(ServerLogsDownloadConstants.TransferKey);
    }

    public async Task SendLogFile(ICommonSession session, FileInfo file)
    {
        await using var input = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var length = input.Length;
        if (length > ServerLogsDownloadConstants.MaxDownloadBytes)
            throw new InvalidOperationException("Log file is too large to download safely.");

        await using var transfer = _transfer.StartTransfer(session.Channel, ServerLogsDownloadConstants.TransferKey);
        await WriteHeader(transfer, file.Name, length);
        await CopyBytes(input, transfer, length);
    }

    private static async Task WriteHeader(Stream stream, string fileName, long length)
    {
        var fileNameBytes = Encoding.UTF8.GetBytes(fileName);
        if (fileNameBytes.Length is <= 0 or > ServerLogsDownloadConstants.MaxFileNameBytes)
            throw new InvalidOperationException("Log file name is too long to download safely.");

        var intBuffer = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(intBuffer, fileNameBytes.Length);
        await stream.WriteAsync(intBuffer);
        await stream.WriteAsync(fileNameBytes);

        var longBuffer = new byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(longBuffer, length);
        await stream.WriteAsync(longBuffer);
    }

    private static async Task CopyBytes(Stream source, Stream destination, long length)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            var remaining = length;
            while (remaining > 0)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, (int) Math.Min(buffer.Length, remaining)));
                if (read == 0)
                    throw new EndOfStreamException("Log file ended before the announced download size.");

                await destination.WriteAsync(buffer.AsMemory(0, read));
                remaining -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
