using System;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Shared._RMC14.Xenonids.JoinXeno;

[TestFixture]
[TestOf(typeof(LarvaQueueState))]
public sealed class LarvaQueueStateTest
{
    [Test]
    public void TryDequeueReadyUsesJoinOrder()
    {
        var queue = new LarvaQueueState();
        var first = User(1);
        var second = User(2);
        var third = User(3);

        queue.AddReady(first);
        queue.AddReady(second);
        queue.AddReady(third);

        Assert.Multiple(() =>
        {
            Assert.That(queue.TryDequeueReady(out var next), Is.True);
            Assert.That(next, Is.EqualTo(first));
            Assert.That(queue.TryDequeueReady(out next), Is.True);
            Assert.That(next, Is.EqualTo(second));
            Assert.That(queue.ReadyCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void PromoteWaitingMovesAllReadyUsersWithoutSkipping()
    {
        var queue = new LarvaQueueState();
        var first = User(1);
        var second = User(2);
        var third = User(3);

        queue.AddWaiting(first, TimeSpan.FromSeconds(10));
        queue.AddWaiting(second, TimeSpan.FromSeconds(20));
        queue.AddWaiting(third, TimeSpan.FromSeconds(30));

        var promoted = queue.PromoteWaiting(TimeSpan.FromSeconds(25));

        Assert.Multiple(() =>
        {
            Assert.That(promoted, Is.EqualTo(new[] { first, second }));
            Assert.That(queue.ReadyUsers, Is.EqualTo(new[] { first, second }));
            Assert.That(queue.WaitingCount, Is.EqualTo(1));
            Assert.That(queue.Contains(third), Is.True);
        });
    }

    [Test]
    public void RemoveDeletesUserFromReadyOrWaitingQueue()
    {
        var queue = new LarvaQueueState();
        var ready = User(1);
        var waiting = User(2);

        queue.AddReady(ready);
        queue.AddWaiting(waiting, TimeSpan.FromSeconds(10));

        Assert.Multiple(() =>
        {
            Assert.That(queue.Remove(ready), Is.True);
            Assert.That(queue.Remove(waiting), Is.True);
            Assert.That(queue.ReadyCount, Is.Zero);
            Assert.That(queue.WaitingCount, Is.Zero);
        });
    }

    private static NetUserId User(int value)
    {
        return new NetUserId(new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }
}
