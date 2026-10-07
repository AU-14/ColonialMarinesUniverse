using System.Text.Json;
using System.Net;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Network;

namespace Content.IntegrationTests.Tests.Preferences;

public sealed partial class ServerDbSqliteTests
{
    [Test]
    public async Task AdminLogReplayPreservesCommittedRowsAndSavesTheUncommittedTail()
    {
        var db = GetDb(Server);
        var player = new NetUserId(Guid.NewGuid());
        await db.UpdatePlayerRecord(player, "CMU replay test", IPAddress.Loopback, null);
        var (server, _) = await db.AddOrGetServer("CMU admin log replay test");
        var round = await db.AddNewRound(server);
        var date = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        AdminLog Log(int id, string message) => new()
        {
            RoundId = round,
            Id = id,
            Type = LogType.Action,
            Impact = LogImpact.Low,
            Date = date,
            Message = message,
            Json = JsonDocument.Parse("{\"action\":\"test\"}"),
            Players = [new AdminLogPlayer { RoundId = round, LogId = id, PlayerUserId = player.UserId }],
        };

        await db.AddAdminLogs([Log(1, "committed before response was lost")]);
        await db.AddAdminLogs([Log(1, "committed before response was lost"), Log(2, "pending")]);
        Assert.That(await db.CountAdminLogs(round), Is.EqualTo(2));
        var messages = new List<string>();
        await foreach (var message in db.GetAdminLogMessages(new() { Round = round, AnyPlayers = [player.UserId] }))
            messages.Add(message);
        Assert.That(messages, Is.EquivalentTo(new[] { "committed before response was lost", "pending" }));

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await db.AddAdminLogs([Log(1, "different event using the same ID"), Log(3, "must remain pending")]));
        Assert.That(await db.CountAdminLogs(round), Is.EqualTo(2), "Conflicting data must not be silently discarded or partly saved.");

        var nextRound = await db.AddNewRound(server);
        var next = Log(1, "same ID in another round");
        next.RoundId = nextRound;
        next.Players[0].RoundId = nextRound;
        await db.AddAdminLogs([next]);
        Assert.That(await db.CountAdminLogs(nextRound), Is.EqualTo(1));
    }
}
