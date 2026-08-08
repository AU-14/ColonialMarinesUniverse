using System;
using System.Collections.Generic;
using Content.Server._CMU14.Threats;
using Content.Server.GameTicking;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Tests.Server._CMU14.Threats;

[TestFixture]
public sealed class ThreatVoteLifecycleTest
{
    [TestCase(42, 42, GameRunLevel.InRound, true)]
    [TestCase(42, 43, GameRunLevel.InRound, false)]
    [TestCase(42, 42, GameRunLevel.PreRoundLobby, false)]
    public void VoteCanOnlyFinishInItsOriginRound(int voteRoundId,
        int currentRoundId,
        GameRunLevel runLevel,
        bool expected)
    {
        Assert.That(ThreatVoteSystem.CanFinishThreatVote(voteRoundId, currentRoundId, runLevel), Is.EqualTo(expected));
    }

    [Test]
    public void VoteCanOnlyConcludeOnce()
    {
        var concluded = false;

        Assert.Multiple(() =>
        {
            Assert.That(ThreatVoteSystem.TryConcludeThreatVote(ref concluded), Is.True);
            Assert.That(ThreatVoteSystem.TryConcludeThreatVote(ref concluded), Is.False);
        });
    }

    [Test]
    public void AbortCleanupRemovesOnlyThreatAssignments()
    {
        var leader = new NetUserId(Guid.NewGuid());
        var member = new NetUserId(Guid.NewGuid());
        var marine = new NetUserId(Guid.NewGuid());
        var assignments = new Dictionary<NetUserId, (ProtoId<JobPrototype>?, EntityUid)>
        {
            [leader] = (new ProtoId<JobPrototype>("AU14JobThreatLeader"), EntityUid.Invalid),
            [member] = (new ProtoId<JobPrototype>("AU14JobThreatMember"), EntityUid.Invalid),
            [marine] = (new ProtoId<JobPrototype>("AU14JobMarine"), EntityUid.Invalid),
        };

        int removed = ThreatSystem.RemoveThreatJobAssignments(assignments);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(2));
            Assert.That(assignments.Keys, Is.EquivalentTo(new[] { marine }));
        });
    }
}
