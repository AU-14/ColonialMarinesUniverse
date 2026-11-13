using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.CMU14.Threats;
using Content.Shared.Roles;
using NUnit.Framework;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.CMU.Tests.Threats;

[TestFixture]
public sealed class ThreatVoteSeatingTest
{
    private static NetUserId Player(int index)
        => new(new Guid($"00000000-0000-0000-0000-{index:D12}"));

    // FLOW-103
    [Test]
    public void ExplicitPreferenceVotersAreOrderedFirstAndKeepTheirShuffledOrder()
    {
        var shuffled = new List<NetUserId> { Player(1), Player(2), Player(3), Player(4), Player(5) };
        var explicitVoters = new HashSet<NetUserId> { Player(2), Player(5) };

        var ordered = ThreatVoteSelection.OrderByThreatPreference(shuffled, explicitVoters.Contains);

        Assert.That(ordered, Is.EqualTo(new[] { Player(2), Player(5), Player(1), Player(3), Player(4) }));
    }

    [Test]
    public void ExplicitPreferenceVotersAreSeatedBeforeAnyThreatVoters()
    {
        // Six voters, three seats. The three explicit voters sit at the back of the shuffle.
        var shuffled = Enumerable.Range(1, 6).Select(Player).ToList();
        var explicitVoters = new HashSet<NetUserId> { Player(4), Player(5), Player(6) };

        var ordered = ThreatVoteSelection.OrderByThreatPreference(shuffled, explicitVoters.Contains);
        var seated = ThreatVoteSelection.BuildSpawnAssignments(ordered, leaderBodies: 0, memberBodies: 3)
            .Select(assignment => assignment.Player)
            .ToList();

        Assert.That(seated, Is.EquivalentTo(explicitVoters));
    }

    [Test]
    public void LeaderPriorityIsStillRespectedAfterPreferenceOrdering()
    {
        var anyThreatLeader = Player(1);
        var explicitMember = Player(2);
        var explicitVoters = new HashSet<NetUserId> { explicitMember };
        var jobs = new Dictionary<NetUserId, ProtoId<JobPrototype>>
        {
            [anyThreatLeader] = ThreatVoteSelection.ThreatLeaderJobId,
            [explicitMember] = ThreatVoteSelection.ThreatMemberJobId,
        };

        var ordered = ThreatVoteSelection.OrderByThreatPreference(
            new List<NetUserId> { anyThreatLeader, explicitMember },
            explicitVoters.Contains);
        var held = ordered.Select(player => new ThreatVoteAssignment(player, jobs[player])).ToList();
        var assignments = ThreatVoteSelection.BuildSpawnAssignments(held, leaderBodies: 1, memberBodies: 1);

        Assert.That(assignments, Is.EquivalentTo(new[]
        {
            new ThreatVoteAssignment(anyThreatLeader, ThreatVoteSelection.ThreatLeaderJobId),
            new ThreatVoteAssignment(explicitMember, ThreatVoteSelection.ThreatMemberJobId),
        }));
    }

    // FLOW-121
    [Test]
    public void ThreatIsBlockedOnlyWhileOneOfItsBlockedRulesIsAdded()
    {
        var hunt = new EntProtoId("CMUYautjaPredatorRound");
        var blocked = new List<EntProtoId> { hunt };

        Assert.That(ThreatVoteSelection.IsBlockedByRules(blocked, new[] { "DistressSignal", hunt.Id }), Is.True);
        Assert.That(ThreatVoteSelection.IsBlockedByRules(blocked, new[] { "DistressSignal" }), Is.False);
        Assert.That(ThreatVoteSelection.IsBlockedByRules(blocked, Array.Empty<string>()), Is.False);
        Assert.That(ThreatVoteSelection.IsBlockedByRules(new List<EntProtoId>(), new[] { hunt.Id }), Is.False);
    }
}
