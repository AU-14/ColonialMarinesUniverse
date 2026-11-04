using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Database;
using Content.Server.GameTicking;
using Moq;
using NUnit.Framework;
using Robust.Shared.Log;
using Robust.Shared.Timing;

namespace Content.Tests.Server.Administration;

[TestFixture]
public sealed class AdminLogBatchTest
{
    private static (AdminLogManager Manager, ConcurrentQueue<AdminLog> Queue) Create(IServerDbManager database)
    {
        var manager = new AdminLogManager();
        Set(manager, "_db", database);
        Set(manager, "_timing", Mock.Of<IGameTiming>());
        Set(manager, "_sawmill", Mock.Of<ISawmill>());
        Set(manager, "_queueMax", 2);
        Set(manager, "_queueSendDelay", TimeSpan.FromSeconds(5));
        Set(manager, "_runLevel", GameRunLevel.InRound);
        Set(manager, "_currentRoundId", 10);
        var queue = (ConcurrentQueue<AdminLog>) typeof(AdminLogManager)
            .GetField("_logQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        return (manager, queue);
    }

    private static void Set(AdminLogManager manager, string field, object value)
        => typeof(AdminLogManager).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, value);

    private static Task Save(AdminLogManager manager)
        => (Task) typeof(AdminLogManager).GetMethod("TrySaveLogs", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(manager, null)!;

    [Test]
    public async Task ShutdownWaitsForTheActiveWriteAndDrainsConcurrentAdditionsInBoundedBatches()
    {
        var firstWrite = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var batches = new List<List<AdminLog>>();
        var db = new Mock<IServerDbManager>();
        db.Setup(d => d.AddAdminLogs(It.IsAny<List<AdminLog>>())).Returns((List<AdminLog> logs) =>
        {
            batches.Add(logs.ToList());
            return batches.Count == 1 ? firstWrite.Task : Task.CompletedTask;
        });
        var (manager, queue) = Create(db.Object);
        foreach (var id in Enumerable.Range(1, 3))
            queue.Enqueue(new AdminLog { Id = id, RoundId = 10 });

        var saving = Save(manager);
        queue.Enqueue(new AdminLog { Id = 4, RoundId = 10 });
        queue.Enqueue(new AdminLog { Id = 5, RoundId = 10 });
        var shutdown = manager.Shutdown();
        Assert.That(shutdown.IsCompleted, Is.False, "Shutdown must await the in-flight database write.");
        Assert.That(batches, Has.Count.EqualTo(1));
        firstWrite.SetResult();
        await Task.WhenAll(saving, shutdown);

        Assert.That(batches.Select(b => b.Count), Is.EqualTo(new[] { 2, 2, 1 }));
        Assert.That(batches.SelectMany(b => b).Select(l => l.Id), Is.EqualTo(Enumerable.Range(1, 5)));
        Assert.That(queue, Is.Empty);
    }

    [Test]
    public async Task RoundStartFlushesLobbyHistoryBeforeTheBusyRoundQueue()
    {
        var batches = new List<List<AdminLog>>();
        var db = new Mock<IServerDbManager>();
        db.Setup(d => d.AddAdminLogs(It.IsAny<List<AdminLog>>())).Returns((List<AdminLog> logs) =>
        {
            batches.Add(logs.ToList());
            return Task.CompletedTask;
        });
        var (manager, queue) = Create(db.Object);
        manager.RoundStarting(10);
        var lobby = (ConcurrentQueue<AdminLog>) typeof(AdminLogManager)
            .GetField("_preRoundLogQueue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        lobby.Enqueue(new AdminLog { Id = 1, Message = "Lobby log 1", Players = new() });
        lobby.Enqueue(new AdminLog { Id = 2, Message = "Lobby log 2", Players = new() });
        queue.Enqueue(new AdminLog { Id = 3, RoundId = 10 });
        await Save(manager);
        Assert.That(batches.Single().Select(l => l.Id), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(batches.Single().Select(l => l.RoundId), Is.All.EqualTo(10));
        await manager.Shutdown();
        Assert.That(batches.SelectMany(b => b).Select(l => l.Id), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task FailedBatchKeepsItsRoundAndRetriesEvenWithNoNewQueuedLogs()
    {
        var attempts = new List<List<AdminLog>>();
        var db = new Mock<IServerDbManager>();
        db.Setup(d => d.AddAdminLogs(It.IsAny<List<AdminLog>>())).Returns((List<AdminLog> logs) =>
        {
            attempts.Add(logs.ToList());
            return attempts.Count == 1 ? Task.FromException(new InvalidOperationException("Database unavailable")) : Task.CompletedTask;
        });
        var (manager, queue) = Create(db.Object);
        queue.Enqueue(new AdminLog { Id = 1, RoundId = 10 });
        await Save(manager);
        Assert.That(queue, Is.Empty);
        Set(manager, "_currentRoundId", 11);
        await manager.Shutdown();
        Assert.That(attempts, Has.Count.EqualTo(2));
        Assert.That(attempts[1].Single().RoundId, Is.EqualTo(10));
        Assert.That(attempts[1].Single().Id, Is.EqualTo(1));
    }
}
