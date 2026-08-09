using System;
using System.Collections.Generic;
using Content.Shared._RMC14.Xenonids.JoinXeno;
using NUnit.Framework;
using Robust.Shared.Network;

namespace Content.Tests.Shared._RMC14.Xenonids.JoinXeno;

[TestFixture]
[TestOf(typeof(LarvaPoolState))]
public sealed class LarvaPoolStateTest
{
    [Test]
    public void OlderCandidateSortsFirst()
    {
        var pool = new LarvaPoolState();
        var older = User(1);
        var newer = User(2);
        var candidates = new List<NetUserId> { newer, older };

        pool.RecordJoined(older, TimeSpan.FromMinutes(1));
        pool.RecordJoined(newer, TimeSpan.FromMinutes(2));
        candidates.Sort(pool.Compare);

        Assert.That(candidates, Is.EqualTo(new[] { older, newer }));
    }

    [Test]
    public void LaterDeathMovesCandidateBack()
    {
        var pool = new LarvaPoolState();
        var diedAgain = User(1);
        var waiting = User(2);
        var candidates = new List<NetUserId> { diedAgain, waiting };

        pool.RecordJoined(diedAgain, TimeSpan.FromMinutes(1));
        pool.RecordJoined(waiting, TimeSpan.FromMinutes(2));
        pool.RecordDeath(diedAgain, TimeSpan.FromMinutes(3), preserveTime: false);
        candidates.Sort(pool.Compare);

        Assert.That(candidates, Is.EqualTo(new[] { waiting, diedAgain }));
    }

    [Test]
    public void EqualTimesHaveStableOrder()
    {
        var pool = new LarvaPoolState();
        var first = User(1);
        var second = User(2);
        var candidates = new List<NetUserId> { second, first };

        pool.RecordJoined(first, TimeSpan.FromMinutes(1));
        pool.RecordJoined(second, TimeSpan.FromMinutes(1));
        candidates.Sort(pool.Compare);

        Assert.That(candidates, Is.EqualTo(new[] { first, second }));
    }

    [Test]
    public void ExemptDeathPreservesOriginalPosition()
    {
        var pool = new LarvaPoolState();
        var exempt = User(1);
        var waiting = User(2);
        var candidates = new List<NetUserId> { waiting, exempt };

        pool.RecordJoined(exempt, TimeSpan.FromMinutes(1));
        pool.RecordJoined(waiting, TimeSpan.FromMinutes(2));
        pool.RecordDeath(exempt, TimeSpan.FromMinutes(3), preserveTime: true);
        candidates.Sort(pool.Compare);

        Assert.That(candidates, Is.EqualTo(new[] { exempt, waiting }));
    }

    [Test]
    public void EstimatesPositionAmongCurrentlyEligibleCandidates()
    {
        var pool = new LarvaPoolState();
        var older = User(1);
        var candidate = User(2);
        var newer = User(3);

        pool.RecordJoined(older, TimeSpan.FromMinutes(1));
        pool.RecordJoined(candidate, TimeSpan.FromMinutes(2));
        pool.RecordJoined(newer, TimeSpan.FromMinutes(3));

        Assert.That(pool.GetPosition(candidate, new[] { newer, older }), Is.EqualTo(2));
    }

    private static NetUserId User(int value)
    {
        return new NetUserId(new Guid(value, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }
}
