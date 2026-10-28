using System.Collections.Generic;
using Content.Shared.CMU14.Xenonids.Evolution;
using NUnit.Framework;
using Robust.Shared.GameObjects;

namespace Content.Tests.CMU14.Xenonids.Evolution;

[TestFixture]
[TestOf(typeof(CMUEvolutionQueueOps))]
public sealed class CMUEvolutionQueueOpsTest
{
    private static CMUEvolutionQueueEntry Entry(int id)
    {
        // the wanted caste is irrelevant to the ordering logic
        return new CMUEvolutionQueueEntry(new EntityUid(id), default);
    }

    private static List<EntityUid> Xenos(List<CMUEvolutionQueueEntry> queue)
    {
        return queue.ConvertAll(entry => entry.Xeno);
    }

    [Test]
    public void EnqueueAppendsNonLeadersInJoinOrder()
    {
        var queue = new List<CMUEvolutionQueueEntry>();

        Assert.Multiple(() =>
        {
            Assert.That(CMUEvolutionQueueOps.Enqueue(queue, Entry(1), _ => false), Is.EqualTo(0));
            Assert.That(CMUEvolutionQueueOps.Enqueue(queue, Entry(2), _ => false), Is.EqualTo(1));
            Assert.That(CMUEvolutionQueueOps.Enqueue(queue, Entry(3), _ => false), Is.EqualTo(2));
            Assert.That(Xenos(queue), Is.EqualTo(new[] { new EntityUid(1), new EntityUid(2), new EntityUid(3) }));
        });
    }

    [Test]
    public void EnqueueInsertsLeadersAheadOfNonLeadersButBehindLeaders()
    {
        var queue = new List<CMUEvolutionQueueEntry>();
        var leaders = new HashSet<EntityUid> { new(3), new(4) };
        bool IsLeader(EntityUid uid) => leaders.Contains(uid);

        CMUEvolutionQueueOps.Enqueue(queue, Entry(1), IsLeader);
        CMUEvolutionQueueOps.Enqueue(queue, Entry(2), IsLeader);
        var firstLeader = CMUEvolutionQueueOps.Enqueue(queue, Entry(3), IsLeader);
        var secondLeader = CMUEvolutionQueueOps.Enqueue(queue, Entry(4), IsLeader);

        Assert.Multiple(() =>
        {
            Assert.That(firstLeader, Is.EqualTo(0));
            Assert.That(secondLeader, Is.EqualTo(1));
            Assert.That(Xenos(queue), Is.EqualTo(new[]
            {
                new EntityUid(3),
                new EntityUid(4),
                new EntityUid(1),
                new EntityUid(2),
            }));
        });
    }

    [Test]
    public void MoveReordersAndRemovesEntries()
    {
        var queue = new List<CMUEvolutionQueueEntry> { Entry(1), Entry(2), Entry(3) };

        Assert.Multiple(() =>
        {
            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(3), CMUEvolutionQueueMove.Up), Is.True);
            Assert.That(Xenos(queue), Is.EqualTo(new[] { new EntityUid(1), new EntityUid(3), new EntityUid(2) }));

            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(1), CMUEvolutionQueueMove.Down), Is.True);
            Assert.That(Xenos(queue), Is.EqualTo(new[] { new EntityUid(3), new EntityUid(1), new EntityUid(2) }));

            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(2), CMUEvolutionQueueMove.Front), Is.True);
            Assert.That(Xenos(queue), Is.EqualTo(new[] { new EntityUid(2), new EntityUid(3), new EntityUid(1) }));

            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(3), CMUEvolutionQueueMove.Remove), Is.True);
            Assert.That(Xenos(queue), Is.EqualTo(new[] { new EntityUid(2), new EntityUid(1) }));
        });
    }

    [Test]
    public void MoveRejectsMissingEntriesAndNoOps()
    {
        var queue = new List<CMUEvolutionQueueEntry> { Entry(1), Entry(2) };

        Assert.Multiple(() =>
        {
            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(9), CMUEvolutionQueueMove.Up), Is.False);
            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(1), CMUEvolutionQueueMove.Up), Is.False);
            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(1), CMUEvolutionQueueMove.Front), Is.False);
            Assert.That(CMUEvolutionQueueOps.Move(queue, new EntityUid(2), CMUEvolutionQueueMove.Down), Is.False);
            Assert.That(Xenos(queue), Is.EqualTo(new[] { new EntityUid(1), new EntityUid(2) }));
        });
    }
}
