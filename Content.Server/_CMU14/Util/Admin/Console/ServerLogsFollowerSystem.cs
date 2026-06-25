using System.IO;
using System.Text;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Administration.Console;

public sealed partial class ServerLogsFollowerSystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chatManager = default!;

    private static readonly string[] NoiseFilters =
    [
        "MainLoop: Cannot keep up!",
        "] admin.logs: ", // sawmill chunk as not to excl. "admin.logs" itself
        "] db.op: ",
        "] battlebuddy: ",
        "] con: ",
        "] net.ent: "
    ];

    private const int MaxLinesPerTick = 20;
    private const float UpdateRate = 0.5f; // in seconds
    private float _updateAccumulator;

    public override void Update(float frameTime)
    {
        _updateAccumulator += frameTime;
        if (_updateAccumulator < UpdateRate)
            return;
        _updateAccumulator = 0f;


        EntityQueryEnumerator<ServerLogsFollowerComponent> query = EntityQueryEnumerator<ServerLogsFollowerComponent>();
        while (query.MoveNext(out EntityUid uid, out ServerLogsFollowerComponent? follower))
        {
            if (follower.Session == null || !_playerManager.TryGetSessionById(follower.Session.UserId, out _))
            {
                RemComp<ServerLogsFollowerComponent>(uid);
                continue;
            }

            try { UpdateFollower(follower, uid); }
            catch (Exception ex)
            {
                // send error to admin and stop following
                SendLine(follower.Session, $"Follow error: {ex.Message}");
                RemComp<ServerLogsFollowerComponent>(uid);
            }
        }
    }

    private void UpdateFollower(ServerLogsFollowerComponent follower, EntityUid uid)
    {
        if (follower.Session == null)
            return;

        var fileInfo = new FileInfo(follower.FilePath);
        if (!fileInfo.Exists)
        {
            SendLine(follower.Session, $"Log file '{follower.FilePath}' no longer exists.");
            RemComp<ServerLogsFollowerComponent>(uid);
            return;
        }

        long newSize = fileInfo.Length;
        if (newSize <= follower.LastPosition)
        {
            // file change, reset to new content
            if (newSize < follower.LastPosition)
                follower.LastPosition = newSize;
            return;
        }

        using var fs = new FileStream(follower.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(follower.LastPosition, SeekOrigin.Begin);
        using var reader = new StreamReader(fs, Encoding.UTF8, false, 4096, true);

        int linesSent = 0, linesRead = 0;
        const int maxLinesReadPerTick = 500;

        long consumed = follower.LastPosition;
        while (reader.ReadLine() is { } line
            && linesSent < MaxLinesPerTick
            && linesRead < maxLinesReadPerTick)
        {
            linesRead++;
            consumed += Encoding.UTF8.GetByteCount(line) + 1; // based on linux server (\n line endings)

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.Contains("[TAIL]")) continue; // feedback loop
            if (Array.Exists(NoiseFilters, f => line.Contains(f))) continue;
            if (follower.Filter != null
                && !line.Contains(follower.Filter, StringComparison.OrdinalIgnoreCase)) continue;

            SendLine(follower.Session, line);
            linesSent++;
        }

        follower.LastPosition = consumed; // update read offset (not eof)
    }

    private void SendLine(ICommonSession session, string line)
    {
        string markupLine = ServerLogsCommand.ConvertAnsiToMarkup(line);
        if (string.IsNullOrWhiteSpace(markupLine)) return;

        var message = $"[color=yellow][TAIL][/color] {markupLine}";
        _chatManager.ChatMessageToOne(ChatChannel.Admin, message, message, EntityUid.Invalid, false, session.Channel);
    }
}
